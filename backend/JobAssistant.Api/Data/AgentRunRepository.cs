using System.Text.Json;
using JobAssistant.Api.Models;
using Npgsql;

namespace JobAssistant.Api.Data;

public sealed class AgentRunRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;

    public AgentRunRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<AgentRunRecord> CreateAsync(
        string goal,
        string finalAnswer,
        IReadOnlyList<string> scratchpad,
        IReadOnlyList<string> toolsUsed,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO agent_runs (goal, final_answer, scratchpad, tools_used)
            VALUES (@goal, @final_answer, CAST(@scratchpad AS jsonb), CAST(@tools_used AS jsonb))
            RETURNING id, goal, final_answer, scratchpad, tools_used, created_at;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("goal", goal);
        cmd.Parameters.AddWithValue("final_answer", finalAnswer);
        cmd.Parameters.AddWithValue("scratchpad", JsonSerializer.Serialize(scratchpad, JsonOptions));
        cmd.Parameters.AddWithValue("tools_used", JsonSerializer.Serialize(toolsUsed, JsonOptions));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadRun(reader);
    }

    public async Task<IReadOnlyList<AgentRunRecord>> ListRecentAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        const string sql = """
            SELECT id, goal, final_answer, scratchpad, tools_used, created_at
            FROM agent_runs
            ORDER BY created_at DESC
            LIMIT @limit;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("limit", limit);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var results = new List<AgentRunRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadRun(reader));
        }

        return results;
    }

    private static AgentRunRecord ReadRun(NpgsqlDataReader reader)
    {
        var scratchpadJson = reader.GetString(3);
        var toolsJson = reader.GetString(4);
        var scratchpad = JsonSerializer.Deserialize<List<string>>(scratchpadJson, JsonOptions) ?? [];
        var toolsUsed = JsonSerializer.Deserialize<List<string>>(toolsJson, JsonOptions) ?? [];

        return new AgentRunRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            scratchpad,
            toolsUsed,
            reader.GetDateTime(5));
    }
}
