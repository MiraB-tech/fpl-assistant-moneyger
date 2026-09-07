namespace AssistantMoneyger.Api.Data.Entities;

// Tracks when a gameweek's predictions were last (re)computed. This is
// what makes the "refresh predictions" button safe for many users to
// press at once: whoever asks first triggers the real work, and everyone
// else within the staleness window just gets the cached rows back.
public class PredictionRun
{
    public int Gw { get; set; }
    public DateTimeOffset LastRefreshedAt { get; set; }
    public int PlayerCount { get; set; }
}
