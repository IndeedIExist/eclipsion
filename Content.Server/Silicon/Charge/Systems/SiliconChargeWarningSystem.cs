using Content.Shared.Popups;
using Content.Shared.Silicon.Components;
using Content.Shared.Silicon.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Silicon.Charge;

/// <summary>
///     Emits a warning alarm to a Silicon's own player as its charge drops past
///     configured thresholds. Each threshold fires once and re-arms when the Silicon
///     recharges back above it. The alarm is heard only by the Silicon itself.
/// </summary>
public sealed class SiliconChargeWarningSystem : EntitySystem
{
    [Dependency] private readonly SiliconChargeSystem _siliconCharge = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconChargeWarningComponent, SiliconChargeStateUpdateEvent>(OnChargeStateUpdate);
    }

    private void OnChargeStateUpdate(EntityUid uid, SiliconChargeWarningComponent comp, SiliconChargeStateUpdateEvent args)
    {
        if (comp.Thresholds.Count == 0
            || !_siliconCharge.TryGetSiliconBattery(uid, out var battery)
            || battery.MaxCharge <= 0)
            return;

        var fraction = battery.CurrentCharge / battery.MaxCharge;

        // Find the most urgent (lowest) threshold newly crossed this update, while marking
        // every crossed threshold as triggered so a single large drain won't stack alarms.
        SiliconChargeWarningThreshold? toWarn = null;

        foreach (var threshold in comp.Thresholds)
        {
            if (fraction <= threshold.Percent)
            {
                if (comp.Triggered.Add(threshold.Percent)
                    && (toWarn == null || threshold.Percent < toWarn.Percent))
                    toWarn = threshold;
            }
            else
            {
                // Recharged back above the threshold: re-arm it for next time.
                comp.Triggered.Remove(threshold.Percent);
            }
        }

        if (toWarn != null)
            Warn(uid, toWarn);
    }

    private void Warn(EntityUid uid, SiliconChargeWarningThreshold threshold)
    {
        if (threshold.PopUp is { } popup)
            _popup.PopupEntity(Loc.GetString(popup), uid, uid);

        // recipient == uid == the Silicon, so only its own player hears the alarm.
        if (threshold.Sound != null)
            _audio.PlayEntity(threshold.Sound, uid, uid);
    }
}
