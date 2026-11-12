using System.Globalization;
using System.Text;

namespace RcBunny.Services;

/// <summary>
/// A tiny PDF writer (like ebunny's App\Support\Pdf): Letter-size pages with text, lines and filled boxes in
/// the standard Helvetica fonts. Enough for letters, notices, bills and reports without a third-party library.
/// </summary>
public sealed class SimplePdf
{
    private readonly List<StringBuilder> pages = [new()];
    // The same drawing as SVG, one per page, for previews on screen (like ebunny's Pdf::svgs)
    private readonly List<StringBuilder> svgs = [new()];
    private int current;
    private StringBuilder Page => pages[current];
    public const double Width = 612, Height = 792;

    private static string Esc(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s)
        {
            var c = ch switch { '’' or '‘' => '\'', '“' or '”' => '"', '–' or '—' => '-', '·' => '-', '•' => '*', '¢' => 'c', _ => ch };
            if (c is '(' or ')' or '\\') sb.Append('\\');
            sb.Append(c < 128 ? c : '?');
        }
        return sb.ToString();
    }

    private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Text with its top-left at (x, y) from the top of the page; right = true puts its right edge at x.</summary>
    public SimplePdf Text(double x, double y, string text, double size = 11, bool bold = false, string color = "#262626", bool right = false)
    {
        var (r, g, b) = Rgb(color);
        if (right) x -= Measure(text, size, bold);
        Page.Append($"BT /{(bold ? "F2" : "F1")} {N(size)} Tf {N(r)} {N(g)} {N(b)} rg {N(x)} {N(Height - y - size)} Td ({Esc(text)}) Tj ET\n");
        svgs[current].Append($"<text x=\"{N(x)}\" y=\"{N(y + size)}\" font-size=\"{N(size)}\"{(bold ? " font-weight=\"bold\"" : "")} fill=\"{color}\">{System.Net.WebUtility.HtmlEncode(text)}</text>");
        return this;
    }

    /// <summary>Wrapped paragraph; returns the y below it.</summary>
    public double Paragraph(double x, double y, double width, string text, double size = 11, double lineHeight = 1.4)
    {
        var maxChars = Math.Max(10, (int)(width / (size * 0.5)));
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in raw.Split(' '))
            {
                if (line.Length + word.Length + 1 > maxChars) { y = NextLine(x, y, line.ToString(), size, lineHeight); line.Clear(); }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            y = NextLine(x, y, line.ToString(), size, lineHeight);
        }
        return y;
    }

    private double NextLine(double x, double y, string text, double size, double lh)
    {
        if (y + size * lh > Height - 60) { NewPage(); y = 60; }
        Text(x, y, text, size);
        return y + size * lh;
    }

    public SimplePdf Box(double x, double y, double w, double h, string color)
    {
        var (r, g, b) = Rgb(color);
        Page.Append($"{N(r)} {N(g)} {N(b)} rg {N(x)} {N(Height - y - h)} {N(w)} {N(h)} re f\n");
        svgs[current].Append($"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" fill=\"{color}\"/>");
        return this;
    }

    public SimplePdf Line(double x1, double y1, double x2, double y2, string color = "#D5D9DA")
    {
        var (r, g, b) = Rgb(color);
        Page.Append($"{N(r)} {N(g)} {N(b)} RG 0.75 w {N(x1)} {N(Height - y1)} m {N(x2)} {N(Height - y2)} l S\n");
        svgs[current].Append($"<line x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" stroke=\"{color}\" stroke-width=\"0.75\"/>");
        return this;
    }

    public SimplePdf NewPage() { pages.Add(new StringBuilder()); svgs.Add(new StringBuilder()); current = pages.Count - 1; return this; }

    /// <summary>Every page as SVG, to show on screen.</summary>
    public List<string> Svgs() => svgs.Select(s => $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {N(Width)} {N(Height)}\" font-family=\"Helvetica, Arial, sans-serif\" role=\"img\"><rect width=\"{N(Width)}\" height=\"{N(Height)}\" fill=\"#fff\"/>{s}</svg>").ToList();

    /// <summary>Draw on an earlier page (e.g. page numbers once the page count is known).</summary>
    public SimplePdf OnPage(int index) { current = index; return this; }

    // Helvetica's character widths (per 1000 units) for the printable ASCII range, regular and bold
    private static readonly int[] Regular = [278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278, 556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556, 1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778, 667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556, 333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556, 556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584];
    private static readonly int[] Bold = [278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278, 556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611, 975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778, 667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556, 333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611, 611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584];

    /// <summary>How wide text is in points.</summary>
    public static double Measure(string text, double size, bool bold = false)
    {
        var widths = bold ? Bold : Regular;
        return text.Sum(ch => ch is >= ' ' and <= '~' ? widths[ch - ' '] : 556) * size / 1000.0;
    }

    public int PageCount => pages.Count;

    private static (double, double, double) Rgb(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return (0, 0, 0);
        return (Convert.ToInt32(hex[..2], 16) / 255.0, Convert.ToInt32(hex[2..4], 16) / 255.0, Convert.ToInt32(hex[4..], 16) / 255.0);
    }

    public byte[] Output(string title = "Document")
    {
        var objs = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "", // pages, filled below
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
            $"<< /Title ({Esc(title)}) /Producer (RC Bunny) >>",
        };
        var kids = new List<int>();
        foreach (var p in pages)
        {
            var content = p.ToString();
            objs.Add($"<< /Length {Encoding.Latin1.GetByteCount(content)} >>\nstream\n{content}endstream");
            var contentId = objs.Count;
            objs.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(Width)} {N(Height)}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentId} 0 R >>");
            kids.Add(objs.Count);
        }
        objs[1] = $"<< /Type /Pages /Kids [{string.Join(" ", kids.Select(k => k + " 0 R"))}] /Count {kids.Count} >>";

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objs.Count; i++)
        {
            offsets.Add(Encoding.Latin1.GetByteCount(sb.ToString()));
            sb.Append($"{i + 1} 0 obj\n{objs[i]}\nendobj\n");
        }
        var xref = Encoding.Latin1.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objs.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objs.Count + 1} /Root 1 0 R /Info 5 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>A letter: optional header band, address block, title, body, footer.</summary>
    public static SimplePdf Letter(string? headerTitle, string? headerSubtitle, string headerColor, string? address, string title, string body, string? footer)
    {
        var pdf = new SimplePdf();
        var y = 50.0;
        if (headerTitle != null)
        {
            pdf.Box(0, 0, Width, 70, headerColor).Text(54, 20, headerTitle, 18, true, "#FFFFFF");
            if (headerSubtitle != null) pdf.Text(54, 44, headerSubtitle, 9, false, "#FFFFFF");
            y = 100;
        }
        pdf.Text(400, y, DateTime.Today.ToString("MMMM d, yyyy", CultureInfo.GetCultureInfo("en-US")), 10);
        if (address != null) y = pdf.Paragraph(54, y, 300, address, 10) + 20;
        else y += 30;
        pdf.Text(54, y, title, 15, true, "#102247");
        y = pdf.Paragraph(54, y + 30, 500, body, 11);
        if (footer != null) { pdf.Line(54, Height - 70, Width - 54, Height - 70); pdf.Text(54, Height - 60, footer, 8, false, "#96999d"); }
        return pdf;
    }
}
