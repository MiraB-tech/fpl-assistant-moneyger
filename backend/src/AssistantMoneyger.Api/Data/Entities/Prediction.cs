namespace AssistantMoneyger.Api.Data.Entities;

// Global, shared by every user — one row per player per gameweek.
// Composite key (Gw, PlayerId) is configured in AppDbContext. The
// breakdown fields (everything besides Xp) aren't shown in the app yet,
// but are kept for transparency/debugging — per the project's own goal
// of an explainable model, not a black box.
public class Prediction
{
    public int Gw { get; set; }
    public int PlayerId { get; set; }
    public string Name { get; set; } = "";
    public string Position { get; set; } = "";
    public string Team { get; set; } = "";
    public decimal Price { get; set; }
    public double StartProbability { get; set; }
    public double CleanSheetProbability { get; set; }
    public double ExpectedGoals { get; set; }
    public double ExpectedAssists { get; set; }
    public double Xp { get; set; }
    public DateTimeOffset ComputedAt { get; set; }
}
