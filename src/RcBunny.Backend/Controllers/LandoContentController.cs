using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

// Lando's Pages, Page Templates, Content Blocks and Style Guide.
public partial class LandoController
{
    /// <summary>A block's {{variable}} or {{variable|Default text}} (ebunny's ContentBlock::VAR).</summary>
    public static readonly Regex Var = new(@"\{\{\s*([a-z][a-z0-9_]*)\s*(?:\|([^}]*))?\}\}", RegexOptions.IgnoreCase);

    /// <summary>The block's variables in order: name → default text. A block with variables is a module.</summary>
    public static Dictionary<string, string> Variables(string html)
    {
        var vars = new Dictionary<string, string>();
        foreach (Match m in Var.Matches(html)) vars.TryAdd(m.Groups[1].Value, m.Groups[2].Value.Trim());
        return vars;
    }

    private static string Url(Page p) => p.File != null ? (p.File == "index.html" ? "/" : "/" + p.File) : p.Path == "/" ? "/" : "/" + p.Path;

    // ---- Who has a page's edit screen open (ebunny's SiteTraffic::editing) --------------------------------------

    private Dictionary<int, Dictionary<int, (string Name, DateTime At)>> Editing()
    {
        var all = cache.Get<Dictionary<int, Dictionary<int, (string Name, DateTime At)>>>("page-editing") ?? [];
        var cutoff = Now.AddMinutes(-10);
        return all.ToDictionary(p => p.Key, p => p.Value.Where(e => e.Value.At > cutoff).ToDictionary()).Where(p => p.Value.Count > 0).ToDictionary();
    }

    private void MarkEditing(int pageId, Permissions.SignedIn me)
    {
        var all = Editing();
        if (!all.TryGetValue(pageId, out var users)) all[pageId] = users = [];
        users[me.Id] = (me.Name, Now);
        cache.Set("page-editing", all, TimeSpan.FromHours(1));
    }

    // ---- Pages ------------------------------------------------------------------------------------------------

    [HttpGet("pages")]
    public async Task<object> Pages(int? site)
    {
        var sites = await db.Sites.OrderBy(s => s.Id).ToListAsync();
        var s = sites.FirstOrDefault(x => x.Id == site) ?? sites.First();
        var pages = await db.Pages.Where(p => p.SiteId == s.Id).ToListAsync();
        var templates = await db.Templates.ToDictionaryAsync(t => t.Id, t => t.Name);
        var components = await db.PageComponents.GroupBy(c => c.PageId).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var week = await db.SiteVisits.Where(v => !v.Blocked && v.PageId != null && v.CreatedAt >= Now.AddDays(-7)).GroupBy(v => v.PageId!.Value)
            .Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var online = await db.SiteVisits.Where(v => !v.Blocked && v.PageId != null && v.SeenAt >= OnlineSince).GroupBy(v => v.PageId!.Value)
            .Select(g => new { g.Key, n = g.Select(v => v.Visitor).Distinct().Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var areas = (await db.SiteBlocks.Where(b => b.Type == "area").ToListAsync()).Where(b => b.InForce).ToList();
        var editing = Editing();
        return new
        {
            site = s, sites,
            pages = pages.OrderBy(p => p.Path == "/" ? "" : p.Path, StringComparer.Ordinal).Select(p => new
            {
                p.Id, p.Path, p.Title, p.File, p.Redirect, p.Status, template = p.TemplateId is { } t ? templates.GetValueOrDefault(t) : null, components = components.GetValueOrDefault(p.Id),
                views = week.GetValueOrDefault(p.Id), online = online.GetValueOrDefault(p.Id), editing = editing.GetValueOrDefault(p.Id)?.Values.Select(e => e.Name),
                blocked = areas.Any(b => b.MatchesPath(p.Path)), p.UpdatedAt, url = Url(p),
            }),
        };
    }

    [HttpGet("pages/form")]
    public async Task<IActionResult> PageForm(int? id)
    {
        var p = id == null ? new Page { Status = "Published", SiteId = (await db.Sites.OrderBy(s => s.Id).FirstAsync()).Id } : await db.Pages.FindAsync(id);
        if (p == null) return NotFound();
        if (id != null) MarkEditing(p.Id, await Me());
        var copyFrom = p.CopyFromId == null ? null : await db.Pages.FindAsync(p.CopyFromId);
        var source = copyFrom ?? p;
        var blocks = await db.ContentBlocks.Include(b => b.Category).OrderBy(b => b.Name).ToListAsync();
        var components = id == null ? [] : await db.PageComponents.Include(c => c.Block).ThenInclude(b => b!.Category).Where(c => c.PageId == p.Id).OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync();
        return Ok(new
        {
            page = p, url = Url(p),
            content = new { source.PageTitle, source.ContentPrimaryId, source.ContentSecondaryId, source.ContentParentId, source.ContentAuxiliaryId, source.ContentAmpId },
            copyFrom = copyFrom == null ? null : new { copyFrom.Id, copyFrom.Title },
            sites = await db.Sites.OrderBy(s => s.Id).ToListAsync(),
            templates = await db.Templates.OrderBy(t => t.Name).Select(t => new { t.Id, t.Name }).ToListAsync(),
            blocks = blocks.Select(b => new { b.Id, b.Name, category = b.Category?.Name ?? "Uncategorized" }),
            groups = await db.PlanGroups.OrderBy(g => g.Id).Select(g => new { g.Id, g.Name, g.Slug }).ToListAsync(),
            markets = await db.Markets.OrderBy(m => m.Code).Select(m => new { m.Id, name = m.Code }).ToListAsync(),
            zones = Cms.Zones, areas = Cms.Areas, priceGrids = Cms.PriceGrids, ratingFormulas = Cms.RatingFormulas, reps = Cms.Reps,
            components = components.Select(c => new
            {
                c.Id, c.Zone, c.Position, block = new { c.Block!.Id, c.Block.Name, c.Block.Slug, category = c.Block.Category?.Name }, variables = Variables(c.Block.Html),
                vars = string.IsNullOrEmpty(c.Vars) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(c.Vars)!,
            }),
            edits = id == null ? [] : await db.History.Where(h => h.RecordType == "Page" && h.RecordId == p.Id).OrderByDescending(h => h.CreatedAt).Take(20)
                .Select(h => new { h.Id, h.CreatedAt, h.Who, h.Summary }).ToListAsync(),
            canDelete = (await Me()).Permissions.Contains("delete") && p.File == null,
        });
    }

    public record PageInput(int SiteId, string? Title, string? Path, int? TemplateId, string? Status, string? Redirect, string? PromoCode, string? MarketLabel, int? RepId,
        int? CopyFromId, string? CopyMode, string? HtmlTitle, string? MetaDescription, string? MetaKeywords, string? HeadContent, string? AddressBoxTitle, string? Phone,
        bool Canonical, bool NoIndex, bool HardCache, string? PageTitle, int? ContentPrimaryId, int? ContentSecondaryId, int? ContentParentId, int? ContentAuxiliaryId,
        int? ContentAmpId, string? PricegridType, string? PricegridHeader, int? PricegridGroupId, string? RatingFormula, int? MarketId);

    [HttpPost("pages")]
    public Task<IActionResult> AddPage(PageInput input) => SavePage(new Page(), input);

    [HttpPut("pages/{id:int}")]
    public async Task<IActionResult> UpdatePage(int id, PageInput input) => await db.Pages.FindAsync(id) is { } p ? await SavePage(p, input) : NotFound();

    private async Task<IActionResult> SavePage(Page p, PageInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title)) return BadRequest(new { error = "Give the page a title." });
        var path = p.File != null ? p.Path : (input.Path ?? "").Trim().Trim('/');
        if (path == "") path = "/";
        if (path != "/" && !Regex.IsMatch(path, @"^[a-z0-9][a-z0-9_./-]*$")) return BadRequest(new { error = "The path is lower-case letters, numbers, dashes and slashes, e.g. get-to-learnin/new-article." });
        if (Cms.Reserved.Contains(path.Split('/')[0])) return BadRequest(new { error = $"/{path.Split('/')[0]} belongs to the website itself; pick another path." });
        var siteId = p.Id == 0 ? input.SiteId : p.SiteId;
        if (await db.Pages.AnyAsync(x => x.SiteId == siteId && x.Path == path && x.Id != p.Id)) return BadRequest(new { error = "Another page on this site has that path." });
        if (input.Status is not ("Published" or "Draft")) return BadRequest(new { error = "Pick a status." });
        Page? copy = null;
        if (input.CopyFromId is { } from)
        {
            copy = await db.Pages.FindAsync(from);
            if (copy == null || copy.Id == p.Id) return BadRequest(new { error = "No page has that ID to copy from." });
        }
        p.SiteId = siteId; p.Path = path; p.Title = input.Title.Trim(); p.TemplateId = input.TemplateId; p.Status = input.Status; p.Redirect = Blank(input.Redirect);
        p.PromoCode = Blank(input.PromoCode); p.MarketLabel = Blank(input.MarketLabel); p.RepId = input.RepId;
        p.HtmlTitle = Blank(input.HtmlTitle); p.MetaDescription = Blank(input.MetaDescription); p.MetaKeywords = Blank(input.MetaKeywords); p.HeadContent = Blank(input.HeadContent);
        p.AddressBoxTitle = Blank(input.AddressBoxTitle); p.Phone = Blank(input.Phone); p.Canonical = input.Canonical; p.NoIndex = input.NoIndex; p.HardCache = input.HardCache;
        p.PricegridType = Blank(input.PricegridType); p.PricegridHeader = Blank(input.PricegridHeader); p.PricegridGroupId = input.PricegridGroupId;
        p.RatingFormula = Blank(input.RatingFormula); p.MarketId = input.MarketId;
        if (copy != null && input.CopyMode == "once")
        {
            // Copy the contents one time, then the page has its own
            (p.PageTitle, p.ContentPrimaryId, p.ContentSecondaryId, p.ContentParentId, p.ContentAuxiliaryId, p.ContentAmpId) =
                (copy.PageTitle, copy.ContentPrimaryId, copy.ContentSecondaryId, copy.ContentParentId, copy.ContentAuxiliaryId, copy.ContentAmpId);
            p.CopyFromId = null;
        }
        else if (copy != null) p.CopyFromId = copy.Id; // keep in sync: the page shows the other page's content
        else
        {
            p.CopyFromId = null;
            (p.PageTitle, p.ContentPrimaryId, p.ContentSecondaryId, p.ContentParentId, p.ContentAuxiliaryId, p.ContentAmpId) =
                (Blank(input.PageTitle), input.ContentPrimaryId, input.ContentSecondaryId, input.ContentParentId, input.ContentAuxiliaryId, input.ContentAmpId);
        }
        p.UpdatedAt = Now;
        if (p.Id == 0) db.Pages.Add(p);
        await db.SaveChangesAsync();
        return Ok(new { p.Id, message = "Page saved" });
    }

    [HttpDelete("pages/{id:int}")]
    public async Task<IActionResult> DeletePage(int id)
    {
        var p = await db.Pages.FindAsync(id);
        if (p == null) return NotFound();
        if (!(await Me()).Permissions.Contains("delete")) return StatusCode(403, new { error = "Deleting needs the delete right." });
        if (p.File != null) return BadRequest(new { error = "Built-in pages can't be deleted." });
        db.PageComponents.RemoveRange(db.PageComponents.Where(c => c.PageId == id));
        db.Pages.Remove(p);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{Url(p)} deleted" });
    }

    [HttpGet("pages/{id:int}/activity")]
    public async Task<IActionResult> Activity(int id, int page = 1)
    {
        var p = await db.Pages.FindAsync(id);
        if (p == null) return NotFound();
        var since = TodayStart.AddDays(-29);
        var month = await db.SiteVisits.Where(v => v.PageId == id && v.CreatedAt >= since).ToListAsync();
        var views = month.Where(v => !v.Blocked).ToList();
        var onlineVisits = await db.SiteVisits.Where(v => v.PageId == id && !v.Blocked && v.SeenAt >= OnlineSince).OrderByDescending(v => v.CreatedAt).ToListAsync();
        var names = await Names(onlineVisits.Select(v => v.CustomerId).Concat(month.Select(v => v.CustomerId)));
        var componentIds = await db.PageComponents.Where(c => c.PageId == id).Select(c => c.Id).ToListAsync();
        var history = db.History.Where(h => (h.RecordType == "Page" && h.RecordId == id) || (h.RecordType == "PageComponent" && componentIds.Contains(h.RecordId)));
        var site = await db.Sites.FindAsync(p.SiteId);
        return Ok(new
        {
            page = new { p.Id, p.Title, p.Path, p.Status, url = Url(p), site = site?.Name },
            blocks = (await db.SiteBlocks.Where(b => b.Type == "area").ToListAsync()).Where(b => b.MatchesPath(p.Path)).Select(b => new { b.Id, b.Value, b.Reason, inForce = b.InForce }),
            online = onlineVisits.DistinctBy(v => v.Visitor).Select(v => new { v.Ip, v.CreatedAt, customer = v.CustomerId is { } c ? names.GetValueOrDefault(c) : null, v.CustomerId }),
            stats = new
            {
                today = views.Count(v => v.CreatedAt >= TodayStart), week = views.Count(v => v.CreatedAt >= TodayStart.AddDays(-6)), month = views.Count,
                people = views.Select(v => v.Visitor).Distinct().Count(), blocked = month.Count(v => v.Blocked),
            },
            editing = Editing().GetValueOrDefault(id)?.Values.Select(e => e.Name) ?? [],
            days = Enumerable.Range(0, 30).Select(i => DateTime.Today.AddDays(i - 29)).Select(d => new { date = d.ToString("yyyy-MM-dd"), n = views.Count(v => v.CreatedAt.ToLocalTime().Date == d) }),
            referrers = views.Select(v => Host(v.Referrer)).OfType<string>().GroupBy(h => h).OrderByDescending(g => g.Count()).Take(10).ToDictionary(g => g.Key, g => g.Count()),
            total = await history.CountAsync(), pageNo = page,
            history = await history.OrderByDescending(h => h.CreatedAt).Skip((Math.Max(page, 1) - 1) * 10).Take(10).Select(h => new { h.Id, h.CreatedAt, h.Who, h.Summary, h.Changes, h.Action }).ToListAsync(),
            recent = month.OrderByDescending(v => v.CreatedAt).Take(20).Select(v => new { v.Id, v.CreatedAt, v.Ip, v.Blocked, customer = v.CustomerId is { } c ? names.GetValueOrDefault(c) : null }),
        });
    }

    public record ComponentInput(int ContentBlockId, string? Zone);

    [HttpPost("pages/{id:int}/components")]
    public async Task<IActionResult> AddComponent(int id, ComponentInput input)
    {
        var p = await db.Pages.FindAsync(id);
        var b = await db.ContentBlocks.FindAsync(input.ContentBlockId);
        if (p == null || b == null) return BadRequest(new { error = "Pick a content block." });
        var zone = input.Zone != null && Cms.Zones.ContainsKey(input.Zone) ? input.Zone : "main";
        var last = await db.PageComponents.Where(c => c.PageId == id && c.Zone == zone).MaxAsync(c => (int?)c.Position) ?? -1;
        db.PageComponents.Add(new PageComponent { PageId = id, ContentBlockId = b.Id, Zone = zone, Position = last + 1 });
        p.UpdatedAt = Now;
        await db.SaveChangesAsync();
        return Ok(new { message = $"{b.Name} added to {Cms.Zones[zone].Split(" (")[0]}" });
    }

    public record MoveInput(string Dir);

    [HttpPost("page-components/{id:int}/move")]
    public async Task<IActionResult> MoveComponent(int id, MoveInput input)
    {
        var c = await db.PageComponents.FindAsync(id);
        if (c == null) return NotFound();
        var zone = await db.PageComponents.Where(x => x.PageId == c.PageId && x.Zone == c.Zone).OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync();
        var i = zone.FindIndex(x => x.Id == id);
        var j = input.Dir == "up" ? i - 1 : i + 1;
        if (j >= 0 && j < zone.Count) (zone[i], zone[j]) = (zone[j], zone[i]);
        for (var n = 0; n < zone.Count; n++) zone[n].Position = n;
        await db.SaveChangesAsync();
        return Ok();
    }

    public record VarsInput(Dictionary<string, string?> Vars);

    [HttpPut("page-components/{id:int}")]
    public async Task<IActionResult> UpdateComponent(int id, VarsInput input)
    {
        var c = await db.PageComponents.FindAsync(id);
        if (c == null) return NotFound();
        c.Vars = JsonSerializer.Serialize(input.Vars.Where(v => !string.IsNullOrWhiteSpace(v.Value)).ToDictionary(v => v.Key, v => v.Value!.Trim()));
        await db.SaveChangesAsync();
        return Ok(new { message = "Text saved" });
    }

    [HttpDelete("page-components/{id:int}")]
    public async Task<IActionResult> RemoveComponent(int id)
    {
        var c = await db.PageComponents.FindAsync(id);
        if (c == null) return NotFound();
        db.PageComponents.Remove(c);
        await db.SaveChangesAsync();
        return Ok(new { message = "Component removed" });
    }

    /// <summary>Rebuild Sitemap: every published page that search engines may index, saved for the website's /sitemap.xml.</summary>
    [HttpPost("sitemap")]
    public async Task<IActionResult> Sitemap()
    {
        var site = config["Site:Url"]?.TrimEnd('/') ?? "http://localhost:5100";
        var pages = await db.Pages.Where(p => p.Status == "Published" && p.Redirect == null && !p.NoIndex).ToListAsync();
        var urls = pages.Where(p => !p.Path.StartsWith("myaccount") && !p.Path.StartsWith("checkout/") && p.Path != "404")
            .Select(p => $"  <url><loc>{System.Security.SecurityElement.Escape(site + Url(p))}</loc><lastmod>{p.UpdatedAt:yyyy-MM-dd}</lastmod></url>");
        Directory.CreateDirectory(Storage);
        await System.IO.File.WriteAllTextAsync(Path.Combine(Storage, "sitemap.xml"),
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n" + string.Join("\n", urls) + "\n</urlset>\n");
        return Ok(new { message = $"Sitemap rebuilt with {urls.Count()} pages" });
    }

    // ---- Page templates ---------------------------------------------------------------------------------------

    [HttpGet("templates")]
    public async Task<object> Templates()
    {
        var counts = await db.Pages.Where(p => p.TemplateId != null).GroupBy(p => p.TemplateId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        return (await db.Templates.OrderBy(t => t.Name).ToListAsync()).Select(t => new { t.Id, t.Name, t.Description, t.File, pages = counts.GetValueOrDefault(t.Id) });
    }

    [HttpGet("templates/form")]
    public async Task<IActionResult> TemplateForm(int? id)
    {
        var t = id == null ? new Template() : await db.Templates.FindAsync(id);
        if (t == null) return NotFound();
        var pages = id == null ? [] : await db.Pages.Where(p => p.TemplateId == id).OrderBy(p => p.Path).ToListAsync();
        return Ok(new { template = t, pages = pages.Select(p => new { p.Id, p.Title, url = Url(p), p.Status }) });
    }

    public record TemplateInput(string? Name, string? Description, string? File);

    [HttpPost("templates")]
    public Task<IActionResult> AddTemplate(TemplateInput input) => SaveTemplate(new Template(), input);

    [HttpPut("templates/{id:int}")]
    public async Task<IActionResult> UpdateTemplate(int id, TemplateInput input) => await db.Templates.FindAsync(id) is { } t ? await SaveTemplate(t, input) : NotFound();

    private async Task<IActionResult> SaveTemplate(Template t, TemplateInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Description)) return BadRequest(new { error = "Give the template a name and a description." });
        var name = input.Name.Trim();
        if (await db.Templates.AnyAsync(x => x.Name == name && x.Id != t.Id)) return BadRequest(new { error = "Another template has that name." });
        (t.Name, t.Description, t.File) = (name, input.Description.Trim(), Blank(input.File));
        if (t.Id == 0) db.Templates.Add(t);
        await db.SaveChangesAsync();
        return Ok(new { t.Id, message = "Template saved" });
    }

    // ---- Content blocks ---------------------------------------------------------------------------------------

    [HttpGet("blocks")]
    public async Task<object> Blocks(string? category)
    {
        var all = await db.ContentBlocks.Include(b => b.Category).ToListAsync();
        var used = await db.PageComponents.GroupBy(c => c.ContentBlockId).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var sites = await db.Sites.ToDictionaryAsync(s => s.Id, s => s.Name);
        var list = category == null ? all : category == "0" ? all.Where(b => b.CategoryId == null) : all.Where(b => b.CategoryId?.ToString() == category);
        return new
        {
            category,
            categories = (await db.BlockCategories.OrderBy(c => c.Name).ToListAsync()).Select(c => new { c.Id, c.Name, blocks = all.Count(b => b.CategoryId == c.Id) }),
            uncategorized = all.Count(b => b.CategoryId == null),
            blocks = list.OrderBy(b => b.Category?.Name).ThenBy(b => b.Name).Select(b => new
            {
                b.Id, b.Name, category = b.Category?.Name, site = b.SiteId is { } s ? sites.GetValueOrDefault(s) : null, variables = Variables(b.Html).Keys,
                text = Regex.Replace(System.Net.WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(b.Html, @"\[\[[^\]]*\]\]", ""), "<[^>]+>", " ")), @"\s+", " ").Trim(),
                uses = Regex.Match(b.Html, @"\[\[block\|([a-z0-9_-]+)") is { Success: true } m ? m.Groups[1].Value : null,
                components = used.GetValueOrDefault(b.Id), b.UpdatedAt,
            }),
        };
    }

    [HttpGet("blocks/form")]
    public async Task<IActionResult> BlockForm(int? id)
    {
        var b = id == null ? new ContentBlock() : await db.ContentBlocks.FindAsync(id);
        if (b == null) return NotFound();
        var usedOn = id == null ? [] : await db.PageComponents.Include(c => c.Page).Where(c => c.ContentBlockId == id).ToListAsync();
        var sites = await db.Sites.OrderBy(s => s.Id).ToListAsync();
        return Ok(new
        {
            block = b, variables = Variables(b.Html),
            categories = await db.BlockCategories.OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(), sites,
            usedOn = usedOn.Select(c => new { pageId = c.PageId, c.Page!.Title, url = Url(c.Page), site = sites.FirstOrDefault(s => s.Id == c.Page.SiteId)?.Name, zone = Cms.Zones.GetValueOrDefault(c.Zone) }),
            canDelete = (await Me()).Permissions.Contains("delete"),
        });
    }

    public record BlockSave(string? Name, string? Slug, int? CategoryId, int? SiteId, string? Html);

    [HttpPost("blocks")]
    public Task<IActionResult> AddContentBlock(BlockSave input) => SaveContentBlock(new ContentBlock(), input);

    [HttpPut("blocks/{id:int}")]
    public async Task<IActionResult> UpdateContentBlock(int id, BlockSave input) => await db.ContentBlocks.FindAsync(id) is { } b ? await SaveContentBlock(b, input) : NotFound();

    private async Task<IActionResult> SaveContentBlock(ContentBlock b, BlockSave input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Give the block a name." });
        var slug = (input.Slug ?? "").Trim();
        if (!Regex.IsMatch(slug, "^[a-z0-9_-]+$")) return BadRequest(new { error = "The slug is lower-case letters, numbers and dashes." });
        if (await db.ContentBlocks.AnyAsync(x => x.Slug == slug && x.Id != b.Id)) return BadRequest(new { error = "Another block has that slug." });
        (b.Name, b.Slug, b.CategoryId, b.SiteId, b.Html, b.UpdatedById, b.UpdatedAt) = (input.Name.Trim(), slug, input.CategoryId, input.SiteId, input.Html ?? "", (await Me()).Id, Now);
        if (b.Id == 0) db.ContentBlocks.Add(b);
        await db.SaveChangesAsync();
        return Ok(new { b.Id, message = "Content block saved" });
    }

    [HttpDelete("blocks/{id:int}")]
    public async Task<IActionResult> DeleteContentBlock(int id)
    {
        var b = await db.ContentBlocks.FindAsync(id);
        if (b == null) return NotFound();
        if (!(await Me()).Permissions.Contains("delete")) return StatusCode(403, new { error = "Deleting needs the delete right." });
        db.PageComponents.RemoveRange(db.PageComponents.Where(c => c.ContentBlockId == id));
        foreach (var p in await db.Pages.Where(p => p.ContentPrimaryId == id || p.ContentSecondaryId == id || p.ContentParentId == id || p.ContentAuxiliaryId == id || p.ContentAmpId == id).ToListAsync())
        {
            if (p.ContentPrimaryId == id) p.ContentPrimaryId = null;
            if (p.ContentSecondaryId == id) p.ContentSecondaryId = null;
            if (p.ContentParentId == id) p.ContentParentId = null;
            if (p.ContentAuxiliaryId == id) p.ContentAuxiliaryId = null;
            if (p.ContentAmpId == id) p.ContentAmpId = null;
        }
        db.ContentBlocks.Remove(b);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{b.Name} deleted" });
    }

    public record CategoryInput(string? Name);

    [HttpPost("block-categories")]
    public async Task<IActionResult> AddCategory(CategoryInput input)
    {
        var name = (input.Name ?? "").Trim();
        if (name == "") return BadRequest(new { error = "Give the category a name." });
        if (await db.BlockCategories.AnyAsync(c => c.Name == name)) return BadRequest(new { error = "That category already exists." });
        db.BlockCategories.Add(new BlockCategory { Name = name });
        await db.SaveChangesAsync();
        return Ok(new { message = $"{name} added" });
    }
}
