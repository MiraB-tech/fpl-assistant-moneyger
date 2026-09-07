using AssistantMoneyger.Api.Fpl.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace AssistantMoneyger.Api.Fpl;

// Everything this app needs from the official, free, no-login FPL API.
// Registered as a typed HttpClient (see Program.cs) so its BaseAddress
// and any future retry/timeout policy live in one place.
public class FplApiClient(HttpClient http, IMemoryCache cache)
{
    private const string BootstrapCacheKey = "fpl:bootstrap-static";

    // bootstrap-static is the full ~650-player list — large, and fetched
    // by both GetCurrentGameweek (just to read two numbers) and the
    // predictions refresh. A short cache avoids redownloading it on every
    // page load; RefreshPredictions' own 6-hour staleness gate is still
    // what governs how often predictions actually get recomputed.
    public async Task<BootstrapStaticDto?> GetBootstrapStaticAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(BootstrapCacheKey, out BootstrapStaticDto? cached))
        {
            return cached;
        }

        var bootstrap = await http.GetFromJsonAsync<BootstrapStaticDto>("bootstrap-static/", ct);
        cache.Set(BootstrapCacheKey, bootstrap, TimeSpan.FromMinutes(5));
        return bootstrap;
    }

    public Task<List<FixtureDto>?> GetFixturesAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<List<FixtureDto>>("fixtures/", ct);

    // {player_id: points} for one finished gameweek — same shape the
    // Python version used for recent_form and evaluation.
    public async Task<Dictionary<int, int>> GetLiveEventAsync(int gw, CancellationToken ct = default)
    {
        var live = await http.GetFromJsonAsync<LiveEventDto>($"event/{gw}/live/", ct);
        return live?.Elements.ToDictionary(e => e.Id, e => e.Stats.TotalPoints) ?? [];
    }

    public Task<EntryPicksDto?> GetEntryPicksAsync(int teamId, int gw, CancellationToken ct = default) =>
        http.GetFromJsonAsync<EntryPicksDto>($"entry/{teamId}/event/{gw}/picks/", ct);

    // Existence check only — throws HttpRequestException if the team ID
    // doesn't exist, which callers catch to reject an invalid registration.
    public Task<EntryDto?> GetEntryAsync(int teamId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<EntryDto>($"entry/{teamId}/", ct);
}
