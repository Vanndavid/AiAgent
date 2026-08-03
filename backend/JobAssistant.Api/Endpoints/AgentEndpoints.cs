using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobAssistant.Api.Data;
using JobAssistant.Api.Models;
using Npgsql;

namespace JobAssistant.Api.Endpoints;

public static class AgentEndpoints
{
    public sealed record AgentRunRequest(string Goal, int? MaxSteps = null);

    public sealed record AgentRunResponse(
        [property: JsonPropertyName("id")] Guid? Id,
        [property: JsonPropertyName("goal")] string Goal,
        [property: JsonPropertyName("final_answer")] string FinalAnswer,
        [property: JsonPropertyName("scratchpad")] IReadOnlyList<string> Scratchpad,
        [property: JsonPropertyName("tools_used")] IReadOnlyList<string>? ToolsUsed = null,
        [property: JsonPropertyName("created_at")] DateTime? CreatedAt = null);

    private sealed record AgentServiceResponse(
        [property: JsonPropertyName("goal")] string Goal,
        [property: JsonPropertyName("final_answer")] string FinalAnswer,
        [property: JsonPropertyName("scratchpad")] IReadOnlyList<string> Scratchpad,
        [property: JsonPropertyName("tools_used")] IReadOnlyList<string>? ToolsUsed = null);

    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app, string agentBaseUrl)
    {
        app.MapPost("/api/agent/run", async (
                AgentRunRequest body,
                IHttpClientFactory httpFactory,
                AgentRunRepository runs,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(body.Goal))
                    return Results.BadRequest(new { error = "Goal is required." });

                var client = httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                var payload = new
                {
                    goal = body.Goal.Trim(),
                    max_steps = body.MaxSteps is > 0 and <= 20 ? body.MaxSteps : 6,
                };

                try
                {
                    using var resp = await client.PostAsJsonAsync(
                        $"{agentBaseUrl.TrimEnd('/')}/agent/run",
                        payload,
                        ct);

                    if (!resp.IsSuccessStatusCode)
                    {
                        var detail = await resp.Content.ReadAsStringAsync(ct);
                        return Results.Json(new { error = "AI agent request failed.", detail }, statusCode: (int)resp.StatusCode);
                    }

                    var result = await resp.Content.ReadFromJsonAsync<AgentServiceResponse>(cancellationToken: ct);
                    if (result is null)
                    {
                        return Results.Json(new { error = "Empty agent response." }, statusCode: 502);
                    }

                    var tools = result.ToolsUsed ?? Array.Empty<string>();
                    AgentRunRecord? saved = null;
                    try
                    {
                        saved = await runs.CreateAsync(
                            result.Goal,
                            result.FinalAnswer,
                            result.Scratchpad,
                            tools,
                            ct);
                    }
                    catch (NpgsqlException)
                    {
                        // Agent succeeded; history is best-effort when Postgres is down.
                    }

                    return Results.Ok(new AgentRunResponse(
                        saved?.Id,
                        result.Goal,
                        result.FinalAnswer,
                        result.Scratchpad,
                        tools,
                        saved?.CreatedAt));
                }
                catch (Exception ex)
                {
                    return Results.Json(
                        new { error = "AI agent unreachable.", detail = ex.Message },
                        statusCode: 503);
                }
            })
            .WithName("RunAgent")
            .WithOpenApi();

        app.MapGet("/api/agent/runs", async (AgentRunRepository runs, int? limit, CancellationToken ct) =>
            {
                try
                {
                    var items = await runs.ListRecentAsync(limit ?? 20, ct);
                    return Results.Ok(items.Select(r => new AgentRunResponse(
                        r.Id,
                        r.Goal,
                        r.FinalAnswer,
                        r.Scratchpad,
                        r.ToolsUsed,
                        r.CreatedAt)));
                }
                catch (NpgsqlException ex)
                {
                    return Results.Json(
                        new { error = "Database unavailable.", detail = ex.Message },
                        statusCode: 503);
                }
            })
            .WithName("ListAgentRuns")
            .WithOpenApi();

        return app;
    }
}
