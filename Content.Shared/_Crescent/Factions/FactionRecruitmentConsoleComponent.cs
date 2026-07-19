using System;
using Content.Shared.DoAfter;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Factions;

/// <summary>
/// A faction-owned muster console. An authorized member of the console's faction walks up, picks a nearby
/// person and one of the faction's roles, and the console enlists (or promotes/reassigns) that person: it sets
/// their faction membership (<see cref="HullrotFaction.HullrotFactionComponent"/>, which every faction-aware
/// system reads live) and rewrites their held ID card's title/icon/department/access to the chosen role. It can
/// also dismiss a member, clearing their faction membership.
///
/// Unlike an ID card console the target is a nearby <em>person</em> (their body carries faction membership; a
/// card in a slot does not), so recruitment reaches diplomacy, squads, payroll and the chat name prefix at once.
/// The role list and faction are server-authored; the client is fed display options through the BUI state, so
/// this component is not itself networked.
/// </summary>
[RegisterComponent]
public sealed partial class FactionRecruitmentConsoleComponent : Component
{
    /// <summary>The <see cref="FactionPrototype"/> id this console recruits into. Written to every recruit.</summary>
    [DataField(required: true)]
    public string Faction = default!;

    /// <summary>The roles this console can assign, in display order. Each supplies a recruit's title/icon/access.</summary>
    [DataField(required: true)]
    public List<ProtoId<JobPrototype>> Roles = new();

    /// <summary>How close (in tiles) a person must be to the console to appear in the recruitable list.</summary>
    [DataField]
    public float Range = 3f;

    /// <summary>
    /// How long the operator must remain at the console before a recruitment is applied. A short delay keeps a
    /// role change (and its ID rewrite) from being spammed instantly, easing server load.
    /// </summary>
    [DataField]
    public TimeSpan AssignDelay = TimeSpan.FromSeconds(2.5);
}

[Serializable, NetSerializable]
public enum FactionRecruitmentUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class FactionRecruitmentConsoleState : BoundUserInterfaceState
{
    public readonly string FactionName;
    public readonly List<FactionRecruitmentOption> Options;
    public readonly List<FactionRecruitmentTarget> Targets;

    public FactionRecruitmentConsoleState(string factionName, List<FactionRecruitmentOption> options, List<FactionRecruitmentTarget> targets)
    {
        FactionName = factionName;
        Options = options;
        Targets = targets;
    }
}

/// <summary>A single assignable role as shown to the client.</summary>
[Serializable, NetSerializable]
public sealed class FactionRecruitmentOption
{
    public string JobId = default!;
    public string RoleName = default!;
    public string Description = string.Empty;
}

/// <summary>A nearby person the console can act on, as shown to the client.</summary>
[Serializable, NetSerializable]
public sealed class FactionRecruitmentTarget
{
    public NetEntity Entity;
    public string Name = default!;

    /// <summary>Localized name of the person's current faction, or empty if they belong to none.</summary>
    public string CurrentFactionName = string.Empty;

    /// <summary>Whether the person is already a member of this console's faction.</summary>
    public bool IsMember;
}

/// <summary>Enlist, promote or reassign the target to a role of this console's faction.</summary>
[Serializable, NetSerializable]
public sealed class FactionRecruitmentAssignMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Target;
    public readonly string JobId;

    public FactionRecruitmentAssignMessage(NetEntity target, string jobId)
    {
        Target = target;
        JobId = jobId;
    }
}

/// <summary>Remove the target from this console's faction (clears their faction membership).</summary>
[Serializable, NetSerializable]
public sealed class FactionRecruitmentDismissMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Target;

    public FactionRecruitmentDismissMessage(NetEntity target)
    {
        Target = target;
    }
}

/// <summary>Ask the server to re-scan for nearby people and resend the state (the roster is not live-tracked).</summary>
[Serializable, NetSerializable]
public sealed class FactionRecruitmentRefreshMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Runs while the operator stands at the console before a recruitment is committed. Carries the chosen role so
/// the same assignment can be re-validated and applied once the delay elapses.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class FactionRecruitmentAssignDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string JobId = string.Empty;

    public FactionRecruitmentAssignDoAfterEvent()
    {
    }

    public FactionRecruitmentAssignDoAfterEvent(string jobId)
    {
        JobId = jobId;
    }

    public override DoAfterEvent Clone() => this;
}
