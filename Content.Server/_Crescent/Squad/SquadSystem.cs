using System.Linq;
using Content.Shared._Crescent.HullrotFaction;
using Content.Shared._Crescent.Squad;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.Server._Crescent.Squad;

/// <summary>
/// Система для управления отрядами.
/// </summary>
public sealed class SquadSystem : EntitySystem
{
    // Храним список всех отрядов по фракциям: Faction -> (SquadId -> SquadInfo)
    private readonly Dictionary<string, Dictionary<int, SquadInfo>> _squadsByFaction = new();
    private int _nextSquadId = 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _squadsByFaction.Clear();
        _nextSquadId = 1;
    }

    /// <summary>
    /// Создать новый отряд.
    /// </summary>
    public bool CreateSquad(string faction, string squadName)
    {
        if (string.IsNullOrWhiteSpace(faction) || string.IsNullOrWhiteSpace(squadName))
            return false;

        if (!_squadsByFaction.ContainsKey(faction))
        {
            _squadsByFaction[faction] = new Dictionary<int, SquadInfo>();
        }

        var squadId = _nextSquadId++;

        _squadsByFaction[faction][squadId] = new SquadInfo(squadId, squadName);
        return true;
    }

    /// <summary>
    /// Удалить отряд.
    /// </summary>
    public bool RemoveSquad(string faction, int squadId)
    {
        if (!_squadsByFaction.ContainsKey(faction))
            return false;

        if (!_squadsByFaction[faction].Remove(squadId))
            return false;

        var toRemove = new List<EntityUid>();
        var query = AllEntityQuery<SquadComponent>();
        while (query.MoveNext(out var uid, out var squadComp))
        {
            if (squadComp.SquadId == squadId)
                toRemove.Add(uid);
        }

        foreach (var uid in toRemove)
        {
            RemComp<SquadComponent>(uid);
        }

        return true;
    }

    /// <summary>
    /// Назначить сущность в отряд.
    /// </summary>
    public bool AssignToSquad(EntityUid entity, int squadId, string faction)
    {
        if (!_squadsByFaction.TryGetValue(faction, out var factionSquads))
            return false;

        if (!factionSquads.TryGetValue(squadId, out var squadInfo))
            return false;

        if (!TryComp<HullrotFactionComponent>(entity, out var factionComp) ||
            factionComp.Faction != faction)
            return false;

        var squadComp = EnsureComp<SquadComponent>(entity);
        squadComp.SquadId = squadId;
        squadComp.SquadName = squadInfo.Name;
        Dirty(entity, squadComp, MetaData(entity));
        return true;
    }

    /// <summary>
    /// Удалить сущность из отряда.
    /// </summary>
    public void RemoveFromSquad(EntityUid entity)
    {
        RemComp<SquadComponent>(entity);
    }

    /// <summary>
    /// Получить список всех отрядов фракции.
    /// </summary>
    public IReadOnlyDictionary<int, SquadInfo> GetFactionSquads(string faction)
    {
        if (!_squadsByFaction.ContainsKey(faction))
            return new Dictionary<int, SquadInfo>();

        return new Dictionary<int, SquadInfo>(_squadsByFaction[faction]);
    }

    /// <summary>
    /// Получить количество членов в отряде.
    /// </summary>
    public int GetSquadMemberCount(int squadId)
    {
        var count = 0;
        var query = EntityQueryEnumerator<SquadComponent>();
        while (query.MoveNext(out var uid, out var squadComp))
        {
            if (squadComp.SquadId == squadId)
                count++;
        }

        return count;
    }
}

/// <summary>
/// Информация об отряде.
/// </summary>
public sealed record SquadInfo(int Id, string Name);
