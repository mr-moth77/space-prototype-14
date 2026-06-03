using Robust.Shared.GameStates;

namespace Content.Shared._DV.Carrying;

[RegisterComponent, NetworkedComponent]
public sealed partial class CantCarryOthersComponent : Component
{
    // Marker component: when present on an entity, that entity cannot carry other entities.
}
