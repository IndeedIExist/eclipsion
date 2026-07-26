using Content.Shared._Crescent.SingleLifeJob;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Customization.Systems;

[UsedImplicitly, Serializable, NetSerializable]
public sealed partial class CharacterSingleLifeJob : CharacterRequirement
{
    public override bool IsValid(
        JobPrototype job,
        HumanoidCharacterProfile profile,
        Dictionary<string, TimeSpan> playTimes,
        bool whitelisted,
        IPrototype prototype,
        IEntityManager entityManager,
        IPrototypeManager prototypeManager,
        IConfigurationManager configManager,
        out string? reason,
        int depth = 0,
        MindComponent? mind = null)
    {
        reason = Loc.GetString("character-not-played-this-round-requirement");

        if (!entityManager.EntitySysManager.TryGetEntitySystem(out SingleLifeJobTrackerSystem? tracker))
            return true;

        return !tracker.HasPlayedThisRound(job.ID);
    }
}