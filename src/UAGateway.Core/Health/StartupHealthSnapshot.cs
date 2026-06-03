namespace UAGateway.Core.Health;

public enum StartupHealthStatus
{
    Healthy,
    Degraded,
    Faulted,
}

public sealed record StartupHealthSnapshot(
    StartupHealthStatus Status,
    string Reason,
    DateTimeOffset UpdatedUtc,
    string? CorrelationId = null);
