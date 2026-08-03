namespace JobAssistant.Api.Models;

public record JobApplication(
    Guid Id,
    string Company,
    string Role,
    string Status,
    DateTime? AppliedAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateApplicationRequest(
    string Company,
    string Role,
    string? Status,
    DateTime? AppliedAt,
    string? Notes);

public record UpdateApplicationRequest(
    string? Company,
    string? Role,
    string? Status,
    DateTime? AppliedAt,
    string? Notes);

public static class ApplicationStatuses
{
    public const string Saved = "saved";
    public const string Applied = "applied";
    public const string Interview = "interview";
    public const string Offer = "offer";
    public const string Rejected = "rejected";

    public static readonly HashSet<string> All = new(StringComparer.Ordinal)
    {
        Saved, Applied, Interview, Offer, Rejected,
    };
}

public sealed record ApplicationListQuery(
    string? Status = null,
    string? Search = null,
    string SortBy = "applied",
    string SortDir = "desc");

public static class ApplicationListQueryParser
{
    private static readonly HashSet<string> SortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "applied", "company", "role", "status", "created", "updated",
    };

    public static ApplicationListQuery Parse(string? status, string? search, string? sortBy, string? sortDir)
    {
        string? normalizedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            normalizedStatus = status.Trim().ToLowerInvariant();
            if (!ApplicationStatuses.All.Contains(normalizedStatus))
            {
                throw new ArgumentException(
                    $"Invalid status '{status}'. Allowed: {string.Join(", ", ApplicationStatuses.All)}.");
            }
        }

        var field = string.IsNullOrWhiteSpace(sortBy) ? "applied" : sortBy.Trim().ToLowerInvariant();
        if (!SortFields.Contains(field))
        {
            throw new ArgumentException(
                $"Invalid sortBy '{sortBy}'. Allowed: {string.Join(", ", SortFields)}.");
        }

        var dir = string.IsNullOrWhiteSpace(sortDir) ? "desc" : sortDir.Trim().ToLowerInvariant();
        if (dir is not ("asc" or "desc"))
        {
            throw new ArgumentException("Invalid sortDir. Allowed: asc, desc.");
        }

        return new ApplicationListQuery(
            normalizedStatus,
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            field,
            dir);
    }
}

public sealed record AgentRunRecord(
    Guid Id,
    string Goal,
    string FinalAnswer,
    IReadOnlyList<string> Scratchpad,
    IReadOnlyList<string> ToolsUsed,
    DateTime CreatedAt);
