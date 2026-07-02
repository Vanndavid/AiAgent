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
