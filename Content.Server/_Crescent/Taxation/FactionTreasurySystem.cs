using System.Text.Json;
using Content.Shared.GameTicking;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server._Crescent.Taxation;

/// <summary>
/// Cross-round persistence for faction treasury balances. Station entities (and their
/// <c>StationTradeMarketComponent</c>) are recreated every round, so the accumulated treasury would
/// otherwise reset to zero. This system keeps an authoritative per-faction balance in memory (it
/// survives round restarts because entity systems live for the whole server process) and mirrors it
/// to a JSON file under the server's user-data directory so it also survives full server restarts.
/// </summary>
public sealed class FactionTreasurySystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _res = default!;

    private static readonly ResPath SavePath = new("faction_treasury.json");

    /// <summary>How often, at most, the in-memory balances are flushed to disk while dirty.</summary>
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(10);

    private readonly Dictionary<string, int> _balances = new();
    private bool _dirty;
    private float _sinceSave;

    public override void Initialize()
    {
        base.Initialize();

        // Flush on round cleanup so a round's earnings survive even if the process is killed shortly after.
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Save());

        Load();
    }

    /// <summary>Current persisted balance for a faction (0 if the faction has never banked anything).</summary>
    public int Get(string faction)
    {
        return string.IsNullOrEmpty(faction) ? 0 : _balances.GetValueOrDefault(faction);
    }

    /// <summary>Records the latest balance for a faction. Debounced to disk via <see cref="Update"/>.</summary>
    public void Set(string faction, int value)
    {
        if (string.IsNullOrEmpty(faction))
            return;

        if (_balances.TryGetValue(faction, out var current) && current == value)
            return;

        _balances[faction] = value;
        _dirty = true;
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

            var loaded = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            if (loaded is null)
                return;

            _balances.Clear();
            foreach (var (faction, balance) in loaded)
                _balances[faction] = balance;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load faction treasury balances: {e}");
        }
    }

    private void Save()
    {
        _sinceSave = 0f;
        if (!_dirty)
            return;

        try
        {
            var json = JsonSerializer.Serialize(_balances);
            _res.UserData.WriteAllText(SavePath, json);
            _dirty = false;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save faction treasury balances: {e}");
        }
    }
}
