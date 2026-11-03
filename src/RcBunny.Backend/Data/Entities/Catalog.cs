namespace RcBunny.Data.Entities;

public class Market
{
    public int Id { get; set; }
    public string Name { get; set; } = "";        // e.g. TX-E-ONCOR
    public string Short { get; set; } = "";
    public string Description { get; set; } = "";
    public string Region { get; set; } = "";      // column in the term discount grid
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ZipRange
{
    public int Id { get; set; }
    public int ZipFrom { get; set; }
    public int ZipTo { get; set; }
    public int MarketId { get; set; }
    public Market? Market { get; set; }
}

/// <summary>TDSP delivery charges. History is kept: the current row is the latest effective today or earlier.</summary>
public class TdspFee
{
    public int Id { get; set; }
    public int MarketId { get; set; }
    public Market? Market { get; set; }
    public decimal PerKwh { get; set; }     // ¢/kWh
    public decimal PerBill { get; set; }    // $ per bill
    public DateOnly EffectiveOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Plan
{
    public int Id { get; set; }
    public string Type { get; set; } = "Resi";    // Resi | Biz
    public int Term { get; set; }                 // months
    public string Name { get; set; } = "";
    public string Internal { get; set; } = "";    // plan code
    public string Slug { get; set; } = "";
    public string Rolloff { get; set; } = "None";
    public string Etf { get; set; } = "-";
    public decimal Mrc { get; set; }
    public int Green { get; set; } = 100;
    public bool Active { get; set; } = true;
    public List<string> Tags { get; set; } = [];   // website filters: fixed, green, flex, month
    public List<string> Perks { get; set; } = [];  // website bullets
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<PlanGroupPlan> Groups { get; set; } = [];
}

public class PlanGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<PlanGroupPlan> Plans { get; set; } = [];
}

public class PlanGroupPlan
{
    public int PlanGroupId { get; set; }
    public PlanGroup? PlanGroup { get; set; }
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    public int Position { get; set; }
}

/// <summary>Energy charge per plan and market. History is kept by EffectiveOn.</summary>
public class Rate
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    public int MarketId { get; set; }
    public Market? Market { get; set; }
    public decimal Energy { get; set; }     // ¢/kWh
    public DateOnly EffectiveOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class TermDiscount
{
    public int Id { get; set; }
    public int Term { get; set; }
    public string Region { get; set; } = "";
    public decimal Discount { get; set; }   // ¢/kWh; positive lowers the rate
}

public class TermEtf
{
    public int Term { get; set; }
    public int Amount { get; set; }         // $
}

public class ByopProduct
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public string Type { get; set; } = "";
    public string Step { get; set; } = "";
    public string? Description { get; set; }
    public decimal RateAdj { get; set; }    // ¢/kWh
    public decimal Monthly { get; set; }    // $
    public bool Active { get; set; } = true;
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
