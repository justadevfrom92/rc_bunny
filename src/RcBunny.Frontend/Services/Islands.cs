using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RcBunny.Frontend.Services;

/// <summary>
/// Finds the built island loader (hashed file name) in Vite's manifest once at start-up.
/// In Development with the Vite dev server running, set Website:IslandsDevServer to load straight from it.
/// </summary>
public sealed class IslandAssets
{
    public string? Script { get; }
    public IReadOnlyList<string> Css { get; }
    public string? DevServer { get; }

    public IslandAssets(IWebHostEnvironment env, IConfiguration config)
    {
        DevServer = env.IsDevelopment() ? config["Website:IslandsDevServer"] : null;
        var manifest = Path.Combine(env.WebRootPath, "islands", ".vite", "manifest.json");
        if (!File.Exists(manifest))
        {
            Css = [];
            return;
        }
        var json = JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
        var entry = json["src/main.tsx"]!;
        Script = "/islands/" + (string)entry["file"]!;
        Css = entry["css"]?.AsArray().Select(c => "/islands/" + (string)c!).ToList() ?? [];
    }
}

/// <summary>
/// &lt;island name="PlanBrowser" props="@data"&gt;…server-rendered HTML…&lt;/island&gt;
///
/// The children are full server-rendered HTML, so the page is complete and readable before any JavaScript runs.
/// The props go inline as JSON, so when the island's code arrives it renders immediately with no API call and no
/// spinner, taking over the same markup to add interactivity.
/// </summary>
[HtmlTargetElement("island")]
public sealed class IslandTagHelper : TagHelper
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Default,   // escapes <, >, & so the JSON can't break out of the script tag
    };

    public string Name { get; set; } = "";
    public object? Props { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var children = await output.GetChildContentAsync();
        output.TagName = "div";
        output.Attributes.SetAttribute("data-island", Name);
        output.Content.SetHtmlContent(children);
        var json = JsonSerializer.Serialize(Props ?? new { }, Json);
        output.PostElement.SetHtmlContent(new HtmlString($"<script type=\"application/json\" data-island-props=\"{HtmlEncoder.Default.Encode(Name)}\">{json}</script>"));
    }
}
