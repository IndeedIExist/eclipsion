using Content.Shared._Crescent.Factions;
using Content.Shared._Crescent.HullrotFaction;
using Content.Shared.Construction;
using JetBrains.Annotations;

namespace Content.Server.Construction.Completions
{
    /// <summary>
    /// Stamps the constructing player's faction onto the finished machine.
    ///
    /// Without this a machine inherits the faction of whatever grid it was built on, which let anyone place a
    /// lathe on a station and reach that station's research server. Faction membership lives on the mob, so
    /// the builder is the only reliable source of ownership at construction time.
    /// </summary>
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class SetFactionFromUser : IGraphAction
    {
        public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
        {
            // No user means this wasn't player-built (admin spawn, map load), so leave the grid fallback alone.
            if (userUid is not { } user)
                return;

            var faction = entityManager.TryGetComponent<HullrotFactionComponent>(user, out var member)
                ? member.Faction
                : string.Empty;

            entityManager.System<FactionMachineSystem>().SetFaction(uid, faction);
        }
    }
}
