using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Crescent.Weapons;

/// <summary>
/// Configures per-weapon camera shake magnitude when the local player fires this gun.
/// Add this component to a gun entity in YAML to enable/tune the shake.
/// Higher magnitude = more shake. Typical range: 0.03 (pistol) to 0.15 (heavy).
/// </summary>
[RegisterComponent]
public sealed partial class GunCameraShakeComponent : Component
{
    /// <summary>
    /// Camera displacement in world units per shot.
    /// Direction is randomised each shot — never directional, never nauseating.
    /// </summary>
    [DataField]
    public float Magnitude { get; set; } = 0.06f;
}
