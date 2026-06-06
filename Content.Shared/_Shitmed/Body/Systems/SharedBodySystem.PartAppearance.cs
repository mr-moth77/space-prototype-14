// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 SX-7 <sn1.test.preria.2002@gmail.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body.Systems;
public partial class SharedBodySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly INetManager _net = default!;

    private void InitializePartAppearances()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartAppearanceComponent, ComponentStartup>(OnPartAppearanceStartup);
        SubscribeLocalEvent<BodyPartAppearanceComponent, AfterAutoHandleStateEvent>(HandleState);
        SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(OnPartAttachedToBody);
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnPartDroppedFromBody);
    }

    private void OnPartAppearanceStartup(EntityUid uid, BodyPartAppearanceComponent component, ComponentStartup args)
    {
        // scav edit start
        if (!TryComp(uid, out BodyPartComponent? part))
            return;

        // Use the visLayers value from YAML if set (e.g. LowLArm), otherwise fall back to ToHumanoidLayers()
        var resolvedLayer = component.Type ?? part.ToHumanoidLayers();
        if (resolvedLayer is not { } relevantLayer)
            return;
        // scav edit end

        if (part.BaseLayerId != null)
        {
            component.ID = part.BaseLayerId;
            component.Type = relevantLayer;
            return;
        }

        if (part.Body is not { Valid: true } body
            || !TryComp(body, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        var customLayers = bodyAppearance.CustomBaseLayers;
        var spriteLayers = bodyAppearance.BaseLayers;
        component.Type = relevantLayer;

        part.Species = bodyAppearance.Species;

        // scav edit start
        if (customLayers.ContainsKey(relevantLayer))
        {
            component.ID = customLayers[relevantLayer].Id;
            component.Color = customLayers[relevantLayer].Color;
        }
        else if (spriteLayers.ContainsKey(relevantLayer))
        {
            component.ID = spriteLayers[relevantLayer].ID;
            component.Color = bodyAppearance.SkinColor;
        }
        else
        {
            component.ID = CreateIdFromPart(bodyAppearance, relevantLayer);
            component.Color = bodyAppearance.SkinColor;
        }
        // scav edit end

        // I HATE HARDCODED CHECKS I HATE HARDCODED CHECKS I HATE HARDCODED CHECKS
        if (part.PartType == BodyPartType.Head)
            component.EyeColor = bodyAppearance.EyeColor;

        var markingsByLayer = new Dictionary<HumanoidVisualLayers, List<Marking>>();

        // scav edit start
        foreach (var sublayer in HumanoidVisualLayersExtension.Sublayers(relevantLayer))
        {
            var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(sublayer);
            if (bodyAppearance.MarkingSet.Markings.TryGetValue(category, out var markingList))
                markingsByLayer[sublayer] = markingList.Select(m => new Marking(m.MarkingId, m.MarkingColors.ToList())).ToList();
        }
        // scav edit end

        component.Markings = markingsByLayer;
        Dirty(uid, component);
    }

    private string? CreateIdFromPart(HumanoidAppearanceComponent bodyAppearance, HumanoidVisualLayers part)
    {
        var speciesProto = Prototypes.Index(bodyAppearance.Species);
        var baseSprites = Prototypes.Index<HumanoidSpeciesBaseSpritesPrototype>(speciesProto.SpriteSet);

        return baseSprites.Sprites.TryGetValue(part, out var value)
            ? HumanoidVisualLayersExtension.GetSexMorph(part, bodyAppearance.Sex, value)
            : null;
    }

    public void ModifyMarkings(EntityUid uid,
        Entity<BodyPartAppearanceComponent?> partAppearance,
        HumanoidAppearanceComponent bodyAppearance,
        HumanoidVisualLayers targetLayer,
        string markingId,
        bool remove = false)
    {

        if (!Resolve(partAppearance, ref partAppearance.Comp))
            return;

        if (!remove)
        {

            if (!_markingManager.Markings.TryGetValue(markingId, out var prototype))
                return;

            var markingColors = MarkingColoring.GetMarkingLayerColors(
                    prototype,
                    bodyAppearance.SkinColor,
                    bodyAppearance.EyeColor,
                    bodyAppearance.MarkingSet
                );

            var marking = new Marking(markingId, markingColors);

            _humanoid.SetLayerVisibility((uid, bodyAppearance), targetLayer, true);
            _humanoid.AddMarking(uid, markingId, markingColors, true, true, bodyAppearance);
            if (!partAppearance.Comp.Markings.ContainsKey(targetLayer))
                partAppearance.Comp.Markings[targetLayer] = new List<Marking>();

            partAppearance.Comp.Markings[targetLayer].Add(marking);
        }
        //else
            //RemovePartMarkings(uid, component, bodyAppearance);
    }

    private void HandleState(EntityUid uid, BodyPartAppearanceComponent component, ref AfterAutoHandleStateEvent args) =>
        ApplyPartMarkings(uid, component);

    private void OnPartAttachedToBody(EntityUid uid, BodyComponent component, ref BodyPartAddedEvent args)
    {
        if (!TryComp(uid, out HumanoidAppearanceComponent? bodyAppearance)
            || _net.IsClient
            || !bodyAppearance.ProfileLoaded)
            return;

        BodyPartAppearanceComponent? partAppearance = null;

        if (!TryComp(args.Part, out partAppearance))
            partAppearance = EnsureComp<BodyPartAppearanceComponent>(args.Part);

        // scav edit start
        if (partAppearance.ID != null && partAppearance.Type.HasValue)
            _humanoid.SetBaseLayerId(uid, partAppearance.Type.Value, partAppearance.ID, sync: true, bodyAppearance);
        // scav edit end

        UpdateAppearance(uid, partAppearance);
    }

    private void OnPartDroppedFromBody(EntityUid uid, BodyComponent component, ref BodyPartRemovedEvent args)
    {
        if (TerminatingOrDeleted(uid)
            || TerminatingOrDeleted(args.Part)
            || !TryComp(uid, out HumanoidAppearanceComponent? bodyAppearance)
            || _timing.ApplyingState)
            return;

        BodyPartAppearanceComponent? partAppearance = null;
        // We check for this conditional here since some entities may not have a profile... If they dont
        // have one, and their part is gibbed, the markings will not be removed or applied properly.
        if (!TryComp<BodyPartAppearanceComponent>(args.Part, out partAppearance))
            partAppearance = EnsureComp<BodyPartAppearanceComponent>(args.Part);

        RemoveAppearance(uid, partAppearance, args.Part);
    }

    protected void UpdateAppearance(EntityUid target,
        BodyPartAppearanceComponent component)
    {
        if (!TryComp(target, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        if (component.EyeColor != null)
        {
            bodyAppearance.EyeColor = component.EyeColor.Value;
            _humanoid.SetLayerVisibility((target, bodyAppearance), HumanoidVisualLayers.Eyes, true);
        }

        // scav edit start
        if (component.Color != null && component.Type.HasValue)
            _humanoid.SetBaseLayerColor(target, component.Type.Value, component.Color, true, bodyAppearance);

        if (component.Type.HasValue)
            _humanoid.SetLayerVisibility((target, bodyAppearance), component.Type.Value, true);
        // scav edit end

        foreach (var (visualLayer, markingList) in component.Markings)
        {
            _humanoid.SetLayerVisibility((target, bodyAppearance), visualLayer, true);
            foreach (var marking in markingList)
            {
                _humanoid.AddMarking(target, marking.MarkingId, marking.MarkingColors, true, true, bodyAppearance);
            }
        }

        Dirty(target, bodyAppearance);
    }

    protected void RemoveAppearance(EntityUid entity, BodyPartAppearanceComponent component, EntityUid partEntity)
    {
        if (!TryComp(entity, out HumanoidAppearanceComponent? bodyAppearance))
            return;

        // scav edit start
        if (component.Type.HasValue)
            _humanoid.SetLayerVisibility(entity, component.Type.Value, false);
        // scav edit end
        foreach (var (visualLayer, markingList) in component.Markings)
        {
            _humanoid.SetLayerVisibility((entity, bodyAppearance), visualLayer, false);
        }
        RemoveBodyMarkings(entity, component, bodyAppearance);
    }

    protected abstract void ApplyPartMarkings(EntityUid target, BodyPartAppearanceComponent component);

    protected abstract void RemoveBodyMarkings(EntityUid target, BodyPartAppearanceComponent partAppearance, HumanoidAppearanceComponent bodyAppearance);
}
