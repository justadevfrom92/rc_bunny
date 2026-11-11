using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Data;
using RcBunny.Frontend.Services;
using CmsPage = RcBunny.Data.Page;

namespace RcBunny.Frontend.Pages;

/// <summary>
/// A website page built in Lando (ebunny's SitePageController): any path that isn't one of the website's own pages.
/// Redirects go where they point; drafts and unknown paths are "page not found".
/// </summary>
public class LandoPageModel(AppDbContext db, SiteContent content, IMemoryCache cache) : PageModel
{
    public CmsPage Item { get; private set; } = null!;
    public Website Site { get; private set; } = null!;
    public string Heading { get; private set; } = "";
    public Dictionary<string, string> Content { get; private set; } = [];
    public Dictionary<string, string> Zones { get; private set; } = [];
    public SiteContent.Grid? Grid { get; private set; }
    public bool HasAmp { get; private set; }
    public Market? Market { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? path)
    {
        var page = await FindAsync(path);
        if (page == null) return NotFound();
        if (page.Redirect != null) return RedirectPermanent(page.Redirect);
        Item = page;
        Site = (await db.Sites.FindAsync(page.SiteId))!;
        Market = page.MarketId is { } m ? await db.Markets.FindAsync(m) : null;
        var source = page.CopyFromId is { } from && await db.Pages.FindAsync(from) is { } copy ? copy : page;
        Heading = source.PageTitle ?? page.Title;
        HasAmp = source.ContentAmpId != null;
        content.AddressBoxTitle = page.AddressBoxTitle;

        // Hard Cache: keep the finished content; it refreshes when the page or any content block is saved (or after an hour)
        var blocksChanged = await db.ContentBlocks.MaxAsync(b => (DateTime?)b.UpdatedAt);
        var key = $"page:{page.Id}:{page.UpdatedAt.Ticks}:{source.UpdatedAt.Ticks}:{blocksChanged?.Ticks}";
        if (page.HardCache && cache.TryGetValue(key, out (Dictionary<string, string>, Dictionary<string, string>) saved))
        {
            (Content, Zones) = saved;
            Response.Headers["X-Page-Cache"] = "hard";
        }
        else
        {
            foreach (var (area, id) in new[] { ("primary", source.ContentPrimaryId), ("secondary", source.ContentSecondaryId), ("parent", source.ContentParentId), ("auxiliary", source.ContentAuxiliaryId) })
                Content[area] = id is { } b && await db.ContentBlocks.FindAsync(b) is { } block ? await content.ExpandAsync(block.Html, Site) : "";
            Zones = await content.ZonesAsync(page);
            if (page.HardCache) cache.Set(key, (Content, Zones), TimeSpan.FromHours(1));
        }
        Grid = await content.PriceGridAsync(page);
        return Page();
    }

    private async Task<CmsPage?> FindAsync(string? raw)
    {
        var path = (raw ?? "").Trim('/');
        if (path == "") path = "/";
        if (Cms.Reserved.Contains(path.Split('/')[0])) return null;
        var site = await db.Sites.Where(s => s.Status == "Active").OrderBy(s => s.Id).FirstOrDefaultAsync();
        return site == null ? null : await db.Pages.FirstOrDefaultAsync(p => p.SiteId == site.Id && p.Path == path && p.Status == "Published");
    }

    public string Phone => Item.Phone ?? Site.Phone ?? "";
}
