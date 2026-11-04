namespace RcBunny.Data;

// ---- Sheriff --------------------------------------------------------------------------------------------------

/// <summary>The timeline: sign-ins, access changes, heartbeat switches and job runs.</summary>
public class AuditEntry
{
    public int Id { get; set; }
    public string Who { get; set; } = "";
    /// <summary>sign-in, access, heartbeat, job or market.</summary>
    public string Category { get; set; } = "";
    public string What { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A switch on the heartbeat page, e.g. "website": when Down, the website shows a maintenance page.</summary>
public class HeartbeatControl
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Down { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public class HealthCheck
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Ok { get; set; }
    public string Detail { get; set; } = "";
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class JobRun
{
    public int Id { get; set; }
    public string Job { get; set; } = "";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    /// <summary>Who pressed "Run now" (empty when the schedule ran it).</summary>
    public string? RunBy { get; set; }
}
