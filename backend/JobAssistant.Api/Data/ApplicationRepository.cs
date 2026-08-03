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

    public async Task<IReadOnlyList<JobApplication>> ListAsync(
        ApplicationListQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new ApplicationListQuery();

        var sql = """
            SELECT id, company, role, status, applied_at, notes, created_at, updated_at
            FROM applications
            WHERE 1=1
            """;

        if (query.Status is not null)
        {
            sql += " AND status = @status";
        }

        if (query.Search is not null)
        {
            sql += " AND (company ILIKE @search OR role ILIKE @search)";
        }

        sql += " ORDER BY " + OrderByClause(query.SortBy, query.SortDir) + ";";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);

        if (query.Status is not null)
        {
            cmd.Parameters.AddWithValue("status", query.Status);
        }

        if (query.Search is not null)
        {
            cmd.Parameters.AddWithValue("search", "%" + query.Search + "%");
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var results = new List<JobApplication>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadApplication(reader));
        }

        return results;
    }

    public async Task<JobApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
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

        return ReadApplication(reader);
    }

    public async Task<JobApplication> CreateAsync(
        CreateApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var status = NormalizeStatus(request.Status);
        const string sql = """
            INSERT INTO applications (company, role, status, applied_at, notes)
            VALUES (@company, @role, @status, @applied_at, @notes)
            RETURNING id, company, role, status, applied_at, notes, created_at, updated_at;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("company", request.Company.Trim());
        cmd.Parameters.AddWithValue("role", request.Role.Trim());
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("applied_at", (object?)request.AppliedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)request.Notes ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadApplication(reader);
    }

    public async Task<JobApplication?> UpdateAsync(
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
        var status = request.Status is not null ? NormalizeStatus(request.Status) : existing.Status;
        var appliedAt = request.AppliedAt ?? existing.AppliedAt;
        var notes = request.Notes ?? existing.Notes;

        const string sql = """
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

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("company", company);
        cmd.Parameters.AddWithValue("role", role);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("applied_at", (object?)appliedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadApplication(reader);
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

    private static string OrderByClause(string sortBy, string sortDir)
    {
        var dir = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        return sortBy.ToLowerInvariant() switch
        {
            "company" => $"company {dir}",
            "role" => $"role {dir}",
            "status" => $"status {dir}",
            "created" => $"created_at {dir}",
            "updated" => $"updated_at {dir}",
            _ => $"COALESCE(applied_at, created_at) {dir}",
        };
    }

    private static JobApplication ReadApplication(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetDateTime(6),
            reader.GetDateTime(7));

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
