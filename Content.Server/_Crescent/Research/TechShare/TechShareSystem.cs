using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared._Crescent.Research.TechShare;
using Content.Shared.Lathe;
using Content.Shared.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.Research.TechShare;

public sealed class TechShareSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Requester events
        SubscribeLocalEvent<TechShareRequestComponent, TechShareSendRequestMessage>(OnSendRequest);
        SubscribeLocalEvent<TechShareRequestComponent, TechShareCancelRequestMessage>(OnCancelRequest);
        SubscribeLocalEvent<TechShareRequestComponent, TechShareDisconnectRequestMessage>(OnDisconnectRequest);
        SubscribeLocalEvent<TechShareRequestComponent, BoundUIOpenedEvent>(OnRequesterUiOpen);
        SubscribeLocalEvent<TechShareRequestComponent, ComponentShutdown>(OnRequesterShutdown);

        // Receiver events
        SubscribeLocalEvent<TechShareReceiverComponent, TechShareAcceptMessage>(OnAccept);
        SubscribeLocalEvent<TechShareReceiverComponent, TechShareRejectMessage>(OnReject);
        SubscribeLocalEvent<TechShareReceiverComponent, TechShareDisconnectReceiverMessage>(OnDisconnectReceiver);
        SubscribeLocalEvent<TechShareReceiverComponent, BoundUIOpenedEvent>(OnReceiverUiOpen);
        SubscribeLocalEvent<TechShareReceiverComponent, ComponentShutdown>(OnReceiverShutdown);

        SubscribeLocalEvent<LatheProduceCompleteEvent>(OnLatheProductionComplete);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TechShareRequestComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ConnectionExpiry == null || comp.ConnectedReceiver == null)
                continue;

            if (now >= comp.ConnectionExpiry.Value)
            {
                Disconnect(uid, comp);
            }
        }
    }

    #region Requester Logic

    private void OnSendRequest(EntityUid uid, TechShareRequestComponent comp, TechShareSendRequestMessage args)
    {
        if (!_power.IsPowered(uid))
            return;

        if (comp.ConnectedReceiver != null || comp.PendingTarget != null)
            return;

        if (args.SelectedRecipes.Count == 0)
            return;

        var target = GetEntity(args.TargetReceiver);
        if (!TryComp<TechShareReceiverComponent>(target, out var receiver))
            return;

        if (!_power.IsPowered(target))
            return;

        // Receiver already busy
        if (receiver.ConnectedRequester != null || receiver.PendingRequester != null)
            return;

        // Validate selected recipes against what's actually available
        var validRecipes = GetAvailableRecipes(uid);
        var selectedValid = new List<string>();
        foreach (var r in args.SelectedRecipes)
        {
            if (validRecipes.Contains(r))
                selectedValid.Add(r);
        }

        if (selectedValid.Count == 0)
            return;

        comp.SelectedRecipes = selectedValid;
        comp.RentalMode = args.RentalMode;
        comp.PendingTarget = target;
        receiver.PendingRequester = uid;
        receiver.RentalMode = args.RentalMode;

        // Clamp and set value based on mode
        if (args.RentalMode == TechShareRentalMode.Production)
        {
            var count = Math.Clamp(args.DurationOrCount, comp.MinProductionCount, comp.MaxProductionCount);
            comp.PendingProductionCount = count;
        }
        else
        {
            var durationMin = Math.Clamp(args.DurationOrCount, comp.MinDurationMinutes, comp.MaxDurationMinutes);
            comp.PendingDurationSeconds = durationMin * 60f;
        }

        Dirty(uid, comp);
        Dirty(target, receiver);

        UpdateRequesterUi(uid, comp);
        UpdateReceiverUi(target, receiver);
    }

    private void OnCancelRequest(EntityUid uid, TechShareRequestComponent comp, TechShareCancelRequestMessage args)
    {
        if (comp.PendingTarget is not { } target)
            return;

        comp.PendingTarget = null;
        Dirty(uid, comp);

        if (TryComp<TechShareReceiverComponent>(target, out var receiver) && receiver.PendingRequester == uid)
        {
            receiver.PendingRequester = null;
            Dirty(target, receiver);
            UpdateReceiverUi(target, receiver);
        }

        UpdateRequesterUi(uid, comp);
    }

    private void OnDisconnectRequest(EntityUid uid, TechShareRequestComponent comp, TechShareDisconnectRequestMessage args)
    {
        Disconnect(uid, comp);
    }

    private void OnRequesterShutdown(EntityUid uid, TechShareRequestComponent comp, ComponentShutdown args)
    {
        if (comp.ConnectedReceiver != null)
            Disconnect(uid, comp);
        else if (comp.PendingTarget != null)
            CancelPending(uid, comp);
    }

    private void OnRequesterUiOpen(EntityUid uid, TechShareRequestComponent comp, BoundUIOpenedEvent args)
    {
        UpdateRequesterUi(uid, comp);
    }

    #endregion

    #region Receiver Logic

    private void OnAccept(EntityUid uid, TechShareReceiverComponent receiver, TechShareAcceptMessage args)
    {
        if (!_power.IsPowered(uid))
            return;

        if (receiver.PendingRequester is not { } requesterUid)
            return;

        if (!TryComp<TechShareRequestComponent>(requesterUid, out var requester))
            return;

        if (!_power.IsPowered(requesterUid))
            return;

        // Establish connection
        receiver.PendingRequester = null;
        receiver.ConnectedRequester = requesterUid;

        requester.PendingTarget = null;
        requester.ConnectedReceiver = uid;

        // Set rental mode and expiry/counts
        if (requester.RentalMode == TechShareRentalMode.Production)
        {
            // Production-based rental: set initial production counts
            receiver.RemainingProductionCounts.Clear();
            foreach (var recipe in requester.SelectedRecipes)
            {
                receiver.RemainingProductionCounts[recipe] = requester.PendingProductionCount;
            }
        }
        else
        {
            // Time-based rental: set expiry time
            requester.ConnectionExpiry = _timing.CurTime + TimeSpan.FromSeconds(requester.PendingDurationSeconds);
        }

        // Share only selected technologies
        ShareTechnologies(requesterUid, uid, receiver, requester.SelectedRecipes);

        Dirty(uid, receiver);
        Dirty(requesterUid, requester);

        UpdateRequesterUi(requesterUid, requester);
        UpdateReceiverUi(uid, receiver);
    }

    private void OnReject(EntityUid uid, TechShareReceiverComponent receiver, TechShareRejectMessage args)
    {
        if (receiver.PendingRequester is not { } requesterUid)
            return;

        receiver.PendingRequester = null;
        Dirty(uid, receiver);

        if (TryComp<TechShareRequestComponent>(requesterUid, out var requester) && requester.PendingTarget == uid)
        {
            requester.PendingTarget = null;
            Dirty(requesterUid, requester);
            UpdateRequesterUi(requesterUid, requester);
        }

        UpdateReceiverUi(uid, receiver);
    }

    private void OnDisconnectReceiver(EntityUid uid, TechShareReceiverComponent receiver, TechShareDisconnectReceiverMessage args)
    {
        if (receiver.ConnectedRequester is not { } requesterUid)
            return;

        if (TryComp<TechShareRequestComponent>(requesterUid, out var requester))
            Disconnect(requesterUid, requester);
    }

    private void OnReceiverShutdown(EntityUid uid, TechShareReceiverComponent receiver, ComponentShutdown args)
    {
        if (receiver.ConnectedRequester is not { } requesterUid)
            return;

        if (TryComp<TechShareRequestComponent>(requesterUid, out var requester))
            Disconnect(requesterUid, requester);
    }

    private void OnReceiverUiOpen(EntityUid uid, TechShareReceiverComponent receiver, BoundUIOpenedEvent args)
    {
        UpdateReceiverUi(uid, receiver);
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// Shares selected recipes from requester's RnD server to receiver's RnD server.
    /// </summary>
    private void ShareTechnologies(EntityUid requesterUid, EntityUid receiverUid, TechShareReceiverComponent receiver, List<string> selectedRecipes)
    {
        // Find receiver's RnD server
        if (!TryComp<ResearchClientComponent>(receiverUid, out var rcvClient) || rcvClient.Server is not { } rcvServer)
            return;

        if (!TryComp<TechnologyDatabaseComponent>(rcvServer, out var rcvDb))
            return;

        receiver.SharedRecipes.Clear();

        // Snapshot receiver's current recipes before sharing
        var existingRecipes = new HashSet<string>(rcvDb.UnlockedRecipes);

        // Add recipes
        foreach (var recipe in selectedRecipes)
        {
            if (existingRecipes.Contains(recipe))
                continue;

            _research.AddLatheRecipe(rcvServer, recipe, rcvDb);
            receiver.SharedRecipes.Add(recipe);
        }

        Dirty(rcvServer, rcvDb);
    }

    /// <summary>
    /// Gets recipes available on the requester's RnD server.
    /// </summary>
    private HashSet<string> GetAvailableRecipes(EntityUid requesterUid)
    {
        if (!TryComp<ResearchClientComponent>(requesterUid, out var reqClient) || reqClient.Server is not { } reqServer)
            return new HashSet<string>();

        if (!TryComp<TechnologyDatabaseComponent>(reqServer, out var reqDb))
            return new HashSet<string>();

        return new HashSet<string>(reqDb.UnlockedRecipes);
    }

    /// <summary>
    /// Removes shared recipes from receiver's RnD server.
    /// </summary>
    private void UnshareTechnologies(EntityUid receiverUid, TechShareReceiverComponent receiver)
    {
        if (!TryComp<ResearchClientComponent>(receiverUid, out var rcvClient) || rcvClient.Server is not { } rcvServer)
            return;

        if (!TryComp<TechnologyDatabaseComponent>(rcvServer, out var rcvDb))
            return;

        foreach (var recipe in receiver.SharedRecipes)
        {
            _research.RemoveLatheRecipe(rcvServer, recipe, rcvDb);
        }

        receiver.SharedRecipes.Clear();
        Dirty(rcvServer, rcvDb);
    }

    private void Disconnect(EntityUid requesterUid, TechShareRequestComponent requester)
    {
        var receiverUid = requester.ConnectedReceiver;
        requester.ConnectedReceiver = null;
        requester.ConnectionExpiry = null;
        requester.PendingTarget = null;
        Dirty(requesterUid, requester);

        if (receiverUid != null && TryComp<TechShareReceiverComponent>(receiverUid.Value, out var receiver))
        {
            UnshareTechnologies(receiverUid.Value, receiver);
            receiver.ConnectedRequester = null;
            Dirty(receiverUid.Value, receiver);
            UpdateReceiverUi(receiverUid.Value, receiver);
        }

        UpdateRequesterUi(requesterUid, requester);
    }

    private void CancelPending(EntityUid requesterUid, TechShareRequestComponent requester)
    {
        if (requester.PendingTarget is { } target)
        {
            if (TryComp<TechShareReceiverComponent>(target, out var receiver) && receiver.PendingRequester == requesterUid)
            {
                receiver.PendingRequester = null;
                Dirty(target, receiver);
                UpdateReceiverUi(target, receiver);
            }
        }

        requester.PendingTarget = null;
        Dirty(requesterUid, requester);
        UpdateRequesterUi(requesterUid, requester);
    }

    #endregion

    #region UI Updates

    private void UpdateRequesterUi(EntityUid uid, TechShareRequestComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        var receivers = new List<(NetEntity, string)>();
        var receiverQuery = EntityQueryEnumerator<TechShareReceiverComponent>();
        while (receiverQuery.MoveNext(out var rcvUid, out var rcv))
        {
            // Don't list busy receivers (unless it's our current target)
            if (rcv.ConnectedRequester != null && rcv.ConnectedRequester != uid)
                continue;
            if (rcv.PendingRequester != null && rcv.PendingRequester != uid)
                continue;

            receivers.Add((GetNetEntity(rcvUid), rcv.ConsoleName));
        }

        // Get available recipes from our RnD server
        var availableRecipes = new List<string>(GetAvailableRecipes(uid));

        TimeSpan? remaining = null;
        if (comp.ConnectionExpiry != null)
        {
            remaining = comp.ConnectionExpiry.Value - _timing.CurTime;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;
        }

        string? connectedName = null;
        if (comp.ConnectedReceiver != null && TryComp<TechShareReceiverComponent>(comp.ConnectedReceiver.Value, out var connRcv))
            connectedName = connRcv.ConsoleName;

        var state = new TechShareRequestBuiState(
            receivers,
            availableRecipes,
            comp.PendingTarget != null ? GetNetEntity(comp.PendingTarget.Value) : null,
            comp.ConnectedReceiver != null ? GetNetEntity(comp.ConnectedReceiver.Value) : null,
            connectedName,
            remaining,
            comp.ConnectedReceiver != null ? comp.SelectedRecipes : null,
            comp.MinDurationMinutes,
            comp.MaxDurationMinutes,
            comp.MinProductionCount,
            comp.MaxProductionCount);

        _uiSystem.SetUiState(uid, TechShareRequestUiKey.Key, state);
    }

    private void UpdateReceiverUi(EntityUid uid, TechShareReceiverComponent? receiver = null)
    {
        if (!Resolve(uid, ref receiver))
            return;

        string? pendingName = null;
        List<string>? offeredRecipes = null;
        int? offeredDuration = null;
        if (receiver.PendingRequester != null && TryComp<TechShareRequestComponent>(receiver.PendingRequester.Value, out var pendReq))
        {
            pendingName = pendReq.ConsoleName;
            offeredRecipes = pendReq.SelectedRecipes;
            offeredDuration = (int)(pendReq.PendingDurationSeconds / 60f);
        }

        string? connectedName = null;
        TimeSpan? remaining = null;
        if (receiver.ConnectedRequester != null && TryComp<TechShareRequestComponent>(receiver.ConnectedRequester.Value, out var connReq))
        {
            connectedName = connReq.ConsoleName;
            offeredRecipes = connReq.SelectedRecipes;
            if (connReq.ConnectionExpiry != null)
            {
                remaining = connReq.ConnectionExpiry.Value - _timing.CurTime;
                if (remaining < TimeSpan.Zero)
                    remaining = TimeSpan.Zero;
            }
        }

        var state = new TechShareReceiverBuiState(
            receiver.PendingRequester != null ? GetNetEntity(receiver.PendingRequester.Value) : null,
            pendingName,
            receiver.ConnectedRequester != null ? GetNetEntity(receiver.ConnectedRequester.Value) : null,
            connectedName,
            remaining,
            offeredRecipes,
            offeredDuration,
            receiver.RentalMode,
            receiver.RemainingProductionCounts.Count > 0 ? new Dictionary<string, int>(receiver.RemainingProductionCounts) : null);

        _uiSystem.SetUiState(uid, TechShareReceiverUiKey.Key, state);
    }

    private void OnLatheProductionComplete(LatheProduceCompleteEvent args)
    {
        // Check if lathe has a TechnologyDatabaseComponent
        if (!TryComp<TechnologyDatabaseComponent>(args.Lathe, out var latheDb))
            return;

        // Find all receivers connected to this lathe's RnD server
        var receiverQuery = EntityQueryEnumerator<TechShareReceiverComponent, ResearchClientComponent>();
        while (receiverQuery.MoveNext(out var receiverUid, out var receiver, out var rcvClient))
        {
            if (receiver.RentalMode != TechShareRentalMode.Production)
                continue;

            if (receiver.ConnectedRequester == null)
                continue;

            // Check if this receiver's RnD server matches the lathe's database
            if (rcvClient.Server is not { } rcvServer)
                continue;

            if (!TryComp<TechnologyDatabaseComponent>(rcvServer, out var rcvDb))
                continue;

            // Check if the lathe's database is the same as the receiver's server's database
            if (latheDb != rcvDb)
                continue;

            // Check if the produced recipe is in the shared list
            if (!receiver.SharedRecipes.Contains(args.Recipe.ID))
                continue;

            // Decrement production count
            if (receiver.RemainingProductionCounts.TryGetValue(args.Recipe.ID, out var count))
            {
                count--;
                if (count <= 0)
                {
                    // Remove recipe
                    receiver.RemainingProductionCounts.Remove(args.Recipe.ID);
                    receiver.SharedRecipes.Remove(args.Recipe.ID);

                    // Remove from RnD server
                    _research.RemoveLatheRecipe(rcvServer, args.Recipe.ID, rcvDb);
                }
                else
                {
                    receiver.RemainingProductionCounts[args.Recipe.ID] = count;
                }

                Dirty(receiverUid, receiver);
                UpdateReceiverUi(receiverUid, receiver);

                // Check if all recipes are exhausted
                if (receiver.SharedRecipes.Count == 0)
                {
                    // Disconnect - remove from RnD server first
                    foreach (var recipe in receiver.SharedRecipes)
                    {
                        _research.RemoveLatheRecipe(rcvServer, recipe, rcvDb);
                    }

                    // Clear connection
                    receiver.SharedRecipes.Clear();
                    receiver.RemainingProductionCounts.Clear();
                    receiver.RentalMode = TechShareRentalMode.Time;

                    if (receiver.ConnectedRequester is { } requesterUid && TryComp<TechShareRequestComponent>(requesterUid, out var requester))
                    {
                        requester.ConnectedReceiver = null;
                        requester.ConnectionExpiry = null;
                        requester.SelectedRecipes.Clear();
                        Dirty(requesterUid, requester);
                        UpdateRequesterUi(requesterUid, requester);
                    }

                    receiver.ConnectedRequester = null;
                    Dirty(receiverUid, receiver);
                    UpdateReceiverUi(receiverUid, receiver);
                }
            }
        }
    }

    #endregion
}
