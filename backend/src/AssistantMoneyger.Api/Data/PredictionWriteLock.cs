using Microsoft.EntityFrameworkCore;

namespace AssistantMoneyger.Api.Data;

// Makes the shared "grade last gameweek / rebuild predictions" work run
// one request at a time. Without this, two requests arriving together
// (two users pressing Refresh at once, or React running the page-load
// refresh twice in development) both see "not done yet", both try to
// save the same rows, and the second crashes with a duplicate-key 500.
//
// The lock is held by Postgres itself (an "advisory lock"), not by this
// server process, so it still works if the backend is ever run as more
// than one copy. It's tied to the transaction: it's released
// automatically when the work commits, or if the request fails.
public static class PredictionWriteLock
{
    // Any fixed number works — it's just the name of this particular lock,
    // so it only has to be the same everywhere this lock is taken.
    private const long LockKey = 7_420_001;

    public static async Task<T> RunExclusiveAsync<T>(AppDbContext db, Func<Task<T>> work, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Waits here until whoever is holding the lock has finished. By then
        // their work is saved, so the "already done?" checks inside `work`
        // see it and skip instead of redoing it.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", [LockKey], ct);

        var result = await work();
        await transaction.CommitAsync(ct);
        return result;
    }
}
