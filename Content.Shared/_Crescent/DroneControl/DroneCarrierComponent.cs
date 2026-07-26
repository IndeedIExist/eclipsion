using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.DroneControl;

/// <summary>
///     Marks an entity (a drone control console) as a drone carrier. Any <see cref="AutoDroneComponent"/>
///     drone docked to this console's grid is automatically deployed into formation and directed to engage
///     diplomatic enemies (War relation) that come within <see cref="EngagementRange"/>.
/// </summary>
[RegisterComponent]
public sealed partial class DroneCarrierComponent : Component
{
    /// <summary>
    ///     Maximum number of drones deployed at once. Also defines the formation slot layout size.
    /// </summary>
    [DataField]
    public int MaxDrones = 4;

    /// <summary>
    ///     Combat stance the drones adopt. Selected by the player from the console verbs.
    ///     Attack engages any enemy within <see cref="EngagementRange"/>; Defend only engages threats that
    ///     come within <see cref="DefendRange"/> of the carrier; Follow holds formation and never fires.
    /// </summary>
    [DataField]
    public DroneStance Stance = DroneStance.Attack;

    /// <summary>
    ///     Which ships the drones treat as targets. Enemies = only ships at War; All = every ship in range
    ///     except friendly (allied/same-faction) ones.
    /// </summary>
    [DataField]
    public DroneTargeting Targeting = DroneTargeting.Enemies;

    /// <summary>
    ///     Formation pattern the drones hold behind the carrier. Cycled at runtime via the console verb.
    /// </summary>
    [DataField]
    public DroneFormation Formation = DroneFormation.Arrow;

    /// <summary>
    ///     Lateral spacing between formation slots, in meters.
    /// </summary>
    [DataField]
    public float FormationSpacing = 26f;

    /// <summary>
    ///     Longitudinal spacing behind the carrier between formation rows, in meters (measured from the
    ///     carrier's rear edge). Kept large so slots sit well clear of the hull.
    /// </summary>
    [DataField]
    public float FormationDepth = 45f;

    /// <summary>
    ///     Extra clearance (meters) added to the carrier's radius. While a drone is closer than this to the
    ///     carrier it first flies straight out to open space before forming up, so it never grinds the hull.
    /// </summary>
    [DataField]
    public float LaunchClearance = 25f;

    /// <summary>
    ///     How close (meters) a drone must get to its slot to be considered holding formation.
    /// </summary>
    [DataField]
    public float FormationRange = 8f;

    /// <summary>
    ///     Enemies within this range (meters) of the carrier get engaged in the Attack stance. Drones return
    ///     to formation once no enemy remains in range (carrier-leashed engagement).
    /// </summary>
    [DataField]
    public float EngagementRange = 2000f;

    /// <summary>
    ///     Engagement range (meters) used in the Defend stance: only threats that get this close to the
    ///     carrier are engaged, keeping the drones tight to the carrier.
    /// </summary>
    [DataField]
    public float DefendRange = 600f;

    /// <summary>
    ///     Orbit distance (meters) drones keep from an engaged enemy while firing.
    /// </summary>
    [DataField]
    public float OrbitRange = 250f;

    /// <summary>
    ///     Radius (meters) in which an unclaimed drone of the carrier's own faction is picked up even if it is
    ///     not docked. A shipyard-bought drone only docks if a matching port happened to be free, so it often
    ///     just arrives floating next to the hull; without this it would never join a formation.
    /// </summary>
    [DataField]
    public float ClaimRange = 1500f;

    /// <summary>
    ///     Currently deployed drones (their control-server uids) keyed by formation slot index.
    /// </summary>
    [ViewVariables]
    public Dictionary<int, EntityUid> Slots = new();

    /// <summary>
    ///     Lifetime count of drones this carrier has fielded. This is the hard production limit: it never
    ///     decreases, so a destroyed drone does NOT free up capacity - once <see cref="MaxDrones"/> have been
    ///     produced, no more can be made.
    /// </summary>
    [ViewVariables]
    public int ProducedCount;

    /// <summary>
    ///     Vessel prototype IDs this console can produce, shown for selection in the UI. Set per faction
    ///     (e.g. DSM console: ValorDrone / RocinanteDrone).
    /// </summary>
    [DataField]
    public List<string> SpawnableDrones = new();

    /// <summary>
    ///     Radar/IFF colour applied to produced drones so they show in the faction's colour instead of the
    ///     default gold a bare grid gets.
    /// </summary>
    [DataField]
    public Color IffColor = Color.Gold;

    /// <summary>
    ///     Spawn times of drones produced but not yet claimed into a slot, used to gate production against the
    ///     limit while a fresh drone is still FTL-docking. Entries expire on their own. Runtime state.
    /// </summary>
    [ViewVariables]
    public List<TimeSpan> PendingSpawns = new();

    /// <summary>
    ///     Shared focus target all this carrier's drones concentrate fire on. Re-selected when the current
    ///     one dies, powers down, or leaves range; drones only switch targets once it is gone (focus fire).
    /// </summary>
    [ViewVariables]
    public EntityUid? FocusTarget;
}

[Serializable, NetSerializable]
public enum DroneTargeting : byte
{
    /// <summary>Only ships at War (diplomatic enemies).</summary>
    Enemies,

    /// <summary>Every ship in range except friendly (allied/same-faction) ones.</summary>
    All,
}

[Serializable, NetSerializable]
public enum DroneStance : byte
{
    /// <summary>Engage any War enemy within EngagementRange.</summary>
    Attack,

    /// <summary>Hold formation, only engage threats that come within DefendRange of the carrier.</summary>
    Defend,

    /// <summary>Hold formation and never fire; purely follow the carrier.</summary>
    Follow,
}

[Serializable, NetSerializable]
public enum DroneFormation : byte
{
    /// <summary>Trailing V behind the carrier ("ok"): pairs off the rear-left and rear-right.</summary>
    Arrow,

    /// <summary>Spread out abreast directly behind the carrier, side by side.</summary>
    LineAbreast,

    /// <summary>Single file directly astern.</summary>
    Column,

    /// <summary>Diagonal staircase off one side.</summary>
    Echelon,

    /// <summary>Box arranged around a point behind the carrier.</summary>
    Diamond,
}
