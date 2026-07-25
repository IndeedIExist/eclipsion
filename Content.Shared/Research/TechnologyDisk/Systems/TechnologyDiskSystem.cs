using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Content.Shared.Research.TechnologyDisk.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System;
using Robust.Shared.Utility;

namespace Content.Shared.Research.TechnologyDisk.Systems;

public sealed class TechnologyDiskSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedResearchSystem _research = default!;
    [Dependency] private readonly SharedLatheSystem _lathe = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TechnologyDiskComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TechnologyDiskComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<TechnologyDiskComponent, ExaminedEvent>(OnExamine);
    }

    private void OnMapInit(Entity<TechnologyDiskComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Recipes != null)
            return;

        var weightedRandom = _protoMan.Index(ent.Comp.TierWeightPrototype);
        var tier = int.Parse(weightedRandom.Pick(_random));

        // Faction-specific disciplines that should be excluded from tech disk terminals at tier 3
        // Commented out as per community feedback - factions can now get T3 tech from disks
        /*
        var excludedDisciplinesT3 = new HashSet<string>
        {
            "Cyberdawn",
            "Communard",
            "Imperial",
            "Corporate",
            "Interdyne"
        };
        */

        //get a list of every distinct recipe in all the technologies, but restrict to _Crescent disciplines.
        var techs = new HashSet<ProtoId<LatheRecipePrototype>>();
        foreach (var tech in _protoMan.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (tech.Tier != tier)
                continue;

                    // Only include technologies that belong to _Crescent disciplines.
                    // Detection heuristic: discipline icon RSI path contains "_Crescent".
                    var isCrescentDiscipline = false;
                    try
                    {
                        var disciplineProto = _protoMan.Index<TechDisciplinePrototype>(tech.Discipline);
                        if (disciplineProto.Icon is SpriteSpecifier.Rsi rsi)
                        {
                            var path = rsi.RsiPath.ToString();
                            if (path.StartsWith("_Crescent", StringComparison.OrdinalIgnoreCase) || path.Contains("/_Crescent/") || path.Contains("\\_Crescent\\"))
                                isCrescentDiscipline = true;
                        }
                    }
                    catch
                    {
                        // Missing discipline prototype or unexpected icon type -> skip this tech
                        continue;
                    }

                    if (!isCrescentDiscipline)
                        continue;

                    techs.UnionWith(tech.RecipeUnlocks);
                }

        // Remove explicitly excluded recipes
        if (ent.Comp.ExcludedRecipes != null)
        {
            techs.ExceptWith(ent.Comp.ExcludedRecipes);
        }

        if (techs.Count == 0)
            return;

        //pick one
        ent.Comp.Recipes = [];
        ent.Comp.Recipes.Add(_random.Pick(techs));
        Dirty(ent);
    }

    private void OnAfterInteract(Entity<TechnologyDiskComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!HasComp<ResearchServerComponent>(target) || !TryComp<TechnologyDatabaseComponent>(target, out var database))
            return;

        if (ent.Comp.Recipes != null)
        {
            foreach (var recipe in ent.Comp.Recipes)
            {
                _research.AddLatheRecipe(target, recipe, database);
            }
        }
        _popup.PopupClient(Loc.GetString("tech-disk-inserted"), target, args.User);
        if (_net.IsServer)
            QueueDel(ent);
        args.Handled = true;
    }

    private void OnExamine(Entity<TechnologyDiskComponent> ent, ref ExaminedEvent args)
    {
        var message = Loc.GetString("tech-disk-examine-none");
        if (ent.Comp.Recipes != null && ent.Comp.Recipes.Count > 0)
        {
            var prototype = _protoMan.Index(ent.Comp.Recipes[0]);
            message = Loc.GetString("tech-disk-examine", ("result", _lathe.GetRecipeName(prototype)));

            if (ent.Comp.Recipes.Count > 1) //idk how to do this well. sue me.
                message += " " + Loc.GetString("tech-disk-examine-more");
        }
        args.PushMarkup(message);
    }
}
