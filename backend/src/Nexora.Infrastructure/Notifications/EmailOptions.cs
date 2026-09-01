namespace Nexora.Infrastructure.Notifications;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string Provider { get; init; } = "Fake";
    public string FromName { get; init; } = "Nexora";
    public string FromAddress { get; init; } = string.Empty;
    public string? ReplyTo { get; init; }
    public string? ApplicationUrl { get; init; }
    public string ResendApiKey { get; init; } = string.Empty;
    public bool WorkerEnabled { get; init; } = true;
    public int PollIntervalSeconds { get; init; } = 10;
    public int ProcessingLeaseMinutes { get; init; } = 10;
    public int MaxAttempts { get; init; } = 5;
    public int[] RetryDelaysSeconds { get; init; } = [60, 300, 900, 3600];
}
