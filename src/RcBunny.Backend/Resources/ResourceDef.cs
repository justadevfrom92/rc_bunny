using System.Text.Json.Nodes;
using RcBunny.Backend.Access;
using RcBunny.Data;

namespace RcBunny.Backend.Resources;

/// <summary>Everything a custom action gets: the row, the request body, who's asking, the database.</summary>
public sealed record ActionContext(AppDbContext Db, object Row, JsonObject Input, AccessSnapshot Me, IServiceProvider Services);

/// <summary>A button on a record (Approve, Send, Take Over…). Returns a message for the user, or throws ActionError.</summary>
public sealed class ActionDef
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    /// <summary>Extra right needed on top of the app (e.g. "refunds").</summary>
    public string? Perm { get; init; }
    public string? Confirm { get; init; }
    /// <summary>Inputs asked for before running: name → label (a single-line text box). "note:" prefix = multi-line.</summary>
    public Dictionary<string, string> Inputs { get; init; } = [];
    /// <summary>Whether the button shows for this row.</summary>
    public Func<object, bool>? When { get; init; }
    public string Tone { get; init; } = "";
    public required Func<ActionContext, Task<string>> Run { get; init; }
}

public sealed class ActionError(string message) : Exception(message);

/// <summary>
/// One admin screen over one table: what the list shows, what you can search and filter by, which fields can be
/// edited, and the record's buttons. The screens (React) and the API (ResourceController) are both driven from this,
/// so most of ebunny's list/edit pages are one definition each (see ResourceRegistry).
/// </summary>
public sealed class ResourceDef
{
    public required string Key { get; init; }
    /// <summary>App key; also the permission needed.</summary>
    public required string App { get; init; }
    public required Type Entity { get; init; }
    public required string Title { get; init; }
    public string? Singular { get; init; }
    public string? Description { get; init; }
    /// <summary>Property names shown as list columns (PascalCase). Empty = the first few fields.</summary>
    public string[] List { get; init; } = [];
    public string[] Search { get; init; } = [];
    public string[] Filters { get; init; } = [];
    /// <summary>"-CreatedAt" = newest first.</summary>
    public string Sort { get; init; } = "-Id";
    public bool Create { get; init; } = true;
    public bool Edit { get; init; } = true;
    public bool Delete { get; init; }
    /// <summary>Right needed to create/edit/delete, beyond the app (null = the app is enough).</summary>
    public string? WritePerm { get; init; }
    /// <summary>Fields shown but never editable here.</summary>
    public string[] ReadOnly { get; init; } = [];
    /// <summary>Fields left out of forms and details entirely.</summary>
    public string[] Hidden { get; init; } = [];
    /// <summary>Fixed choices for a field: property → options.</summary>
    public Dictionary<string, string[]> Options { get; init; } = [];
    /// <summary>Fixed values on every row of this screen (e.g. Kind = "header"); also set on create.</summary>
    public Dictionary<string, object> Fixed { get; init; } = [];
    public List<ActionDef> Actions { get; init; } = [];
    /// <summary>Related lists on the record page: (title, resource key, foreign key property on that resource).</summary>
    public (string Title, string Resource, string ForeignKey)[] Children { get; init; } = [];
    /// <summary>Runs before a create/update is saved (validation, defaults). Throw ActionError to refuse.</summary>
    public Func<ActionContext, Task>? BeforeSave { get; init; }

    public string Name => Singular ?? Title.TrimEnd('s');
}
