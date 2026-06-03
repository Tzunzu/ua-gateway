using UAGateway.Core.Health;

namespace UAGateway.Service;

internal sealed class StartupHealthState
{
    private readonly object _sync = new();
    private StartupHealthSnapshot _current = new(
        StartupHealthStatus.Degraded,
        "Startup not initialized.",
        DateTimeOffset.UtcNow,
        null);

    public StartupHealthSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public StartupHealthSnapshot Update(StartupHealthStatus status, string reason, string? correlationId)
    {
        lock (_sync)
        {
            _current = new StartupHealthSnapshot(status, reason, DateTimeOffset.UtcNow, correlationId);
            return _current;
        }
    }
}
