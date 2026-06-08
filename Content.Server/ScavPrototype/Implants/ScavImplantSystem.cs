using Content.Server.Temperature.Components;
using Content.Server.Radiation.Components;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.ScavPrototype.Implants;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.ScavPrototype.Implants;

public sealed class ScavImplantSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly MobThresholdSystem _thresholdSystem = default!;
    [Dependency] private readonly WoundSystem _woundSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly HungerSystem _hungerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScavImplantComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ScavImplantComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ScavImplantComponent, OpenScavImplantEvent>(OnImplantActivate);

        Subs.BuiEvents<ScavImplantComponent>(ScavImplantUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
        });
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ScavImplantComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.NextUpdate > _timing.CurTime)
                continue;

            if (component.ScannedEntity is not { } patient)
                continue;

            if (Deleted(patient))
            {
                component.ScannedEntity = null;
                continue;
            }

            component.NextUpdate = _timing.CurTime + component.UpdateInterval;
            UpdateScannedUser(uid, patient, component);
        }
    }

    private void OnInserted(EntityUid uid, ScavImplantComponent component, EntGotInsertedIntoContainerMessage args)
    {
        var owner = GetImplantedEntity(uid);
        if (owner == null)
            return;

        component.ImplantedEntity = owner;
        EnsureAction(owner.Value, uid, component);
    }

    private void OnRemoved(EntityUid uid, ScavImplantComponent component, EntGotRemovedFromContainerMessage args)
    {
        if (component.ImplantedEntity != null)
        {
            _actionsSystem.RemoveProvidedActions(component.ImplantedEntity.Value, uid);
            component.ImplantedEntity = null;
        }
    }

    private void OnImplantActivate(Entity<ScavImplantComponent> ent, ref OpenScavImplantEvent args)
    {
        if (args.Handled)
            return;

        var uiOpen = _uiSystem.IsUiOpen(ent.Owner, ScavImplantUiKey.Key, args.Performer);

        if (uiOpen)
        {
            _uiSystem.CloseUi(ent.Owner, ScavImplantUiKey.Key, args.Performer);
        }
        else
        {
            _uiSystem.OpenUi(ent.Owner, ScavImplantUiKey.Key, args.Performer);
            ent.Comp.ScannedEntity = args.Performer;
            UpdateScannedUser(ent.Owner, args.Performer, ent.Comp);
        }

        args.Handled = true;
    }

    private void OnUiClosed(Entity<ScavImplantComponent> ent, ref BoundUIClosedEvent args)
    {
        ent.Comp.ScannedEntity = null;
    }

    private EntityUid? GetImplantedEntity(EntityUid implant)
    {
        if (TryComp<SubdermalImplantComponent>(implant, out var subdermal) && subdermal.ImplantedEntity != null)
        {
            return subdermal.ImplantedEntity;
        }

        if (_containerSystem.TryGetContainingContainer(implant, out var container))
        {
            var owner = container.Owner;
            if (TryComp<BodyPartComponent>(owner, out var part) && part.Body != null)
            {
                return part.Body;
            }
            if (HasComp<MobStateComponent>(owner))
            {
                return owner;
            }
        }

        return null;
    }

    private void EnsureAction(EntityUid user, EntityUid implant, ScavImplantComponent component)
    {
        if (!string.IsNullOrWhiteSpace(component.ImplantAction))
        {
            _actionsSystem.AddAction(user, ref component.Action, component.ImplantAction, implant);
        }
    }

    public void UpdateScannedUser(EntityUid implant, EntityUid target, ScavImplantComponent component)
    {
        if (!_uiSystem.HasUi(implant, ScavImplantUiKey.Key))
            return;

        var speciesName = Loc.GetString("health-analyzer-window-entity-unknown-species-text");
        var age = 18;
        var height = 154;

        if (TryComp<HumanoidAppearanceComponent>(target, out var humanoid))
        {
            age = humanoid.Age;
            var speciesId = humanoid.Species;
            if (_prototypeManager.TryIndex<SpeciesPrototype>(speciesId, out var speciesProto))
            {
                speciesName = Loc.GetString(speciesProto.Name);
                
                var seed = (int) target;
                var rand = new System.Random(seed);
                height = rand.Next(speciesProto.MinScavHeight, speciesProto.MaxScavHeight + 1);
            }
        }

        // Brain health
        var brainHealth = 1.0f;
        if (TryComp<BodyComponent>(target, out var body))
        {
            foreach (var (organId, organComp) in _bodySystem.GetBodyOrgans(target))
            {
                if (organComp.SlotId == "brain")
                {
                    brainHealth = organComp.IntegrityCap > 0 ? (float)organComp.OrganIntegrity / (float)organComp.IntegrityCap : 0f;
                    break;
                }
            }
        }

        // Blood & Bleeding
        var bloodAmount = 1.0f;
        var isBleeding = false;
        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            if (_solutionContainerSystem.ResolveSolution(target, bloodstream.BloodSolutionName,
                    ref bloodstream.BloodSolution, out var bloodSolution))
            {
                bloodAmount = bloodSolution.FillFraction;
            }
            isBleeding = bloodstream.BleedAmount > 0f || bloodstream.BleedAmountFromWounds > 0f || bloodstream.BleedAmountNotFromWounds > 0f;
        }

        // Overall health percentage until Dead threshold
        var deadThreshold = 200f; // fallback
        if (_thresholdSystem.TryGetDeadThreshold(target, out var threshold))
        {
            deadThreshold = (float) threshold.Value;
        }
        var currentDamage = 0f;
        if (TryComp<DamageableComponent>(target, out var damageable))
        {
            currentDamage = (float) _thresholdSystem.CheckVitalDamage(target, damageable);
        }
        var healthPercent = deadThreshold > 0 ? (deadThreshold - currentDamage) / deadThreshold : 0f;
        healthPercent = Math.Clamp(healthPercent * 100f, 0f, 100f);

        // Pain
        var painPercent = 0f;
        if (TryComp<NerveSystemComponent>(target, out var nerveSystem))
        {
            painPercent = nerveSystem.PainCap > 0 ? (float)(nerveSystem.Pain / nerveSystem.PainCap) : 0f;
            painPercent = Math.Clamp(painPercent * 100f, 0f, 100f);
        }

        // Temperature (Celsius)
        var tempCelsius = 36.6f;
        if (TryComp<TemperatureComponent>(target, out var temp))
        {
            tempCelsius = temp.CurrentTemperature - 273.15f;
        }

        // Radiation
        var rads = 0f;
        if (TryComp<RadiationReceiverComponent>(target, out var radReceiver))
        {
            rads = radReceiver.CurrentRadiation;
        }

        // Hunger & Thirst
        var hungerPercent = 1.0f;
        if (TryComp<HungerComponent>(target, out var hunger))
        {
            var currentHunger = _hungerSystem.GetHunger(hunger);
            var hungerThresholds = hunger.Thresholds;
            var maxHunger = hungerThresholds.TryGetValue(HungerThreshold.Overfed, out var val) ? val : 250.0f;
            hungerPercent = maxHunger > 0 ? currentHunger / maxHunger : 0f;
        }

        var thirstPercent = 1.0f;
        if (TryComp<ThirstComponent>(target, out var thirst))
        {
            var currentThirst = thirst.CurrentThirst;
            var thirstThresholds = thirst.ThirstThresholds;
            var maxThirst = thirstThresholds.TryGetValue(ThirstThreshold.OverHydrated, out var val) ? val : 850.0f;
            thirstPercent = maxThirst > 0 ? currentThirst / maxThirst : 0f;
        }

        // Pulse (Dynamic baseline altered by damage and chemicals)
        var pulse = 75;
        var randPulse = new System.Random();
        pulse += randPulse.Next(-2, 3); // minor random jitter

        // Increase pulse based on damage ratio
        var damageRatio = healthPercent < 100f ? (100f - healthPercent) / 100f : 0f;
        pulse += (int)(damageRatio * 40f);

        // Stimulants modifier
        var stimulantBonus = 0f;
        if (bloodstream != null &&
            _solutionContainerSystem.ResolveSolution(target, bloodstream.ChemicalSolutionName,
                ref bloodstream.ChemicalSolution, out var chemicalSolution))
        {
            foreach (var reagent in chemicalSolution.Contents)
            {
                if (reagent.Reagent.Prototype == "Adrenaline")
                    stimulantBonus += (float)reagent.Quantity * 3f;
                else if (reagent.Reagent.Prototype == "Epinephrine")
                    stimulantBonus += (float)reagent.Quantity * 2f;
            }
        }
        pulse += (int)stimulantBonus;
        pulse = Math.Clamp(pulse, 0, 220);

        // Body part status & bleeding dictionaries for the doll
        var bodyParts = _woundSystem.GetDamageableStatesOnBody(target);
        var bleedingParts = new Dictionary<TargetBodyPart, bool>();
        if (TryComp<BodyComponent>(target, out var targetBody) && targetBody.RootContainer.ContainedEntity is { } rootPart)
        {
            foreach (var (woundable, woundableComp) in _woundSystem.GetAllWoundableChildren(rootPart))
            {
                var targetPart = _bodySystem.GetTargetBodyPart(woundable);
                if (bleedingParts.TryGetValue(targetPart, out var existing))
                    bleedingParts[targetPart] = existing || woundableComp.Bleeds > 0;
                else
                    bleedingParts[targetPart] = woundableComp.Bleeds > 0;
            }
        }

        var entityName = target.ToString();
        if (TryComp<MetaDataComponent>(target, out var meta))
        {
            entityName = meta.EntityName;
        }

        // Send state to UI
        var msg = new ScavImplantUpdateMessage(
            speciesName,
            height,
            age,
            brainHealth,
            bloodAmount,
            isBleeding,
            healthPercent,
            painPercent,
            tempCelsius,
            rads,
            hungerPercent,
            thirstPercent,
            pulse,
            bodyParts,
            bleedingParts,
            entityName,
            GetNetEntity(target)
        );

        _uiSystem.SetUiState(implant, ScavImplantUiKey.Key, msg);
    }
}
