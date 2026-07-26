using Content.Shared.Examine;

namespace Content.Shared._Crescent.Cloning;

public sealed class CloneOrganExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CloneOrganComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, CloneOrganComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("clone-organ-examine-line-1"));
        args.PushMarkup(Loc.GetString("clone-organ-examine-line-2"));
    }
}
