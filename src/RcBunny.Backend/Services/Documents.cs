using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Services;

/// <summary>
/// The documents we generate for an account (welcome letter, Electricity Facts Label, Terms of Service, Your Rights as a
/// Customer, invoice), in English or Spanish, as on-screen text and as a PDF. Built fresh from the account each time.
/// </summary>
public static class Documents
{
    public static readonly Dictionary<string, string> Languages = new() { ["en"] = "English", ["es"] = "Spanish" };

    public record Doc(string Title, List<string> Paragraphs, List<(string Label, string Value)> Rows);

    public static string Preferred(string? language) => (language ?? "").StartsWith("span", StringComparison.OrdinalIgnoreCase) ? "es" : "en";

    public static async Task<Doc> RenderAsync(AppDbContext db, CustomerFile file, Customer c, string lang)
    {
        var es = lang == "es";
        var plan = c.Plan?.Name ?? (es ? "su plan" : "your plan");
        var bill = await db.Bills.Where(b => b.CustomerId == c.Id).OrderByDescending(b => b.BilledOn).FirstOrDefaultAsync();
        string Date(DateOnly? d) => d?.ToString(es ? "d/M/yyyy" : "M/d/yyyy") ?? "—";

        return file.Kind switch
        {
            "efl" => new Doc(es ? "Etiqueta de Datos de Electricidad" : "Electricity Facts Label",
                [es ? $"Plan: {plan}. Precio promedio por kWh según su uso mensual:" : $"Plan: {plan}. Average price per kWh at your monthly usage:"],
                [(es ? "Uso de 500 kWh" : "500 kWh use", "18.4¢"), (es ? "Uso de 1,000 kWh" : "1,000 kWh use", "15.9¢"), (es ? "Uso de 2,000 kWh" : "2,000 kWh use", "15.1¢"),
                 (es ? "Plazo" : "Term", $"{c.Plan?.TermMonths ?? 12}{(es ? " meses" : " months")}"), (es ? "Cargo por cancelación" : "Cancellation fee", "$150.00")]),
            "yrac" => new Doc(es ? "Sus Derechos como Cliente" : "Your Rights as a Customer",
                es ? ["Usted tiene derecho a recibir una copia de su contrato.", "Usted puede cancelar su contrato dentro de los tres días hábiles después de recibirlo, sin cargo.", "Usted puede presentar una queja ante la Comisión de Servicios Públicos."]
                   : ["You have the right to a copy of your contract.", "You may cancel within three business days of receiving your contract, at no charge.", "You may file a complaint with the Public Utility Commission."],
                []),
            "tos" => new Doc(es ? "Términos de Servicio" : "Terms of Service",
                es ? [$"Este acuerdo es entre {c.Name} (cuenta {c.Account}) y Energy Texas.", $"Plan: {plan}. Las facturas vencen 16 días después de la fecha de emisión.", "Los pagos atrasados pueden tener un cargo de 5%."]
                   : [$"This agreement is between {c.Name} (account {c.Account}) and Energy Texas.", $"Plan: {plan}. Bills are due 16 days after they are issued.", "Late payments may carry a 5% fee."],
                []),
            "welcome" => new Doc(es ? "Carta de Bienvenida" : "Welcome Letter",
                es ? [$"¡Bienvenido/a, {c.FirstName ?? c.Name}!", $"Su servicio en {c.Address}, {c.City} comienza pronto. Su plan: {plan}.", $"Su número de cuenta es {c.Account}. Administre su cuenta en Mi Cuenta."]
                   : [$"Welcome, {c.FirstName ?? c.Name}!", $"Your service at {c.Address}, {c.City} starts soon. Your plan: {plan}.", $"Your account number is {c.Account}. Manage your account in My Account."],
                []),
            _ => new Doc(es ? "Factura" : "Invoice",
                [es ? $"Factura para {c.Name}, cuenta {c.Account}." : $"Invoice for {c.Name}, account {c.Account}."],
                bill is null ? [] :
                [(es ? "Factura" : "Invoice", bill.Invoice ?? bill.Reference), (es ? "Periodo" : "Period", $"{Date(bill.PeriodStart)} – {Date(bill.PeriodEnd)}"),
                 ("kWh", bill.Kwh.ToString("N0")), (es ? "Monto" : "Amount", bill.Amount.ToString("C")), (es ? "Vence" : "Due", Date(bill.DueOn))]),
        };
    }

    public static byte[] Pdf(Doc doc, Customer c)
    {
        var pdf = new SimplePdf();
        pdf.Box(0, 0, SimplePdf.Width, 70, "#102247").Text(54, 24, "Energy Texas", 18, true, "#FFFFFF");
        pdf.Text(54, 100, c.Name, 11).Text(54, 116, $"{c.Address}, {c.City}, TX {c.Zip}", 10, false, "#4d5566").Text(54, 130, $"Account {c.Account}", 10, false, "#4d5566");
        pdf.Text(54, 170, doc.Title, 18, true, "#102247");
        var y = 205.0;
        foreach (var p in doc.Paragraphs) y = pdf.Paragraph(54, y, SimplePdf.Width - 108, p) + 8;
        foreach (var (label, value) in doc.Rows) { pdf.Text(54, y, label, 11, true).Text(300, y, value, 11); y += 20; }
        return pdf.Output(doc.Title);
    }

    public static string PdfName(CustomerFile file, string lang) =>
        System.IO.Path.GetFileNameWithoutExtension(file.Name) + (lang == "es" ? "-es" : "") + ".pdf";
}
