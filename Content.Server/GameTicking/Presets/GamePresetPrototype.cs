
using Content.Server.Maps;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.GameTicking.Presets
{
    /// <summary>
    ///     A round-start setup preset, such as which antagonists to spawn.
    /// </summary>
    [Prototype("gamePreset")]
    public sealed partial class GamePresetPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField("alias")]
        public string[] Alias = Array.Empty<string>();

        [DataField("name")]
        public string ModeTitle = "????";

        [DataField("description")]
        public string Description = string.Empty;

        [DataField("showInVote")]
        public bool ShowInVote;

        [DataField("minPlayers")]
        public int? MinPlayers;

        [DataField("maxPlayers")]
        public int? MaxPlayers;

        [DataField("rules", customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
        public IReadOnlyList<string> Rules { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// If specified, the gamemode will only be run with these maps.
        /// If none are elligible, the global fallback will be used.
        /// </summary>
        [DataField("supportedMaps", customTypeSerializer: typeof(PrototypeIdSerializer<GameMapPoolPrototype>))]
        public string? MapPool;

        /// <summary>
        /// When set, character customization will only show these jobs.
        /// When empty/null, all jobs are shown (freeplay behavior).
        /// </summary>
        [DataField("availableJobs")]
        public List<ProtoId<JobPrototype>>? AvailableJobs;

        /// <summary>
        /// Jobs to hide from character customization even when availableJobs is not set.
        /// </summary>
        [DataField("excludedJobs")]
        public List<ProtoId<JobPrototype>>? ExcludedJobs;

        /// <summary>
        /// Custom round-start announcement message for this gamemode.
        /// When set, this replaces the default sector-wide announcement.
        /// </summary>
        [DataField("roundStartMessage")]
        public string? RoundStartMessage;

        /// <summary>
        /// Custom sender name for the round-start announcement.
        /// When set, this replaces the default announcer name (e.g. "Sector-Wide Announcement").
        /// </summary>
        [DataField("roundStartSender")]
        public string? RoundStartSender;
    }
}
