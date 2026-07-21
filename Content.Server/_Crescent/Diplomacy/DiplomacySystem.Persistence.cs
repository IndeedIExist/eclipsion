using System.Linq;
using System.Text.Json;
using Content.Shared._Crescent.Diplomacy;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server._Crescent.Diplomacy;

/// <summary>
/// Cross-round persistence for the IFF relation matrix.
///
/// The matrix lives on an entity that is respawned every <see cref="Content.Shared.GameTicking.RoundStartedEvent"/>,
/// so it was rebuilt from <see cref="DiplomacyPrototype"/> defaults each round and every admin-set relation
/// evaporated at round end. Only the deltas are saved — a pair that was never touched keeps following its
/// prototype, so editing the YAML defaults still takes effect instead of being shadowed by an old save.
/// </summary>
public sealed partial class DiplomacySystem
{
    [Dependency] private readonly IResourceManager _res = default!;

    private static readonly ResPath SavePath = new("/iff_diplomacy.json");
    private const int SaveVersion = 1;

    /// <summary>Relations set at runtime, keyed by ordered pair. These override the prototype defaults.</summary>
    private readonly Dictionary<string, Relations> _overrides = new();

    private static string PairKey(string a, string b)
        => string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

    private void LoadOverrides()
    {
        try
        {
            if (!_res.UserData.TryReadAllText(SavePath, out var json))
                return;

            var saved = JsonSerializer.Deserialize<IffDiplomacySaveData>(json);
            if (saved is null || saved.Version != SaveVersion)
                return;

            _overrides.Clear();
            foreach (var (key, relation) in saved.Relations)
            {
                if (Enum.TryParse<Relations>(relation, out var parsed))
                    _overrides[key] = parsed;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load IFF diplomacy: {e}");
        }
    }

    /// <summary>Replays the saved overrides onto a freshly built matrix. Factions that no longer exist are dropped.</summary>
    private void ApplyOverrides(DiplomacyComponent component)
    {
        foreach (var (key, relation) in _overrides.ToList())
        {
            var pair = key.Split('|');
            if (pair.Length != 2 ||
                !component.DiplomacyIndicies.ContainsKey(pair[0]) ||
                !component.DiplomacyIndicies.ContainsKey(pair[1]))
            {
                _overrides.Remove(key);
                continue;
            }

            ChangeRelation(pair[0], pair[1], relation, component, true);
        }
    }

    private void Save()
    {
        try
        {
            var data = new IffDiplomacySaveData { Version = SaveVersion };
            foreach (var (key, relation) in _overrides)
                data.Relations[key] = relation.ToString();

            _res.UserData.WriteAllText(SavePath, JsonSerializer.Serialize(data));
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save IFF diplomacy: {e}");
        }
    }

    /// <summary>
    /// Drops every carried-over override and puts the live matrix back to its prototype defaults.
    /// Admin use only.
    /// </summary>
    public void ResetOverrides()
    {
        _overrides.Clear();
        Save();

        if (TryComp<DiplomacyComponent>(_diplomacyEntity, out var diplo))
        {
            BuildDefaults(diplo);
            HandleDiplomacyChanged();
        }
    }
}

internal sealed class IffDiplomacySaveData
{
    public int Version { get; set; }

    /// <summary>"FactionA|FactionB" (ordered) to relation name.</summary>
    public Dictionary<string, string> Relations { get; set; } = new();
}
