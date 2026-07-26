using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Reflect;

public sealed class DeflectMasterySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ParryComponent, ParrySuccessEvent>(OnParrySuccess);
    }

    private void OnParrySuccess(EntityUid uid, ParryComponent comp, ParrySuccessEvent ev)
    {
        if (!_melee.TryGetWeapon(ev.Parrier, out var weaponUid, out _))
            return;

        if (!TryComp<DeflectMasteryComponent>(weaponUid, out var mastery))
            return;

        var bonus = ev.IsPerfect ? mastery.BonusPerPerfectParry : mastery.BonusPerBlock;
        mastery.CurrentBonus = MathF.Min(mastery.CurrentBonus + bonus, mastery.MaxBonus);
        mastery.LastStackTime = _timing.CurTime;
        Dirty(weaponUid, mastery);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<DeflectMasteryComponent>();

        while (query.MoveNext(out var uid, out var mastery))
        {
            if (mastery.CurrentBonus <= 0f)
                continue;

            var elapsed = (curTime - mastery.LastStackTime).TotalSeconds;
            if (elapsed < mastery.DecayDelay)
                continue;

            mastery.CurrentBonus = MathF.Max(0f, mastery.CurrentBonus - mastery.DecayRate * frameTime);
            Dirty(uid, mastery);
        }
    }
}
