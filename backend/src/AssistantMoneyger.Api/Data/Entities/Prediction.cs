namespace AssistantMoneyger.Api.Data.Entities;

// Global, shared by every user — one row per player per gameweek.
// Composite key (Gw, PlayerId) is configured in AppDbContext.
public class Prediction
{
    public int Gw { get; set; }
    public int PlayerId { get; set; }
    public string Name { get; set; } = "";
    public string Position { get; set; } = "";
    public string Team { get; set; } = "";
    public decimal Price { get; set; }
    public double RecentForm { get; set; }
    public double XaXgPer90 { get; set; }
    public double FixtureDifficulty { get; set; }
    public double MinutesReliability { get; set; }
    public double Xp { get; set; }
    public DateTimeOffset ComputedAt { get; set; }
}
