using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace RcBunny.Controllers;

// Lando → Style Guide: every color and style variable in the shared, admin and website stylesheets (ebunny's StyleSheets).
public partial class LandoController
{
    /// <summary>The stylesheets, as ebunny names them, and where they are in this repository.</summary>
    private (string Key, string Label, string Path)[] StyleFiles =>
    [
        ("shared/css/tokens.css", "Shared (website + every admin app)", Path.Combine(env.ContentRootPath, "wwwroot/admin/shared/css/tokens.css")),
        ("backend/admin.css", "Admin apps", Path.Combine(env.ContentRootPath, "ClientApp/src/admin.css")),
        ("frontend/css/style.css", "Website", Path.Combine(env.ContentRootPath, "../RcBunny.Frontend/wwwroot/frontend/css/style.css")),
    ];

    private record Rule(string Selector, string Body);

    /// <summary>Rules per file (comments removed, @media flattened).</summary>
    private Dictionary<string, List<Rule>> Rules() => StyleFiles.ToDictionary(f => f.Key, f =>
    {
        var css = System.IO.File.Exists(f.Path) ? System.IO.File.ReadAllText(f.Path) : "";
        css = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        css = Regex.Replace(css, @"@import\s+url\([^)]*\)[^;]*;|@import\s+(['""])[^'""]*\1[^;]*;|@charset[^;]*;", "");
        return Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}").Select(m => new Rule(Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim(), m.Groups[2].Value.Trim())).ToList();
    });

    private static readonly (string Title, string[] Selectors, string Html)[] AdminSamples =
    [
        ("Buttons", [".btn"], "<div class=\"actions\"><button type=\"button\" class=\"btn\">Default</button><button type=\"button\" class=\"btn cyan\">Cyan</button><button type=\"button\" class=\"btn ghost\">Ghost</button><button type=\"button\" class=\"btn red\">Red</button><button type=\"button\" class=\"btn sm\">Small</button></div>"),
        ("Pills", [".pill"], "<div class=\"actions\"><span class=\"pill\">Plain</span><span class=\"pill ok\">Sent</span><span class=\"pill warn\">Pending</span><span class=\"pill bad\">Failed</span><span class=\"pill info\">Info</span></div>"),
        ("Messages", [".flash", ".banner-note", ".alert"], "<div style=\"display:grid;gap:8px\"><div class=\"flash ok\">Saved.</div><div class=\"flash bad\">That email and password do not match.</div><div class=\"banner-note\">Twilio isn't set up, so texts are logged, not sent.</div><div class=\"alert info\"><b>Heads up.</b> An info alert.</div><div class=\"alert bad\"><b>Problem.</b> A bad alert.</div></div>"),
        ("Stat cards", [".stat"], "<div class=\"stat-row\" style=\"display:grid;grid-template-columns:1fr 1fr;gap:10px\"><div class=\"stat\"><span>On Flow</span><strong>1,204</strong><small>utility accepted</small></div><div class=\"stat alert\"><span>Past Due</span><strong>37</strong><small>over 30 days</small></div></div>"),
        ("Panel & table", [".panel", ".table", "table", "th", "td", "tbody"], "<div class=\"panel\"><div class=\"panel-head\"><h2>Recent Payments</h2><span class=\"muted\">last 30 days</span></div><div class=\"table-wrap\"><table class=\"table\"><thead><tr><th>Account</th><th class=\"num\">Amount</th><th>Status</th></tr></thead><tbody><tr><td>1219000000</td><td class=\"num\">$171.68</td><td><span class=\"pill ok\">Success</span></td></tr><tr><td>1219000001</td><td class=\"num\">$84.10</td><td><span class=\"pill warn\">Pending</span></td></tr></tbody></table></div></div>"),
        ("Form fields", [".form-grid"], "<div class=\"form-grid\"><label for=\"sg-a\">Name</label><input id=\"sg-a\" value=\"Fall Renewal Reminder\"><label for=\"sg-b\">Channel</label><select id=\"sg-b\"><option>Email</option><option>SMS</option></select></div>"),
        ("Tabs", [".tabs"], "<div class=\"tabs\"><button type=\"button\" class=\"on\">Account</button><button type=\"button\">Billing</button><button type=\"button\">History</button></div>"),
        ("Counts & badges", [".count-pill", ".menu .count"], "<div class=\"actions\" style=\"align-items:center\">Notes<span class=\"count-pill\">12</span><span class=\"muted\">· the red sidebar badges use --red</span></div>"),
    ];

    private static readonly (string Title, string[] Selectors, string Html)[] SiteSamples =
    [
        ("Buttons", [".btn", ".btn-outline", ".btn-white"], "<div style=\"display:flex;gap:12px;flex-wrap:wrap\"><a class=\"btn\" href=\"#\">Sign Up</a><a class=\"btn btn-outline\" href=\"#\">Plan &amp; Renewal</a></div><div class=\"section-navy\" style=\"padding:16px;margin-top:12px;border-radius:8px\"><a class=\"btn btn-white\" href=\"#\">On navy</a></div>"),
        ("Alerts", [".site-alert", ".form-msg"], "<div class=\"site-alert\">Your request was received.</div><div class=\"site-alert ok\">Payment received. Thank you!</div><div class=\"site-alert error\">That card was declined.</div>"),
        ("Plan card", [".plan", ".checks"], "<div style=\"max-width:340px\"><article class=\"plan featured\"><span class=\"plan-flag\">Popular Pick</span><div class=\"plan-top\"><span class=\"plan-term\">36 months</span><h3>Clean &amp; Green 36</h3><div class=\"plan-rate\"><strong>13.9¢</strong><span>per kWh</span></div><p class=\"plan-rate-note\">Avg. price at 1,000 kWh</p></div><div class=\"plan-body\"><ul class=\"checks\"><li>Fixed rate for 3 years</li><li>100% renewable</li></ul><a class=\"btn btn-block\" href=\"#\">Sign Up</a></div></article></div>"),
        ("Card & form", [".co-card", ".field"], "<div class=\"co-card\" style=\"max-width:420px\"><div class=\"field\"><label for=\"sg-z\">ZIP Code</label><input id=\"sg-z\" value=\"77002\"></div><button class=\"btn\" style=\"margin-top:16px\">Check Plans</button></div>"),
        ("My Account menu", [".ma-nav"], "<nav class=\"ma-nav\" style=\"max-width:260px;position:static\"><div class=\"ma-who\"><b>Avery Sample</b><span>Account 1219000000</span></div><h4>Account</h4><a class=\"on\" href=\"#\">Dashboard</a><a href=\"#\">View Bills</a></nav>"),
        ("Header Sign Out", [".nav-out", ".nav .nav-out"], "<nav class=\"nav\" style=\"justify-content:flex-start;flex-wrap:wrap\"><a class=\"btn\" href=\"#\">My Account</a><form class=\"nav-out\"><button type=\"button\">Sign Out</button></form></nav>"),
        ("Navy section", [".section-navy", ".eyebrow"], "<div class=\"section-navy\" style=\"padding:28px;border-radius:8px\"><p class=\"eyebrow\">Rewards</p><h3 style=\"color:var(--white)\">Earn stars every month</h3><p>Text on navy uses --white.</p></div>"),
    ];

    [HttpGet("styles")]
    public object Styles()
    {
        var rules = Rules();
        var vars = new Dictionary<string, (string Value, string File)>();
        foreach (var (file, fileRules) in rules)
            foreach (var r in fileRules.Where(r => r.Selector == ":root"))
                foreach (Match m in Regex.Matches(r.Body, @"(--[\w-]+)\s*:\s*([^;]+);")) vars[m.Groups[1].Value] = (m.Groups[2].Value.Trim(), file);
        string Resolve(string v, int depth = 0) => depth > 5 ? v : Regex.Replace(v, @"var\((--[\w-]+)(?:\s*,\s*([^)]+))?\)",
            m => vars.TryGetValue(m.Groups[1].Value, out var x) ? Resolve(x.Value, depth + 1) : m.Groups[2].Success ? m.Groups[2].Value : m.Value);
        Dictionary<string, List<string>> Uses(string name) => rules.ToDictionary(f => f.Key, f => f.Value
            .Where(r => r.Selector != ":root" && Regex.IsMatch(r.Body, @"var\(\s*" + Regex.Escape(name) + @"\s*[,)]")).Select(r => r.Selector).ToList()).Where(x => x.Value.Count > 0).ToDictionary();
        List<string> VarsFor(string[] selectors, string file) => rules[file].Where(r => r.Selector.Split(',').Select(s => s.Trim())
                .Any(sel => selectors.Any(want => sel.StartsWith(want) && !Regex.IsMatch(sel[want.Length..], @"^[\w-]"))))
            .SelectMany(r => Regex.Matches(r.Body, @"var\(\s*(--[\w-]+)").Select(m => m.Groups[1].Value)).Distinct().Where(vars.ContainsKey).ToList();

        var list = vars.Select(v =>
        {
            var resolved = Resolve(v.Value.Value);
            var kind = v.Key.Contains("font") ? "font" : Regex.IsMatch(resolved, @"^(#[0-9a-fA-F]{3,8}|rgba?\(|hsla?\()") ? "color" : "other";
            var group = kind == "font" ? "Fonts" : kind == "other" ? "Sizes & effects" : Regex.IsMatch(v.Key, "^--(ok|warn|bad|info)") ? "Status colors"
                : v.Value.File == "shared/css/tokens.css" ? "Brand colors" : "App colors";
            var uses = Uses(v.Key);
            return new { name = v.Key, value = v.Value.Value, resolved, file = v.Value.File, kind, group, uses, count = uses.Sum(u => u.Value.Count) };
        }).ToList();
        var literals = rules.SelectMany(f => f.Value.Where(r => r.Selector != ":root").SelectMany(r => Regex.Matches(r.Body, @"#[0-9a-fA-F]{3,8}\b|rgba?\([^)]*\)|hsla?\([^)]*\)")
                .Select(m => (Key: Regex.Replace(m.Value, @"\s+", "").ToLowerInvariant(), Value: m.Value, Use: StyleFiles.First(x => x.Key == f.Key).Label + ": " + r.Selector))))
            .GroupBy(x => x.Key).Select(g => new { value = g.First().Value, count = g.Count(), uses = g.Select(x => x.Use).Distinct() }).OrderByDescending(x => x.count);
        var siteCss = string.Join("\n", StyleFiles.Where(f => f.Key != "backend/admin.css" && System.IO.File.Exists(f.Path))
            .Select(f => Regex.Replace(System.IO.File.ReadAllText(f.Path), @"@import[^;]+tokens\.css[^;]*;", "")));
        return new
        {
            vars = list, literals, files = StyleFiles.Select(f => new { key = f.Key, label = f.Label }),
            admin = AdminSamples.Select(s => new { title = s.Title, html = s.Html, vars = VarsFor(s.Selectors, "backend/admin.css") }),
            site = SiteSamples.Select(s => new { title = s.Title, html = s.Html, vars = VarsFor(s.Selectors, "frontend/css/style.css") }),
            siteCss,
        };
    }
}
