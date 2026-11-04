using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Services;

/// <summary>Adding and removing products on an account. AutoPay, Paperless Billing and Peak Perks also switch a yes/no field.</summary>
public static class Products
{
    public static async Task<bool> AddAsync(AppDbContext db, Customer c, string product, string by, string? details = null)
    {
        if (await db.CustomerProducts.AnyAsync(p => p.CustomerId == c.Id && p.Product == product && p.RemovedAt == null)) return false;
        db.CustomerProducts.Add(new CustomerProduct { CustomerId = c.Id, Product = product, Author = by, Details = details });
        SetField(c, product, true);
        return true;
    }

    public static async Task<bool> RemoveAsync(AppDbContext db, Customer c, string product)
    {
        var rows = await db.CustomerProducts.Where(p => p.CustomerId == c.Id && p.Product == product && p.RemovedAt == null).ToListAsync();
        foreach (var row in rows) row.RemovedAt = DateTime.UtcNow;
        SetField(c, product, false);
        return rows.Count > 0;
    }

    private static void SetField(Customer c, string product, bool on)
    {
        if (product == "AutoPay") c.Autopay = on;
        if (product == "Paperless Billing") c.Paperless = on;
        if (product == "Peak Perks") c.PeakPerks = on;
    }
}
