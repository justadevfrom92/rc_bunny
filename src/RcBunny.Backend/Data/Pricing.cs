namespace RcBunny.Data;

/// <summary>Pricing math shared by the website and the admin.</summary>
public static class Pricing
{
    /// <summary>Average price in ¢/kWh at a usage level, the way the Electricity Facts Label shows it.</summary>
    public static decimal AveragePrice(decimal energy, decimal tdspPerKwh, decimal tdspPerBill, decimal mrc, int kwh)
        => energy + tdspPerKwh + (tdspPerBill + mrc) / kwh * 100m;

    public static string TermLabel(int term) => term == 1 ? "Month-to-Month" : $"{term} Months · Fixed";

    public static string EtfLabel(string etf) => etf.Any(char.IsDigit) ? etf.Split('/')[0] + " ETF" : "No ETF";
}
