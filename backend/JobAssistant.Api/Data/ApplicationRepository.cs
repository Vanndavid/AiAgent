using JobAssistant.Api.Models;
using Npgsql;

namespace JobAssistant.Api.Data;

public sealed class ApplicationRepository
{
    private readonly string _connectionString;

    public ApplicationRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "schema.sql");
        if (!File.Exists(schemaPath))
        {
            schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "schema.sql");
        }

        var sql = await File.ReadAllTextAsync(schemaPath, cancellationToken);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobApplicationDetail>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, company, role, status, applied_at, notes, created_at, updated_at
            FROM applications
            ORDER BY COALESCE(applied_at, created_at) DESC;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var results = new List<JobApplicationDetail>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadApplicationDetail(reader, []));
        }

        await reader.CloseAsync();

        if (results.Count == 0)
        {
            return results;
        }

        var eventsByApplication = await LoadEventsByApplicationIdsAsync(
            conn,
            results.Select(application => application.Id),
            cancellationToken);

        return results
            .Select(application => application with
            {
                Events = eventsByApplication.GetValueOrDefault(application.Id, []),
            })
            .ToList();
    }

    public async Task<JobApplicationDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, company, role, status, applied_at, notes, created_at, updated_at
            FROM applications
            WHERE id = @id;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var application = ReadApplicationDetail(reader, []);
        await reader.CloseAsync();

        var events = await ListEventsAsync(conn, id, cancellationToken);
        return application with { Events = events };
    }

    public async Task<IReadOnlyList<ApplicationEvent>> ListEventsAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        return await ListEventsAsync(conn, applicationId, cancellationToken);
    }

    public async Task<JobApplicationDetail> CreateAsync(
        CreateApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Status is not null && !string.Equals(request.Status, ApplicationStatuses.Saved, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("New applications must start with status 'saved'.");
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string insertApplicationSql = """
            INSERT INTO applications (company, role, status, applied_at, notes)
            VALUES (@company, @role, @status, @applied_at, @notes)
            RETURNING id, company, role, status, applied_at, notes, created_at, updated_at;
            """;

        await using var insertCmd = new NpgsqlCommand(insertApplicationSql, conn, tx);
        insertCmd.Parameters.AddWithValue("company", request.Company.Trim());
        insertCmd.Parameters.AddWithValue("role", request.Role.Trim());
        insertCmd.Parameters.AddWithValue("status", ApplicationStatuses.Saved);
        insertCmd.Parameters.AddWithValue("applied_at", (object?)request.AppliedAt ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("notes", (object?)request.Notes ?? DBNull.Value);

        JobApplicationDetail application;
        await using (var reader = await insertCmd.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            application = ReadApplicationDetail(reader, []);
        }

        var createdEvent = await InsertEventAsync(
            conn,
            tx,
            application.Id,
            fromStatus: null,
            toStatus: ApplicationStatuses.Saved,
            cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return application with { Events = [createdEvent] };
    }

    public async Task<JobApplicationDetail?> UpdateAsync(
        Guid id,
        UpdateApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var company = request.Company?.Trim() ?? existing.Company;
        var role = request.Role?.Trim() ?? existing.Role;
        var notes = request.Notes ?? existing.Notes;
        var appliedAt = request.AppliedAt ?? existing.AppliedAt;
        var nextStatus = request.Status is not null ? NormalizeStatus(request.Status) : existing.Status;

        if (request.Status is not null && !string.Equals(nextStatus, existing.Status, StringComparison.Ordinal))
        {
            if (!ApplicationStatusTransitions.CanTransition(existing.Status, nextStatus))
            {
                throw new InvalidStatusTransitionException(existing.Status, nextStatus);
            }
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE applications
            SET company = @company,
                role = @role,
                status = @status,
                applied_at = @applied_at,
                notes = @notes,
                updated_at = NOW()
            WHERE id = @id
            RETURNING id, company, role, status, applied_at, notes, created_at, updated_at;
            """;

        await using var updateCmd = new NpgsqlCommand(updateSql, conn, tx);
        updateCmd.Parameters.AddWithValue("id", id);
        updateCmd.Parameters.AddWithValue("company", company);
        updateCmd.Parameters.AddWithValue("role", role);
        updateCmd.Parameters.AddWithValue("status", nextStatus);
        updateCmd.Parameters.AddWithValue("applied_at", (object?)appliedAt ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value);

        JobApplicationDetail updated;
        await using (var reader = await updateCmd.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                await tx.RollbackAsync(cancellationToken);
                return null;
            }

            updated = ReadApplicationDetail(reader, []);
        }

        if (request.Status is not null && !string.Equals(nextStatus, existing.Status, StringComparison.Ordinal))
        {
            await InsertEventAsync(conn, tx, id, existing.Status, nextStatus, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        var events = await ListEventsAsync(id, cancellationToken);
        return updated with { Events = events };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM applications WHERE id = @id;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    private static async Task<IReadOnlyList<ApplicationEvent>> ListEventsAsync(
        NpgsqlConnection conn,
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, application_id, from_status, to_status, created_at
            FROM application_events
            WHERE application_id = @application_id
            ORDER BY created_at ASC;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("application_id", applicationId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var events = new List<ApplicationEvent>();
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(ReadEvent(reader));
        }

        return events;
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<ApplicationEvent>>> LoadEventsByApplicationIdsAsync(
        NpgsqlConnection conn,
        IEnumerable<Guid> applicationIds,
        CancellationToken cancellationToken)
    {
        var ids = applicationIds.ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        const string sql = """
            SELECT id, application_id, from_status, to_status, created_at
            FROM application_events
            WHERE application_id = ANY(@application_ids)
            ORDER BY created_at ASC;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("application_ids", ids);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var eventsByApplication = new Dictionary<Guid, List<ApplicationEvent>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var applicationEvent = ReadEvent(reader);
            if (!eventsByApplication.TryGetValue(applicationEvent.ApplicationId, out var events))
            {
                events = [];
                eventsByApplication[applicationEvent.ApplicationId] = events;
            }

            events.Add(applicationEvent);
        }

        return eventsByApplication.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ApplicationEvent>)pair.Value);
    }

    private static async Task<ApplicationEvent> InsertEventAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid applicationId,
        string? fromStatus,
        string toStatus,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO application_events (application_id, from_status, to_status)
            VALUES (@application_id, @from_status, @to_status)
            RETURNING id, application_id, from_status, to_status, created_at;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("application_id", applicationId);
        cmd.Parameters.AddWithValue("from_status", (object?)fromStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("to_status", toStatus);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadEvent(reader);
    }

    private static JobApplicationDetail ReadApplicationDetail(
        NpgsqlDataReader reader,
        IReadOnlyList<ApplicationEvent> events) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetDateTime(6),
            reader.GetDateTime(7),
            events);

    private static ApplicationEvent ReadEvent(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.GetDateTime(4));

    private static string NormalizeStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status)
            ? ApplicationStatuses.Saved
            : status.Trim().ToLowerInvariant();

        if (!ApplicationStatuses.All.Contains(normalized))
        {
            throw new ArgumentException(
                $"Invalid status '{status}'. Allowed: {string.Join(", ", ApplicationStatuses.All)}.");
        }

        return normalized;
    }
}
