using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Shared._Crescent.CCVar
{
    [CVarDefs]
    public sealed class RatCCVars : CVars
    {
        /// <summary>
        /// Whether automatic debris cleanup is enabled.
        /// </summary>
        public static readonly CVarDef<bool> TrashCleanupEnabled =
            CVarDef.Create("trash.cleanup_enabled", false, CVar.SERVERONLY);

        /// <summary>
        /// Interval between debris cleanups, in seconds.
        /// </summary>
        public static readonly CVarDef<float> TrashCleanupInterval =
            CVarDef.Create("trash.cleanup_interval", 60f, CVar.SERVERONLY);

        /// <summary>
        /// Delay after round start before debris cleanup switches on, in seconds.
        /// </summary>
        public static readonly CVarDef<float> TrashCleanupStartDelay =
            CVarDef.Create("trash.cleanup_start_delay", 60f, CVar.SERVERONLY);

        /// <summary>
        /// Enables/disables automatic deletion of small grids.
        /// </summary>
        public static readonly CVarDef<bool> AutoGridCleanupEnabled =
            CVarDef.Create("shuttle.grid_cleanup_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);
    }
}