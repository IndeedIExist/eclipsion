using System.Threading;
using System.Threading.Tasks;
using Content.Server.Language;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Language;
using Content.Shared.Language.Components;
using Content.Shared._Art.CVars;
using Content.Shared._Art.TTS;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Art.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly LanguageSystem _language = default!;

    private const int MaxMessageChars = 200;
    private bool _isEnabled;


    public override void Initialize()
    {
        _cfg.OnValueChanged(ArtCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke, after: [typeof(RadioSystem), typeof(HeadsetSystem)]); // Art-TTS

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _ttsManager.ResetCache());
        SubscribeLocalEvent<ActorComponent, TTSRadioPlayEvent>(OnTTSRadioPlayEvent);

        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        if (!_isEnabled || args.Message.Length > MaxMessageChars)
            return;

        if (args.RadioMessageSent)
            return;

        if (!args.Language.SpeechOverride.RequireSpeech)
            return;

        var voiceId = component.VoicePrototype;
        // var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        // RaiseLocalEvent(uid, voiceEv);
        // voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex(voiceId, out var protoVoice))
            return;

        if (args.IsWhisper)
        {
            HandleWhisper(uid, args.Message, args.Language, protoVoice.Speaker);
            return;
        }

        HandleSay(uid, args.Message, args.Language, protoVoice.Speaker);
    }

    private void OnTTSRadioPlayEvent(EntityUid uid, ActorComponent comp, TTSRadioPlayEvent args)
    {
        if (!_isEnabled || args.Message.Length > MaxMessageChars)
            return;

        HandleReceiveRadio(uid, args.Message, args.Language, args.Voice);
    }

    private async void HandleReceiveRadio(EntityUid uid, string message, LanguagePrototype language, string speaker)
    {
        // This fires once per listening headset, so it must only reach that listener. Broadcasting to
        // everyone in PVS made a single radio line play once per nearby listener, all overlapping.
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        TryComp<LanguageSpeakerComponent>(uid, out var lang);
        if (!_language.CanUnderstand((uid, lang), language.ID))
            return;

        var soundData = await GenerateTTS(message, speaker, "radio");
        if (soundData is null)
            return;

        // The request was awaited, so the listener may be gone by now.
        if (Deleted(uid) || !TryComp(uid, out actor))
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid)), Filter.SinglePlayer(actor.PlayerSession));
    }

    private async void HandleSay(EntityUid uid, string message, LanguagePrototype language, string speaker)
    {
        var normal = await GenerateTTS(message, speaker);
        if (normal is null)
            return;

        // var obfuscated = await GenerateTTS(_language.ObfuscateSpeech(message, language), speaker);
        // if (obfuscated is null)
        //     return;

        // The speaker can disconnect or be deleted while the API request is in flight.
        if (Deleted(uid))
            return;

        var nilter = Filter.Empty();
        var lilter = Filter.Empty();
        foreach (var session in Filter.Pvs(uid).Recipients)
        {
            if (!session.AttachedEntity.HasValue)
                continue;

            EntityManager.TryGetComponent(session.AttachedEntity.Value, out LanguageSpeakerComponent? lang);
            if (_language.CanUnderstand(new(session.AttachedEntity.Value, lang), language.ID))
                nilter.AddPlayer(session);
            else
                lilter.AddPlayer(session);
        }

        RaiseNetworkEvent(new PlayTTSEvent(normal, GetNetEntity(uid)), nilter);
        // RaiseNetworkEvent(new PlayTTSEvent(obfuscated, GetNetEntity(uid)), lilter, false);
    }

    private async void HandleWhisper(EntityUid uid, string message, LanguagePrototype language, string speaker)
    {
        var normal = await GenerateTTS(message, speaker);
        if (normal is null)
            return;

        // var obfuscated = await GenerateTTS(message, speaker);
        // if (obfuscated is null)
        //     return;

        // The speaker can disconnect or be deleted while the API request is in flight. GetComponent below
        // throws rather than returning null, and this is an async void, so the throw would go unhandled.
        if (!TryComp<TransformComponent>(uid, out var sourceXform))
            return;

        // TODO: Check obstacles
        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourcePos = _xforms.GetWorldPosition(sourceXform, xformQuery);
        var nilter = Filter.Empty();
        var lilter = Filter.Empty();
        foreach (var session in Filter.Pvs(uid).Recipients)
        {
            if (!session.AttachedEntity.HasValue)
                continue;

            if (!xformQuery.TryGetComponent(session.AttachedEntity.Value, out var xform))
                continue;

            var distance = (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).Length();
            if (distance > ChatSystem.WhisperMuffledRange)
                continue;

            EntityManager.TryGetComponent(session.AttachedEntity.Value, out LanguageSpeakerComponent? lang);
            if (_language.CanUnderstand(new(session.AttachedEntity.Value, lang), language.ID)
                && distance <= ChatSystem.WhisperClearRange)
                nilter.AddPlayer(session);
            else
                lilter.AddPlayer(session);
        }

        RaiseNetworkEvent(new PlayTTSEvent(normal, GetNetEntity(uid), true), nilter);
        // RaiseNetworkEvent(new PlayTTSEvent(obfuscated, GetNetEntity(uid), true), lilter, false);
    }

    private readonly Dictionary<string, Task<byte[]?>> _ttsTasks = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    // ReSharper disable once InconsistentNaming
    private async Task<byte[]?> GenerateTTS(string text, string speaker, string? effect = null)
    {
        var textSanitized = Sanitize(text);
        if (string.IsNullOrEmpty(textSanitized))
            return null;

        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        return await _ttsManager.ConvertTextToSpeech(speaker, textSanitized, effect);
        // var taskKey = $"{textSanitized}_{speaker}_{effect}";

        // await _lock.WaitAsync();
        // try
        // {
        //     if (_ttsTasks.TryGetValue(taskKey, out var existingTask))
        //         return await existingTask;

        //     var newTask = _ttsManager.ConvertTextToSpeech(speaker, textSanitized);
        //     _ttsTasks[taskKey] = newTask;
        // }
        // finally
        // {
        //     _lock.Release();
        // }

        // try
        // {
        //     return await _ttsTasks[taskKey];
        // }
        // finally
        // {
        //     await _lock.WaitAsync();
        //     try
        //     {
        //         _ttsTasks.Remove(taskKey);
        //     }
        //     finally
        //     {
        //         _lock.Release();
        //     }
        // }
    }
}
