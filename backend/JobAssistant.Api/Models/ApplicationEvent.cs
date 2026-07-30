namespace JobAssistant.Api.Models;

public record ApplicationEvent(
    Guid Id,
    Guid ApplicationId,
    string? FromStatus,
    string ToStatus,
    DateTime CreatedAt);

public record JobApplicationDetail(
    Guid Id,
    string Company,
    string Role,
    string Status,
    DateTime? AppliedAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ApplicationEvent> Events);

public static class ApplicationStatusTransitions
{
    private static readonly Dictionary<string, HashSet<string>> AllowedNext = new(StringComparer.Ordinal)
    {
        [ApplicationStatuses.Saved] = new(StringComparer.Ordinal) { ApplicationStatuses.Applied },
        [ApplicationStatuses.Applied] = new(StringComparer.Ordinal) { ApplicationStatuses.Interview },
        [ApplicationStatuses.Interview] = new(StringComparer.Ordinal)
        {
            ApplicationStatuses.Offer,
            ApplicationStatuses.Rejected,
        },
        [ApplicationStatuses.Offer] = new(StringComparer.Ordinal),
        [ApplicationStatuses.Rejected] = new(StringComparer.Ordinal),
    };

    public static bool CanTransition(string fromStatus, string toStatus) =>
        AllowedNext.TryGetValue(fromStatus, out var allowed) && allowed.Contains(toStatus);

    public static IReadOnlyList<string> GetAllowedNext(string fromStatus) =>
        AllowedNext.TryGetValue(fromStatus, out var allowed)
            ? allowed.ToList()
            : Array.Empty<string>();
}

public sealed class InvalidStatusTransitionException : Exception
{
    public InvalidStatusTransitionException(string fromStatus, string toStatus)
        : base($"Invalid status transition from '{fromStatus}' to '{toStatus}'.")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }

    public string FromStatus { get; }
    public string ToStatus { get; }
}
