using JobAssistant.Api.Data;
using JobAssistant.Api.Models;

namespace JobAssistant.Api.Endpoints;

public static class ApplicationsEndpoints
{
    public static RouteGroupBuilder MapApplicationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/applications").WithTags("Applications");

        group.MapGet("/", async (ApplicationRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.ListAsync(ct)))
            .WithName("ListApplications")
            .WithOpenApi();

        group.MapGet("/{id:guid}", async (Guid id, ApplicationRepository repo, CancellationToken ct) =>
        {
            var application = await repo.GetByIdAsync(id, ct);
            return application is null ? Results.NotFound() : Results.Ok(application);
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
        })
            .WithName("UpdateApplication")
            .WithOpenApi();

        group.MapDelete("/{id:guid}", async (Guid id, ApplicationRepository repo, CancellationToken ct) =>
        {
            var deleted = await repo.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
            .WithName("DeleteApplication")
            .WithOpenApi();

        return group;
    }
}
