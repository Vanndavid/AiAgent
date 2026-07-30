using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace JobAssistant.Api.Endpoints;

public static class AgentEndpoints
{
    public sealed record AgentRunRequest(string Goal, int? MaxSteps = null);

    public sealed record AgentRunResponse(
        [property: JsonPropertyName("goal")] string Goal,
        [property: JsonPropertyName("final_answer")] string FinalAnswer,
        [property: JsonPropertyName("scratchpad")] IReadOnlyList<string> Scratchpad);

    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app, string agentBaseUrl)
    {
        app.MapPost("/api/agent/run", async (AgentRunRequest body, IHttpClientFactory httpFactory, CancellationToken ct) =>
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

                    var result = await resp.Content.ReadFromJsonAsync<AgentRunResponse>(cancellationToken: ct);
                    return result is null
                        ? Results.Json(new { error = "Empty agent response." }, statusCode: 502)
                        : Results.Ok(result);
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

        return app;
    }
}
