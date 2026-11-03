using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Frontend.Pages.MyAccount;

/// <summary>My Account → Rewards: the star balance, what it can buy, and the history. Staff manage this in Bounty.</summary>
public class RewardsModel(AppDbContext db) : PageModel
{
    public Customer Customer { get; private set; } = null!;
    public List<RewardOffer> Offers { get; private set; } = [];
    public List<StarEntry> History { get; private set; } = [];
    public bool InDrawing { get; private set; }
    public string? Error { get; private set; }

    private int CustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> OnGetAsync()
    {
        Customer = await db.Customers.FirstAsync(c => c.Id == CustomerId);
        Offers = await db.RewardOffers.Where(o => o.Active).OrderBy(o => o.Position).ThenBy(o => o.Stars).ToListAsync();
        History = await db.StarEntries.Where(e => e.CustomerId == CustomerId).OrderByDescending(e => e.CreatedAt).Take(50).ToListAsync();
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        InDrawing = await db.DrawingEntries.AnyAsync(e => e.CustomerId == CustomerId && e.Month == month);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int offerId)
    {
        var offer = await db.RewardOffers.FindAsync(offerId);
        Error = await Rewards.RedeemAsync(db, CustomerId, offerId, "My Account");
        if (Error != null) return await OnGetAsync();
        TempData["redeemed"] = offer!.Effect switch
        {
            "drawing" => "You're in this month's drawing. Good luck!",
            "credit" => $"Done! {offer.Name} is on your account.",
            _ => $"Done! We'll send your {offer.Name} soon.",
        };
        return Redirect("/myaccount/rewards");
    }
}
