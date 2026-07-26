using System.Text.Json;
using Content.Server.Preferences.Managers;
using Content.Shared.Bank.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Crescent.Economy;

/// <summary>
/// Cross-round persistence for player stock holdings.
///
/// Bank balances already survive rounds because they live on the character profile
/// (see <c>BankSystem.OnMindAdded</c>), but shares live on the mob and would evaporate at round end.
/// That combination would quietly delete money: buy shares, round ends, the credits are gone and
/// nothing came back. So holdings are keyed and persisted the same way the bank is — per character,
/// not per player — and reloaded when a mind is attached to a body.
///
/// Trade history is deliberately not persisted: its timestamps are round-relative and would read as
/// nonsense next round.
/// </summary>
public sealed class StockPortfolioPersistenceSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly StockWarLiquidationSystem _liquidation = default!;

    private static readonly ResPath SavePath = new("/stock_portfolios.json");
    private const int SaveVersion = 1;

    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, StoredPortfolio> _portfolios = new();

    /// <summary>
    /// The storage key each live body was spawned under, captured at mind-attach.
    ///
    /// Resolving the key on demand would read <see cref="PlayerPreferences.SelectedCharacter"/> as it stands
    /// at that moment, which is not necessarily the character the body belongs to: a player sitting in the
    /// lobby between rounds can pick a different slot before the round-restart flush runs, and the flush would
    /// then file this mob's shares under that other character and overwrite its holdings. Pinning the key when
    /// the body is claimed ties the shares to the character that actually earned them.
    /// </summary>
    private readonly Dictionary<EntityUid, string> _mobKeys = new();

    private bool _dirty;
    private float _sinceSave;

    public override void Initialize()
    {
        base.Initialize();

        // Keyed off the humanoid rather than the bank account: the engine allows only one subscriber per
        // (component, event) pair, and BankSystem already owns BankAccountComponent + MindAddedMessage.
        SubscribeLocalEvent<HumanoidAppearanceComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        Load();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        // Mobs are about to be deleted; capture anything that has not been written back yet.
        var query = EntityQueryEnumerator<PlayerStockPortfolioComponent>();
        while (query.MoveNext(out var uid, out var portfolio))
            Store(uid, portfolio);

        Save();

        // The bodies these keys belong to are about to be deleted.
        _mobKeys.Clear();
    }

    private void OnMindAdded(EntityUid uid, HumanoidAppearanceComponent humanoid, ref MindAddedMessage args)
    {
        // Shares are only ever bought against a bank account, so a body without one has nothing to restore.
        if (!HasComp<BankAccountComponent>(uid))
            return;

        if (args.Mind.Comp.UserId is not { } userId)
            return;

        if (!TryGetKey(userId, out var key))
            return;

        // Pinned even when there is nothing to restore, so shares bought later this round are still filed
        // against the character that bought them rather than whatever slot is selected at flush time.
        _mobKeys[uid] = key;

        if (!_portfolios.TryGetValue(key, out var stored) || stored.Shares.Count == 0)
            return;

        var portfolio = EnsureComp<PlayerStockPortfolioComponent>(uid);
        portfolio.OwnedShares.Clear();
        foreach (var (companyId, amount) in stored.Shares)
        {
            if (amount > 0)
                portfolio.OwnedShares[companyId] = amount;
        }

        portfolio.TotalInvested = stored.TotalInvested;
        Dirty(uid, portfolio);

        // Restore first, then take back anything the player should not still be holding. Holdings outlive
        // the round they were bought in, so a war declared while they were away is only enforceable here.
        _liquidation.LiquidateEnemyHoldings(uid, portfolio);
    }

    /// <summary>
    /// Writes a mob's current holdings into the persistent store. Call this after any trade — the
    /// component itself is not the source of truth once the round ends.
    /// </summary>
    public void Store(EntityUid mob, PlayerStockPortfolioComponent? portfolio = null)
    {
        if (!Resolve(mob, ref portfolio, false))
            return;

        // Prefer the key pinned when this body was claimed; fall back to the live selection only for a body
        // that somehow never went through OnMindAdded.
        if (!_mobKeys.TryGetValue(mob, out var key))
        {
            if (!_players.TryGetSessionByEntity(mob, out var session))
                return;

            if (!TryGetKey(session.UserId, out key))
                return;

            _mobKeys[mob] = key;
        }

        var stored = new StoredPortfolio
        {
            Shares = new Dictionary<string, int>(portfolio.OwnedShares),
            TotalInvested = portfolio.TotalInvested,
        };

        _portfolios[key] = stored;
        _dirty = true;
    }

    /// <summary>Drops every stored holding. Admin use only.</summary>
    public void ClearAll()
    {
        _portfolios.Clear();

        var query = EntityQueryEnumerator<PlayerStockPortfolioComponent>();
        while (query.MoveNext(out var uid, out var portfolio))
        {
            portfolio.OwnedShares.Clear();
            portfolio.TotalInvested = 0;
            portfolio.TradeHistory.Clear();
            Dirty(uid, portfolio);
        }

        _dirty = true;
        Save();
    }

    /// <summary>
    /// Holdings belong to a character, not an account, so the key matches how the bank picks its
    /// profile slot. A player's trader and their miner keep separate books.
    /// </summary>
    private bool TryGetKey(NetUserId userId, out string key)
    {
        key = string.Empty;

        var prefs = _prefs.GetPreferences(userId);
        var character = prefs.SelectedCharacter;
        if (character is not HumanoidCharacterProfile)
            return false;

        key = $"{userId}:{prefs.IndexOfCharacter(character)}";
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_dirty)
            return;

        _sinceSave += frameTime;
        if (_sinceSave >= SaveInterval.TotalSeconds)
            Save();
    }

    private void Load()
    {
        try
        {
            if (!_res.UserData.TryReadAllText(SavePath, out var json))
                return;

            var saved = JsonSerializer.Deserialize<StockPortfolioSaveData>(json);
            if (saved is null || saved.Version != SaveVersion)
                return;

            _portfolios.Clear();
            foreach (var (key, stored) in saved.Portfolios)
                _portfolios[key] = stored;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load stock portfolios: {e}");
        }
    }

    private void Save()
    {
        _sinceSave = 0f;
        if (!_dirty)
            return;

        try
        {
            var data = new StockPortfolioSaveData
            {
                Version = SaveVersion,
                Portfolios = new Dictionary<string, StoredPortfolio>(_portfolios),
            };

            _res.UserData.WriteAllText(SavePath, JsonSerializer.Serialize(data));
            _dirty = false;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save stock portfolios: {e}");
        }
    }
}

public sealed class StoredPortfolio
{
    public Dictionary<string, int> Shares { get; set; } = new();
    public double TotalInvested { get; set; }
}

internal sealed class StockPortfolioSaveData
{
    public int Version { get; set; }
    public Dictionary<string, StoredPortfolio> Portfolios { get; set; } = new();
}
