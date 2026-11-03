using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.Json;

namespace RcBunny.Backend;

/// <summary>Uploaded and generated files (reports, uploads, attachments) under Storage:Root (default: storage/ in the repo).</summary>
public sealed class FileStore(IConfiguration config, IWebHostEnvironment env)
{
    public string Root { get; } = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../storage"));

    /// <summary>Full path for a relative one; refuses anything that escapes the root.</summary>
    public string PathFor(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(Root, relative));
        if (!full.StartsWith(Root, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Path outside storage.");
        return full;
    }

    public async Task SaveAsync(string relative, byte[] bytes)
    {
        var full = PathFor(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, bytes);
    }

    public async Task SaveAsync(string relative, Stream stream)
    {
        var full = PathFor(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var f = File.Create(full);
        await stream.CopyToAsync(f);
    }

    public bool Exists(string? relative) => relative != null && File.Exists(PathFor(relative));
}

/// <summary>Writes a table as xlsx, csv, tsv or json (Walker's export formats).</summary>
public static class TableExport
{
    public static (byte[] Bytes, string Mime, string Ext) Write(string format, IReadOnlyList<string> columns, IReadOnlyList<object?[]> rows, string sheet = "Report") => format switch
    {
        "csv" => (Delimited(columns, rows, ','), "text/csv", "csv"),
        "tsv" => (Delimited(columns, rows, '\t'), "text/tab-separated-values", "tsv"),
        "json" => (JsonSerializer.SerializeToUtf8Bytes(rows.Select(r => columns.Select((c, i) => (c, r[i])).ToDictionary(x => x.c, x => x.Item2)), new JsonSerializerOptions { WriteIndented = true }), "application/json", "json"),
        _ => (Xlsx(columns, rows, sheet), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
    };

    private static byte[] Delimited(IReadOnlyList<string> columns, IReadOnlyList<object?[]> rows, char sep)
    {
        var sb = new StringBuilder();
        string Cell(object? v)
        {
            var s = v switch { null => "", DateTime d => d.ToString("yyyy-MM-dd HH:mm"), DateOnly d => d.ToString("yyyy-MM-dd"), decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => v.ToString() ?? "" };
            return sep == ',' && (s.Contains(',') || s.Contains('"') || s.Contains('\n')) ? "\"" + s.Replace("\"", "\"\"") + "\"" : s.Replace('\t', ' ');
        }
        sb.AppendLine(string.Join(sep, columns.Select(Cell)));
        foreach (var r in rows) sb.AppendLine(string.Join(sep, r.Select(Cell)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    /// <summary>A minimal, valid .xlsx (one sheet, inline strings, numbers as numbers).</summary>
    private static byte[] Xlsx(IReadOnlyList<string> columns, IReadOnlyList<object?[]> rows, string sheet)
    {
        static string Col(int i) { var s = ""; for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s; return s; }
        var x = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        void Row(int r, IEnumerable<object?> cells)
        {
            x.Append($"<row r=\"{r}\">");
            var c = 0;
            foreach (var v in cells)
            {
                var refId = Col(c++) + r;
                if (v is int or long or decimal or double or float)
                    x.Append($"<c r=\"{refId}\"><v>{Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)}</v></c>");
                else if (v is not null)
                {
                    var s = v switch { DateTime d => d.ToString("yyyy-MM-dd HH:mm"), DateOnly d => d.ToString("yyyy-MM-dd"), bool b => b ? "Yes" : "No", _ => v.ToString() };
                    x.Append($"<c r=\"{refId}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(s)}</t></is></c>");
                }
            }
            x.Append("</row>");
        }
        Row(1, columns);
        for (var i = 0; i < rows.Count; i++) Row(i + 2, rows[i]);
        x.Append("</sheetData></worksheet>");

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string name, string content) { using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); w.Write(content); }
            Add("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Add("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Add("xl/workbook.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"{SecurityElement.Escape(sheet[..Math.Min(31, sheet.Length)])}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Add("xl/worksheets/sheet1.xml", x.ToString());
        }
        return ms.ToArray();
    }
}
