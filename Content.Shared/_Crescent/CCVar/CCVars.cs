using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Shared._Crescent.CCVar
{
    [CVarDefs]
    public sealed class RatCCVars : CVars
    {
        /// <summary>
        /// Включена ли автоматическая очистка мусора.
        /// </summary>
        public static readonly CVarDef<bool> TrashCleanupEnabled =
            CVarDef.Create("trash.cleanup_enabled", false, CVar.SERVERONLY);

        /// <summary>
        /// Интервал в секундах между очистками мусора.
        /// </summary>
        public static readonly CVarDef<float> TrashCleanupInterval =
            CVarDef.Create("trash.cleanup_interval", 60f, CVar.SERVERONLY);

        /// <summary>
        /// Задержка в секундах после начала раунда перед активацией очистки мусора.
        /// </summary>
        public static readonly CVarDef<float> TrashCleanupStartDelay =
            CVarDef.Create("trash.cleanup_start_delay", 60f, CVar.SERVERONLY);

        /// <summary>
        /// Включение/отключение автоматического удаления мелких гридов.
        /// </summary>
        public static readonly CVarDef<bool> AutoGridCleanupEnabled =
            CVarDef.Create("shuttle.grid_cleanup_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);
    }
}