using CasinoTracker.Helpers;
using CasinoTracker.Models;

namespace CasinoTracker.Services;

public sealed class SessionService : ISessionService
{
    private readonly IDatabaseService _db;

    public SessionService(IDatabaseService db)
    {
        _db = db;
    }

    // ------------------------------------------------------------------ lifecycle

    public async Task<Session?> GetActiveSessionAsync()
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Session>()
            .Where(s => s.Endtime == null)
            .OrderByDescending(s => s.Starttime)
            .FirstOrDefaultAsync();
    }

    public async Task<Session?> GetAsync(int sessionId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.FindAsync<Session>(sessionId);
    }

    public async Task<Session> StartSessionAsync(int casinoId, string? name)
    {
        var conn = await _db.GetConnectionAsync();
        var now = TimeHelper.Now();

        if (string.IsNullOrWhiteSpace(name))
        {
            var casino = await conn.FindAsync<Casino>(casinoId);
            name = $"{casino?.Name ?? "Session"} {TimeHelper.FormatDate(now)}";
        }

        var session = new Session
        {
            CasinoId = casinoId,
            Name = name.Trim(),
            Starttime = now,
            Endtime = null,
        };
        await conn.InsertAsync(session);
        return session;
    }

    public async Task UpdateSessionNameAsync(int sessionId, string name)
    {
        // Targeted update so a debounced rename can never overwrite a concurrently written Endtime.
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync("UPDATE Session SET Name = ? WHERE Id = ?", name.Trim(), sessionId);
    }

    public async Task EndSessionAsync(int sessionId, long? endTime = null)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.RunInTransactionAsync(t =>
        {
            var session = t.Find<Session>(sessionId);
            if (session is null || session.Endtime.HasValue) return;

            var end = endTime ?? TimeHelper.Now();

            // Never date the end before anything that happened inside the session. This matters for the
            // auto-close path, where the end is start + limit and can otherwise precede a late entry.
            end = Math.Max(end, session.Starttime);
            var allGames = t.Table<PlayedGame>().Where(g => g.SessionId == sessionId).ToList();
            foreach (var game in allGames)
                end = Math.Max(end, Math.Max(game.Starttime, game.Endtime ?? long.MinValue));
            foreach (var exchange in t.Table<Exchange>().Where(x => x.SessionId == sessionId).ToList())
                end = Math.Max(end, exchange.Timestamp);
            foreach (var consumed in t.Table<ConsumedMenuitem>().Where(x => x.SessionId == sessionId).ToList())
                end = Math.Max(end, consumed.Timestamp);

            var openGames = allGames.Where(g => g.Endtime is null).ToList();

            foreach (var game in openGames)
            {
                game.Endtime = end;
                t.Update(game);
            }

            session.Endtime = end;
            t.Update(session);
        });
    }

    public async Task DeleteSessionAsync(int sessionId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.RunInTransactionAsync(t =>
        {
            t.Table<Exchange>().Delete(x => x.SessionId == sessionId);
            t.Table<PlayedGame>().Delete(x => x.SessionId == sessionId);
            t.Table<ConsumedMenuitem>().Delete(x => x.SessionId == sessionId);
            t.Delete<Session>(sessionId);
        });
    }

    public async Task<int> AutoCloseExpiredSessionsAsync(TimeSpan maxDuration)
    {
        var conn = await _db.GetConnectionAsync();
        var now = TimeHelper.Now();
        var limit = (long)maxDuration.TotalSeconds;

        var open = await conn.Table<Session>().Where(s => s.Endtime == null).ToListAsync();
        var closed = 0;
        foreach (var session in open.Where(s => now - s.Starttime >= limit))
        {
            await EndSessionAsync(session.Id, session.Starttime + limit);
            closed++;
        }
        return closed;
    }

    // ------------------------------------------------------------------ read models

    public async Task<List<SessionSummary>> GetSummariesAsync()
    {
        var conn = await _db.GetConnectionAsync();

        var sessions = await conn.Table<Session>().OrderByDescending(s => s.Starttime).ToListAsync();
        if (sessions.Count == 0) return new List<SessionSummary>();

        var casinos = (await conn.Table<Casino>().ToListAsync()).ToDictionary(c => c.Id);
        var exchanges = (await conn.Table<Exchange>().ToListAsync()).ToLookup(e => e.SessionId);
        var games = (await conn.Table<PlayedGame>().ToListAsync()).ToLookup(g => g.SessionId);
        var consumed = (await conn.Table<ConsumedMenuitem>().ToListAsync()).ToLookup(c => c.SessionId);
        var prices = (await conn.Table<CasinoMenuitem>().ToListAsync())
            .GroupBy(p => p.CasinoId)
            .ToDictionary(g => g.Key, g => g.GroupBy(p => p.MenuitemId).ToDictionary(x => x.Key, x => x.First().Price));

        return sessions.Select(s =>
        {
            var priceMap = prices.TryGetValue(s.CasinoId, out var map) ? map : new Dictionary<int, decimal>();
            var ex = exchanges[s.Id].ToList();
            return new SessionSummary
            {
                Session = s,
                CasinoName = casinos.TryGetValue(s.CasinoId, out var c) ? c.Name : "Unknown casino",
                GameResult = games[s.Id].Sum(g => g.Amount),
                BuyIns = ex.Where(e => e.Amount > 0).Sum(e => e.Amount),
                CashOuts = -ex.Where(e => e.Amount < 0).Sum(e => e.Amount),
                ConsumptionSpend = consumed[s.Id].Sum(c => c.Price ?? (priceMap.TryGetValue(c.MenuitemId, out var p) ? p : 0m)),
                ConsumedCount = consumed[s.Id].Count(),
            };
        }).ToList();
    }

    public async Task<SessionDetail?> GetDetailAsync(int sessionId)
    {
        var conn = await _db.GetConnectionAsync();
        var session = await conn.FindAsync<Session>(sessionId);
        if (session is null) return null;

        var casino = await conn.FindAsync<Casino>(session.CasinoId);
        var exchanges = await conn.Table<Exchange>().Where(e => e.SessionId == sessionId).OrderBy(e => e.Timestamp).ToListAsync();
        var played = await conn.Table<PlayedGame>().Where(g => g.SessionId == sessionId).OrderBy(g => g.Starttime).ToListAsync();
        var consumed = await conn.Table<ConsumedMenuitem>().Where(c => c.SessionId == sessionId).OrderBy(c => c.Timestamp).ToListAsync();
        var menuitems = (await conn.Table<Menuitem>().ToListAsync()).ToDictionary(m => m.Id);
        var games = (await conn.Table<Game>().ToListAsync()).ToDictionary(g => g.Id);
        var casinoId = session.CasinoId;
        var prices = (await conn.Table<CasinoMenuitem>().Where(p => p.CasinoId == casinoId).ToListAsync())
            .GroupBy(p => p.MenuitemId)
            .ToDictionary(g => g.Key, g => g.First().Price);

        return new SessionDetail
        {
            Session = session,
            Casino = casino,
            Exchanges = exchanges,
            PlayedGames = played,
            Consumed = consumed,
            Menuitems = menuitems,
            Games = games,
            Prices = prices,
        };
    }

    public async Task<BankrollSnapshot> GetBankrollAsync(int sessionId)
    {
        var exchanges = await GetExchangesAsync(sessionId);
        var games = await GetPlayedGamesAsync(sessionId);
        return BankrollCalculator.Compute(exchanges, games);
    }

    // ------------------------------------------------------------------ exchanges

    public async Task<Exchange> AddExchangeAsync(int sessionId, decimal amount)
    {
        var conn = await _db.GetConnectionAsync();
        var exchange = new Exchange { SessionId = sessionId, Amount = amount, Timestamp = TimeHelper.Now() };
        await conn.InsertAsync(exchange);
        return exchange;
    }

    public async Task<List<Exchange>> GetExchangesAsync(int sessionId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Exchange>().Where(e => e.SessionId == sessionId).OrderBy(e => e.Timestamp).ToListAsync();
    }

    public async Task UpdateExchangeAmountAsync(int exchangeId, decimal amount)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync("UPDATE Exchanges SET Amount = ? WHERE Id = ?", amount, exchangeId);
    }

    public async Task DeleteExchangeAsync(int exchangeId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<Exchange>(exchangeId);
    }

    // ------------------------------------------------------------------ games

    public async Task<List<PlayedGame>> GetPlayedGamesAsync(int sessionId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PlayedGame>().Where(g => g.SessionId == sessionId).OrderBy(g => g.Starttime).ToListAsync();
    }

    public async Task<PlayedGame?> GetOpenPlayedGameAsync(int sessionId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PlayedGame>()
            .Where(g => g.SessionId == sessionId && g.Endtime == null)
            .OrderByDescending(g => g.Starttime)
            .FirstOrDefaultAsync();
    }

    public async Task<PlayedGame> StartGameAsync(int sessionId, int gameId)
    {
        var conn = await _db.GetConnectionAsync();
        var played = new PlayedGame { SessionId = sessionId, GameId = gameId, Amount = 0m, Starttime = TimeHelper.Now(), Endtime = null };
        await conn.InsertAsync(played);
        return played;
    }

    public async Task StopGameAsync(int playedGameId, decimal amount)
    {
        var conn = await _db.GetConnectionAsync();
        var played = await conn.FindAsync<PlayedGame>(playedGameId);
        if (played is null) return;
        played.Amount = amount;
        played.Endtime = Math.Max(TimeHelper.Now(), played.Starttime);
        await conn.UpdateAsync(played);
    }

    public async Task UpdatePlayedGameAmountAsync(int playedGameId, decimal amount)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync("UPDATE PlayedGames SET Amount = ? WHERE Id = ?", amount, playedGameId);
    }

    public async Task DeletePlayedGameAsync(int playedGameId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<PlayedGame>(playedGameId);
    }

    // ------------------------------------------------------------------ consumption

    public async Task<List<ConsumedMenuitem>> GetConsumedAsync(int sessionId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ConsumedMenuitem>().Where(c => c.SessionId == sessionId).OrderBy(c => c.Timestamp).ToListAsync();
    }

    public async Task<ConsumedMenuitem> AddConsumedAsync(int sessionId, int menuitemId)
    {
        var conn = await _db.GetConnectionAsync();

        // Snapshot the current price of the item at the session's casino. An item that is not on the
        // menu is snapshotted as 0 rather than null, so adding a price later cannot re-price the past.
        decimal? price = null;
        var session = await conn.FindAsync<Session>(sessionId);
        if (session is not null)
        {
            var casinoId = session.CasinoId;
            var link = await conn.Table<CasinoMenuitem>()
                .Where(p => p.CasinoId == casinoId && p.MenuitemId == menuitemId)
                .FirstOrDefaultAsync();
            price = link?.Price ?? 0m;
        }

        var consumed = new ConsumedMenuitem
        {
            SessionId = sessionId,
            MenuitemId = menuitemId,
            Timestamp = TimeHelper.Now(),
            Price = price,
        };
        await conn.InsertAsync(consumed);
        return consumed;
    }

    public async Task<bool> RemoveLastConsumedAsync(int sessionId, int menuitemId)
    {
        var conn = await _db.GetConnectionAsync();
        var last = await conn.Table<ConsumedMenuitem>()
            .Where(c => c.SessionId == sessionId && c.MenuitemId == menuitemId)
            .OrderByDescending(c => c.Timestamp)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync();
        if (last is null) return false;
        await conn.DeleteAsync(last);
        return true;
    }

    public async Task DeleteConsumedAsync(int consumedId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<ConsumedMenuitem>(consumedId);
    }
}
