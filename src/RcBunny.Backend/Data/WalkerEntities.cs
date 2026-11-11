namespace RcBunny.Data;

// ---- Walker: reports, saved reports, export columns and files (ebunny's report_runs, saved_reports, report_columns
//      and report_uploads tables) ----------------------------------------------------------------------------------

/// <summary>One run of a report: queued → running → done or failed. The file it saved, and its first rows to show.</summary>
public class ReportRun
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    /// <summary>Where it was started (walker).</summary>
    public string App { get; set; } = "walker";
    /// <summary>The report's key (orders, notes, payments…).</summary>
    public string Report { get; set; } = "";
    public string? Title { get; set; }
    public string? Model { get; set; }
    /// <summary>JSON: the filters it ran with.</summary>
    public string? Params { get; set; }
    /// <summary>xlsx, csv, txt or json.</summary>
    public string Format { get; set; } = "xlsx";
    public bool Summary { get; set; }
    public int Rows { get; set; }
    /// <summary>queued, running, done or failed.</summary>
    public string Status { get; set; } = "queued";
    public string? Error { get; set; }
    /// <summary>Where the file is, under the storage folder (reports/…).</summary>
    public string? File { get; set; }
    public long? FileSize { get; set; }
    /// <summary>JSON: {header, rows} — the first 25 rows.</summary>
    public string? Preview { get; set; }
    public int? DurationMs { get; set; }
    public int? RerunOf { get; set; }
    public int? SavedReportId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    /// <summary>A run still queued or running after this long shows as "Did not finish".</summary>
    public const int TimeoutMinutes = 60;

    /// <summary>queued, running, done, failed, or stalled (still running past the timeout).</summary>
    public string State => Status is "queued" or "running" && (StartedAt ?? CreatedAt) < DateTime.UtcNow.AddMinutes(-TimeoutMinutes) ? "stalled" : Status;
}

/// <summary>A report bookmarked with its settings, to run again in one click. Shared ones show for everyone with Walker.</summary>
public class SavedReport
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = "";
    public string Report { get; set; } = "";
    public string? Params { get; set; }
    public string Format { get; set; } = "xlsx";
    public bool Summary { get; set; }
    public bool Shared { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }
}

/// <summary>A report's export columns: which keys on each record go in the file, with their titles, in order.</summary>
public class ReportColumn
{
    public int Id { get; set; }
    public string Report { get; set; } = "";
    /// <summary>JSON: [{key, title}].</summary>
    public string Columns { get; set; } = "[]";
    public int? UserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A file in Walker → Files (ETL files, legal documents, customer templates, finance workbooks…).</summary>
public class ReportUpload
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "Other";
    public string? Description { get; set; }
    /// <summary>Where it is, under the storage folder (report-uploads/…).</summary>
    public string Path { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public long Size { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
