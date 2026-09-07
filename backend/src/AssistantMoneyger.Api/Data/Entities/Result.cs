namespace AssistantMoneyger.Api.Data.Entities;

// Predicted vs. actual points for one player in one finished gameweek.
// Composite key (Gw, PlayerId) is configured in AppDbContext.
public class Result
{
    public int Gw { get; set; }
    public int PlayerId { get; set; }
    public string Name { get; set; } = "";
    public string Position { get; set; } = "";
    public string Team { get; set; } = "";
    public double PredictedPoints { get; set; }
    public double ActualPoints { get; set; }
    public double Difference { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
}
