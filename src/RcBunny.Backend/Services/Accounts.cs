using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Services;

public static class Accounts
{
    /// <summary>Deletes an account and everything on it (Status - Delete Order). Tickets and calls keep their text but lose the link.</summary>
    public static async Task DeleteAsync(AppDbContext db, Customer c)
    {
        var id = c.Id;
        await db.Notes.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.Bills.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.Payments.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.PaymentMethods.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.CustomerFlags.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.CustomerProducts.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.PlanTerms.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.ServiceAddresses.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.QueueLogs.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.CustomerFiles.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.ContactLogs.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.ApiLogs.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.Bookmarks.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.WorkItems.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.ErcotTransactions.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.ShopOrders.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.StarEntries.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.LedgerEntries.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.Refunds.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.DrawingEntries.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.DrawingWinners.Where(x => x.CustomerId == id).ExecuteDeleteAsync();
        await db.PhoneCalls.Where(x => x.CustomerId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, (int?)null));
        await db.SupportTickets.Where(x => x.CustomerId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, (int?)null));
        await db.CrmLeads.Where(x => x.CustomerId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, (int?)null));
        db.Customers.Remove(c);
    }
}
