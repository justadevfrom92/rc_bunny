namespace RcBunny.Frontend.Services;

/// <summary>Brand settings (ebunny's config/brand.php). Override any of them under "Brand" in appsettings.</summary>
public sealed class Brand
{
    public string Name { get; set; } = "Energy Texas";
    public string Wordmark { get; set; } = "ENERGY TEXAS";
    public string Tagline { get; set; } = "POWERED BY TEXANS";
    public string Domain { get; set; } = "energytexas.com";
    public string Phone { get; set; } = "1-800-555-0100";
    public string PhoneHref { get; set; } = "tel:18005550100";
    public string Hours { get; set; } = "Mon – Fri, 8am – 6pm CT";
    public string Puct { get; set; } = "XXXXX";
    public string Rewards { get; set; } = "Rangler Rewards";
    public string AdminUrl { get; set; } = "http://localhost:5200";

    public static readonly (string Href, string Label)[] Nav =
    [
        ("/", "Home"),
        ("/plans", "Plans"),
        ("/build-your-own-plan", "Build Your Own Plan"),
        ("/business", "Business"),
        ("/shop", "Shop"),
        ("/contact-us", "Contact Us"),
    ];
}
