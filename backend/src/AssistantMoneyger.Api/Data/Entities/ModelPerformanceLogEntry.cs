namespace AssistantMoneyger.Api.Data.Entities;

// One row per gameweek, ever — how accurate the formula was that week.
// Gw is the primary key, so a gameweek can only be logged once.
public class ModelPerformanceLogEntry
{
    public int Gw { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
    public int NumPlayers { get; set; }
    public double MeanAbsoluteError { get; set; }
}
