using Content.Server.GameTicking.Events;
using Content.Shared._Crescent.SingleLifeJob;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Crescent.SingleLifeJob;

public sealed class SingleLifeJobSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly Dictionary<NetUserId, HashSet<ProtoId<JobPrototype>>> _playedJobs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<IsJobAllowedEvent>(OnIsJobAllowed);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _playedJobs.Clear();
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null)
            return;

        if (!_prototype.TryIndex<JobPrototype>(args.JobId, out var job) || !job.SingleLifeRound)
            return;

        var jobProtoId = new ProtoId<JobPrototype>(args.JobId);
        _playedJobs.GetOrNew(args.Player.UserId).Add(jobProtoId);

        RaiseNetworkEvent(new SingleLifeJobPlayedEvent(args.JobId), args.Player.Channel);
    }

    private void OnIsJobAllowed(ref IsJobAllowedEvent ev)
    {
        if (_playedJobs.TryGetValue(ev.Player.UserId, out var jobs) && jobs.Contains(ev.JobId))
            ev.Cancelled = true;
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        if (_playedJobs.TryGetValue(ev.Player.UserId, out var jobs))
            ev.Jobs.UnionWith(jobs);
    }
}