using Microsoft.EntityFrameworkCore;

namespace RcBunny.Data;

/// <summary>Spending stars. Used by My Account (the website) and by staff in Bounty.</summary>
public static class Rewards
{
    /// <summary>Redeems an offer for a customer. Returns null when it worked, or what went wrong.</summary>
    public static async Task<string?> RedeemAsync(AppDbContext db, int customerId, int offerId, string by)
    {
        var customer = await db.Customers.FindAsync(customerId);
        var offer = await db.RewardOffers.FirstOrDefaultAsync(o => o.Id == offerId && o.Active);
        if (customer is null || offer is null) return "That offer isn't available.";
        if (customer.Stars < offer.Stars) return $"{offer.Name} needs {offer.Stars:N0} stars. You have {customer.Stars:N0}.";

        var month = DateTime.UtcNow.ToString("yyyy-MM");
        if (offer.Effect == "drawing")
        {
            if (await db.DrawingEntries.AnyAsync(e => e.CustomerId == customerId && e.Month == month)) return "You're already in this month's drawing.";
            db.DrawingEntries.Add(new DrawingEntry { CustomerId = customerId, Month = month });
        }
        if (offer.Effect == "credit" && decimal.TryParse(offer.Value, out var credit))
        {
            customer.Balance -= credit;
            db.Notes.Add(new Note { CustomerId = customerId, Author = by, Body = $"Redeemed {offer.Stars:N0} stars for a {credit:C} bill credit." });
        }

        customer.Stars -= offer.Stars;
        db.StarEntries.Add(new StarEntry
        {
            CustomerId = customerId, Stars = -offer.Stars, Reason = $"Redeemed: {offer.Name}", OfferId = offer.Id, By = by,
            // Gift cards and products are sent by staff (Bounty → Redemptions); credits and drawing entries happen right away.
            Fulfillment = offer.Effect is "gift" or "product" ? "pending" : null,
        });
        await db.SaveChangesAsync();
        return null;
    }
}
