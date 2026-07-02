using Content.Shared.Damage.Components;
using Content.Shared.Movement.Events;

namespace Content.Shared.Damage.Systems;

public sealed partial class StaminaSystem
{
    /// <summary>
    /// How much stamina is drained per second while sprinting.
    /// </summary>
    private const float SprintStaminaCost = 5f;

    private void InitializeSprint()
    {
        SubscribeLocalEvent<StaminaComponent, MoveInputEvent>(OnSprintMoveInput);
    }

    private void OnSprintMoveInput(Entity<StaminaComponent> entity, ref MoveInputEvent args)
    {
        var sprinting = args.Entity.Comp.Sprinting && args.HasDirectionalMovement;
        ToggleStaminaDrain(entity, SprintStaminaCost, sprinting, false, entity);
    }
}
