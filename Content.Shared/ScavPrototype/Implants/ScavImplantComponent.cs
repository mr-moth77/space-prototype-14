using Content.Shared.Actions;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.ScavPrototype.Implants;

/// <summary>
/// Component for Scav Health Monitor implant.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ScavImplantComponent : Component
{
    /// <summary>
    /// The action to open the Scav Health Monitor UI.
    /// </summary>
    [DataField("implantAction")]
    public string? ImplantAction = "ActionOpenScavImplant";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    /// <summary>
    /// The entity this implant is inside of.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? ImplantedEntity;

    /// <summary>
    /// The entity whose data is currently being monitored/scanned.
    /// </summary>
    [ViewVariables]
    public EntityUid? ScannedEntity;

    /// <summary>
    /// The time when the next update should be sent.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextUpdate;

    /// <summary>
    /// Interval at which updates are sent when UI is open.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Event raised to trigger opening the Scav Health Monitor UI.
/// </summary>
public sealed partial class OpenScavImplantEvent : InstantActionEvent;

/// <summary>
/// UI Key for the Scav Health Monitor.
/// </summary>
[Serializable, NetSerializable]
public enum ScavImplantUiKey : byte
{
    Key,
}

/// <summary>
/// State sent from server to client to update the Scav health interface.
/// </summary>
[Serializable, NetSerializable]
public sealed class ScavImplantUpdateMessage : BoundUserInterfaceState
{
    public string SpeciesName;
    public int Height;
    public int Age;
    public float BrainHealth;
    public float BloodAmount;
    public bool IsBleeding;
    public float HealthPercent;
    public float PainPercent;
    public float Temperature;
    public float Radiation;
    public float HungerPercent;
    public float ThirstPercent;
    public int Pulse;
    public Dictionary<TargetBodyPart, WoundableSeverity> BodyParts;
    public Dictionary<TargetBodyPart, bool> BleedingParts;
    public string EntityName;
    public NetEntity TargetEntity;

    public ScavImplantUpdateMessage(
        string speciesName,
        int height,
        int age,
        float brainHealth,
        float bloodAmount,
        bool isBleeding,
        float healthPercent,
        float painPercent,
        float temperature,
        float radiation,
        float hungerPercent,
        float thirstPercent,
        int pulse,
        Dictionary<TargetBodyPart, WoundableSeverity> bodyParts,
        Dictionary<TargetBodyPart, bool> bleedingParts,
        string entityName,
        NetEntity targetEntity)
    {
        SpeciesName = speciesName;
        Height = height;
        Age = age;
        BrainHealth = brainHealth;
        BloodAmount = bloodAmount;
        IsBleeding = isBleeding;
        HealthPercent = healthPercent;
        PainPercent = painPercent;
        Temperature = temperature;
        Radiation = radiation;
        HungerPercent = hungerPercent;
        ThirstPercent = thirstPercent;
        Pulse = pulse;
        BodyParts = bodyParts;
        BleedingParts = bleedingParts;
        EntityName = entityName;
        TargetEntity = targetEntity;
    }
}
