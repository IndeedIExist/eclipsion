using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared._Crescent.SignalTransmitter;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.SignalTransmitter;

public sealed class SignalTransmitterDeviceSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly SoundPathSpecifier ActivateSound = new("/Audio/Effects/double_beep.ogg");

    /// <summary>
    /// Tracks the global timer state. Only one transmitter timer can be active at a time.
    /// </summary>
    private bool _timerActive;
    private TimeSpan _timerEnd;
    private bool _reminderSent;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SignalTransmitterDeviceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SignalTransmitterDeviceComponent, BoundUIOpenedEvent>(OnUiOpen);
        SubscribeLocalEvent<SignalTransmitterDeviceComponent, TransmitterKeypadMessage>(OnKeypad);
        SubscribeLocalEvent<SignalTransmitterDeviceComponent, TransmitterKeypadClearMessage>(OnClear);
        SubscribeLocalEvent<SignalTransmitterDeviceComponent, TransmitterKeypadEnterMessage>(OnEnter);
    }

    private void OnMapInit(EntityUid uid, SignalTransmitterDeviceComponent comp, MapInitEvent args)
    {
        comp.Code = GenerateCode(comp.CodeLength);
    }

    private string GenerateCode(int length)
    {
        var code = string.Empty;
        for (var i = 0; i < length; i++)
        {
            code += _random.Next(0, 10).ToString();
        }
        return code;
    }

    private void OnUiOpen(EntityUid uid, SignalTransmitterDeviceComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, comp);
    }

    private void OnKeypad(EntityUid uid, SignalTransmitterDeviceComponent comp, TransmitterKeypadMessage args)
    {
        if (comp.Activated)
            return;

        if (comp.EnteredCode.Length >= comp.CodeLength)
            return;

        comp.EnteredCode += args.Value.ToString();
        UpdateUi(uid, comp);
    }

    private void OnClear(EntityUid uid, SignalTransmitterDeviceComponent comp, TransmitterKeypadClearMessage args)
    {
        if (comp.Activated)
            return;

        comp.EnteredCode = string.Empty;
        UpdateUi(uid, comp);
    }

    private void OnEnter(EntityUid uid, SignalTransmitterDeviceComponent comp, TransmitterKeypadEnterMessage args)
    {
        if (comp.Activated)
            return;

        if (comp.EnteredCode != comp.Code)
        {
            _popup.PopupEntity(Loc.GetString("transmitter-wrong-code"), uid, PopupType.MediumCaution);
            comp.EnteredCode = string.Empty;
            UpdateUi(uid, comp);
            return;
        }

        if (_timerActive)
        {
            _popup.PopupEntity(Loc.GetString("transmitter-timer-already-active"), uid, PopupType.MediumCaution);
            return;
        }

        var xform = Transform(uid);
        if (!xform.Anchored)
        {
            _popup.PopupEntity(Loc.GetString("transmitter-not-anchored"), uid, PopupType.MediumCaution);
            return;
        }

        // Activate!
        comp.Activated = true;
        _timerActive = true;
        _timerEnd = _timing.CurTime + TimeSpan.FromMinutes(comp.TimerMinutes);
        _reminderSent = false;

        _audio.PlayPvs(ActivateSound, uid, AudioParams.Default.WithVolume(4f));

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("transmitter-activated-announcement", ("minutes", comp.TimerMinutes)),
            Loc.GetString("transmitter-sender"),
            true,
            colorOverride: Color.Red
        );

        UpdateUi(uid, comp);
    }

    private void UpdateUi(EntityUid uid, SignalTransmitterDeviceComponent comp)
    {
        var xform = Transform(uid);
        var state = new SignalTransmitterUiState(
            comp.Activated,
            comp.EnteredCode.Length,
            comp.CodeLength,
            xform.Anchored);

        _ui.SetUiState(uid, SignalTransmitterUiKey.Key, state);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timerActive)
            return;

        var remaining = _timerEnd - _timing.CurTime;

        if (remaining <= TimeSpan.Zero)
        {
            _timerActive = false;
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("transmitter-timer-expired"),
                Loc.GetString("transmitter-sender"),
                true,
                colorOverride: Color.Red
            );
            return;
        }

        if (!_reminderSent && remaining <= TimeSpan.FromMinutes(5))
        {
            _reminderSent = true;
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("transmitter-reminder-announcement", ("minutes", 5)),
                Loc.GetString("transmitter-sender"),
                true,
                colorOverride: Color.Orange
            );
        }
    }
}
