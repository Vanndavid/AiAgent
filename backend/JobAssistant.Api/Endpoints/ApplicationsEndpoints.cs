using JobAssistant.Api.Data;
using JobAssistant.Api.Models;
using Npgsql;

namespace JobAssistant.Api.Endpoints;

public static class ApplicationsEndpoints
{
    public static RouteGroupBuilder MapApplicationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/applications").WithTags("Applications");

        group.MapGet("/", async (
            ApplicationRepository repo,
            string? status,
            string? search,
            string? sortBy,
            string? sortDir,
            CancellationToken ct) =>
        {
            try
            {
                var query = ApplicationListQueryParser.Parse(status, search, sortBy, sortDir);
                return Results.Ok(await repo.ListAsync(query, ct));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (NpgsqlException ex)
            {
                return Results.Json(new { error = "Database unavailable.", detail = ex.Message }, statusCode: 503);
            }
        })
            .WithName("ListApplications")
            .WithOpenApi();

        group.MapGet("/{id:guid}", async (Guid id, ApplicationRepository repo, CancellationToken ct) =>
        {
            try
            {
                var application = await repo.GetByIdAsync(id, ct);
                return application is null ? Results.NotFound() : Results.Ok(application);
            }
            catch (NpgsqlException ex)
            {
                return Results.Json(new { error = "Database unavailable.", detail = ex.Message }, statusCode: 503);
            }
        })
            .WithName("GetApplication")
            .WithOpenApi();

        group.MapPost("/", async (CreateApplicationRequest request, ApplicationRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Company) || string.IsNullOrWhiteSpace(request.Role))
            {
                return Results.BadRequest(new { error = "Company and role are required." });
            }

            try
            {
                var created = await repo.CreateAsync(request, ct);
                return Results.Created($"/api/applications/{created.Id}", created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (NpgsqlException ex)
            {
                return Results.Json(new { error = "Database unavailable.", detail = ex.Message }, statusCode: 503);
            }
        })
            .WithName("CreateApplication")
            .WithOpenApi();

        group.MapPatch("/{id:guid}", async (Guid id, UpdateApplicationRequest request, ApplicationRepository repo, CancellationToken ct) =>
        {
            if (request.Company is not null && string.IsNullOrWhiteSpace(request.Company))
            {
                return Results.BadRequest(new { error = "Company cannot be empty." });
            }

            if (request.Role is not null && string.IsNullOrWhiteSpace(request.Role))
            {
                return Results.BadRequest(new { error = "Role cannot be empty." });
            }

            try
            {
                var updated = await repo.UpdateAsync(id, request, ct);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (NpgsqlException ex)
            {
                return Results.Json(new { error = "Database unavailable.", detail = ex.Message }, statusCode: 503);
            }
        })
            .WithName("UpdateApplication")
            .WithOpenApi();

        group.MapDelete("/{id:guid}", async (Guid id, ApplicationRepository repo, CancellationToken ct) =>
        {
            try
            {
                var deleted = await repo.DeleteAsync(id, ct);
                return deleted ? Results.NoContent() : Results.NotFound();
            }
            catch (NpgsqlException ex)
            {
                return Results.Json(new { error = "Database unavailable.", detail = ex.Message }, statusCode: 503);
            }
        })
            .WithName("DeleteApplication")
            .WithOpenApi();

        return group;
    }
}
