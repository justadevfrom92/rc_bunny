using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Services;

/// <summary>
/// Turns a PDF template (Rodeo → PDFs) into a PDF for one account (ebunny's PdfTemplateRenderer). The body is plain text
/// with a little markup, one block per line:
///   # Heading            ## Smaller heading         - A bullet point
///   | Label | Value |    a shaded row of two cells  ---  a divider line
///   **A bold line**      a blank line starts a new paragraph; anything else is wrapped text (** inside a line is dropped)
/// Fill-ins like {{first_name}} or {{balance}} are replaced with the account's details (see Fields).
/// Long documents run onto more pages; every page gets the footer and "Page X of Y".
/// </summary>
public static class PdfTemplates
{
    public static readonly Dictionary<string, string> Fields = new()
    {
        ["first_name"] = "First name", ["last_name"] = "Last name", ["name"] = "Full name", ["account"] = "Account number",
        ["address"] = "Service address", ["city"] = "City", ["zip"] = "ZIP code", ["esiid"] = "ESIID", ["plan"] = "Plan name",
        ["balance"] = "Balance due", ["due_date"] = "Latest bill due date", ["last_bill"] = "Latest bill amount", ["today"] = "Today's date",
        ["brand"] = "Company name", ["phone"] = "Customer service phone",
    };

    /// <summary>What the renderer needs: the wording and the header (title, subtitle, color).</summary>
    public record Doc(string Title, string Body, string? Footer, bool ShowAddress, Header? Header);
    public record Header(string? Title, string? Subtitle, string? Color);

    /// <summary>The account's details for the fill-ins.</summary>
    public record Account(Customer Customer, string? Plan, Bill? LastBill);

    public static async Task<Account?> AccountAsync(AppDbContext db, int? customerId)
    {
        if (customerId == null || await db.Customers.Include(c => c.Plan).FirstOrDefaultAsync(c => c.Id == customerId) is not { } c) return null;
        return new Account(c, c.Plan?.Name, await db.Bills.Where(b => b.CustomerId == c.Id).OrderByDescending(b => b.BilledOn).FirstOrDefaultAsync());
    }

    /// <summary>The template's document: its header (the one picked, else the default) and footer (its own small print, else the one picked, else the default).</summary>
    public static async Task<Doc> DocumentAsync(AppDbContext db, PdfTemplate t)
    {
        var header = (t.PdfHeaderId is { } h ? await db.PdfParts.FindAsync(h) : null) ?? await db.PdfParts.FirstOrDefaultAsync(p => p.Kind == "header" && p.IsDefault);
        var footer = !string.IsNullOrWhiteSpace(t.Footer) ? t.Footer
            : ((t.PdfFooterId is { } f ? await db.PdfParts.FindAsync(f) : null) ?? await db.PdfParts.FirstOrDefaultAsync(p => p.Kind == "footer" && p.IsDefault))?.Body;
        return new Doc(t.Title, t.Body, footer, t.ShowAddress, header == null ? null : new Header(header.Title, header.Subtitle, header.Color));
    }

    public static Doc DocumentOf(PdfGeneration g) =>
        new(g.Title, g.Body, g.Footer, g.ShowAddress, g.Header == null ? null : JsonSerializer.Deserialize<Header>(g.Header));

    public static string FileName(PdfTemplate t, Customer? c) =>
        Regex.Replace(t.Name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-') + (c != null ? "-" + c.Account : "-sample") + ".pdf";

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Replace {{fill_ins}} with the account's details (left as-is for unknown names).</summary>
    public static string Fill(string text, Account? a)
    {
        var c = a?.Customer;
        var v = new Dictionary<string, string?>
        {
            ["first_name"] = c?.FirstName, ["last_name"] = c?.LastName, ["name"] = c?.Name, ["account"] = c?.Account,
            ["address"] = c == null ? null : (c.Address + (c.Unit != null ? " #" + c.Unit : "")).Trim(), ["city"] = c?.City, ["zip"] = c?.Zip, ["esiid"] = c?.Esiid,
            ["plan"] = a?.Plan, ["balance"] = c?.Balance.ToString("$#,##0.00", Us),
            ["due_date"] = a?.LastBill?.DueOn.ToString("MMMM d, yyyy", Us), ["last_bill"] = a?.LastBill?.Amount.ToString("$#,##0.00", Us),
            ["today"] = DateTime.Today.ToString("MMMM d, yyyy", Us), ["brand"] = Rodeo.Brand, ["phone"] = Rodeo.Phone,
        };
        return Regex.Replace(text, @"\{\{\s*([a-z_]+)\s*\}\}", m => v.TryGetValue(m.Groups[1].Value, out var x) ? x ?? "—" : m.Value);
    }

    private const string Navy = "#102247", Cyan = "#00AEEF", Grey = "#6B7380", Sky = "#EDF5F7", Ink = "#1A2147";
    private const double Left = 48, Right = 564, Bottom = 712;

    public static SimplePdf Build(Doc doc, Account? a)
    {
        var pdf = new SimplePdf();
        var c = a?.Customer;
        var title = Fill(doc.Title, a);
        var footer = Fill(doc.Footer ?? "", a).Trim();

        // First page: the header band (its big text, the line under it and its colors), the account's name and address, the title
        var band = Fill(doc.Header?.Title ?? "", a).Trim() is { Length: > 0 } b ? b : Rodeo.Brand;
        var under = Fill(doc.Header?.Subtitle ?? "", a).Trim() is { Length: > 0 } u ? u : title;
        var (fill, ink, soft) = doc.Header?.Color switch
        {
            "cyan" => ((string?)Cyan, "#FFFFFF", "#E6F7FF"),
            "white" => (null, Navy, Grey),
            _ => (Navy, "#FFFFFF", "#BFD9F2"),
        };
        if (fill != null) pdf.Box(0, 0, SimplePdf.Width, 80, fill);
        else pdf.Box(0, 78, SimplePdf.Width, 2, Cyan);
        pdf.Text(Left, 26, band.ToUpperInvariant(), 20, true, ink).Text(Left, 50, under, 10, false, soft)
            .Text(Right, 32, DateTime.Today.ToString("MMMM d, yyyy", Us), 10, false, soft, right: true);
        var y = 104.0;
        if (doc.ShowAddress)
        {
            if (c != null)
            {
                pdf.Text(Left, y, c.Name, 11, true)
                    .Text(Left, y + 15, (c.Address + (c.Unit != null ? " #" + c.Unit : "")).Trim(), 10, false, Grey)
                    .Text(Left, y + 29, ((c.City is { Length: > 0 } city ? city + ", " : "") + "TX " + c.Zip).Trim(), 10, false, Grey)
                    .Text(Right, y, "Account " + c.Account, 10, true, Navy, right: true);
            }
            y += 58;
        }
        pdf.Text(Left, y, title, 17, true, Ink).Box(Left, y + 22, 56, 3, Cyan);
        y += 40;

        void Room(double need)
        {
            if (y + need <= Bottom) return;
            pdf.NewPage().Box(0, 0, SimplePdf.Width, 6, Navy);
            y = 44;
        }
        List<string> Wrap(string text, double size, bool bold, double width)
        {
            var lines = new List<string>();
            var line = "";
            foreach (var word in Regex.Split(text.Trim(), @"\s+").Where(w => w != ""))
            {
                var attempt = line == "" ? word : line + " " + word;
                if (line != "" && SimplePdf.Measure(attempt, size, bold) > width) { lines.Add(line); line = word; }
                else line = attempt;
            }
            if (line != "") lines.Add(line);
            return lines;
        }
        void Paragraph(string text, double size = 10.5, bool bold = false, double indent = 0, string color = Ink)
        {
            foreach (var line in Wrap(text, size, bold, Right - Left - indent))
            {
                Room(size + 5);
                pdf.Text(Left + indent, y, line, size, bold, color);
                y += size + 5;
            }
        }

        var buffer = new List<string>();
        void Flush()
        {
            if (buffer.Count == 0) return;
            Paragraph(string.Join(" ", buffer));
            y += 7;
            buffer.Clear();
        }
        foreach (var raw in Regex.Split(Fill(doc.Body, a), @"\r?\n"))
        {
            var line = raw.Trim();
            Match m;
            if (line == "") { Flush(); continue; }
            if ((m = Regex.Match(line, @"^(#{1,2})\s+(.*)$")).Success)
            {
                Flush();
                var size = m.Groups[1].Value == "#" ? 14 : 12;
                Room(size + 30);
                y += 6;
                Paragraph(m.Groups[2].Value, size, true, 0, Navy);
                y += 4;
            }
            else if ((m = Regex.Match(line, @"^[-*]\s+(.*)$")).Success)
            {
                Flush();
                Room(16);
                pdf.Box(Left + 4, y + 4, 3.5, 3.5, Cyan);
                Paragraph(m.Groups[1].Value.Replace("**", ""), 10.5, false, 16);
                y += 2;
            }
            else if ((m = Regex.Match(line, @"^\|(.+)\|$")).Success)
            {
                Flush();
                var cells = m.Groups[1].Value.Split('|').Select(x => x.Trim()).ToArray();
                Room(26);
                pdf.Box(Left, y - 5, Right - Left, 22, Sky)
                    .Text(Left + 10, y + 1, cells.ElementAtOrDefault(0) ?? "", 10, true, Grey)
                    .Text(Right - 10, y + 1, cells.ElementAtOrDefault(1) ?? "", 10.5, true, Navy, right: true);
                y += 26;
            }
            else if (line == "---")
            {
                Flush();
                Room(16);
                pdf.Line(Left, y + 4, Right, y + 4);
                y += 14;
            }
            else if ((m = Regex.Match(line, @"^\*\*(.+)\*\*$")).Success)
            {
                Flush();
                Paragraph(m.Groups[1].Value, 10.5, true);
                y += 7;
            }
            else buffer.Add(line.Replace("**", "")); // bold inside a line isn't supported: keep the words
        }
        Flush();

        // Footer and page numbers on every page
        var total = pdf.PageCount;
        for (var i = 0; i < total; i++)
        {
            pdf.OnPage(i).Line(Left, 732, Right, 732).Text(Right, 742, $"Page {i + 1} of {total}", 8, false, Grey, right: true);
            var fy = 742.0;
            foreach (var fl in Wrap(footer == "" ? Rodeo.Brand : footer, 7.5, false, 420).Take(3))
            {
                pdf.Text(Left, fy, fl, 7.5, false, Grey);
                fy += 10;
            }
        }
        return pdf;
    }
}
