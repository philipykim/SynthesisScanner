using System;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using Mutagen.Bethesda.Plugins.Cache;

namespace Scanner
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "YourPatcher.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var scanners = new AbstractScanner[] { new ArmorScanner(), new WeaponScanner(), new FoodScanner(), new IngredientScanner() };

            foreach (AbstractScanner scanner in scanners)
            {
                scanner.InitialScan(state);
            }

            foreach (var leveledItem in state.LoadOrder.PriorityOrder.LeveledItem().WinningOverrides())
            {
                recurseLeveledItem(leveledItem, state.LinkCache, scanners);
            }

            foreach (var constructible in state.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides())
            {
                if (constructible == null || constructible.WorkbenchKeyword == null)
                    continue;

                IKeywordGetter? workbenchKeyword;
                if (!constructible.WorkbenchKeyword.TryResolve<IKeywordGetter>(state.LinkCache, out workbenchKeyword)
                    || workbenchKeyword?.EditorID == null || workbenchKeyword.EditorID.StartsWith("CraftingDisabled"))
                    continue;

                var createdObject = constructible.CreatedObject.TryResolve(state.LinkCache);

                if (createdObject == null)
                    continue;

                foreach (AbstractScanner scanner in scanners)
                {
                    scanner.HandleConstructible(constructible, createdObject, workbenchKeyword);
                }

            }

            foreach (AbstractScanner scanner in scanners)
            {
                scanner.WriteResults(state);
            }

            Console.WriteLine("Done!");
        }

        private static void recurseLeveledItem(ILeveledItemGetter leveledItem, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, AbstractScanner[] scanners)
        {
            recurseLeveledItem(leveledItem, linkCache, scanners, new HashSet<ILeveledItemGetter>());
        }

        // TODO: No need to recurse when we're looking at every leveled item
        //     What we really need to do is look at every manual placement, every NPC inventory, Outfit, and death item
        private static void recurseLeveledItem(ILeveledItemGetter leveledItem, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, AbstractScanner[] scanners, HashSet<ILeveledItemGetter> alreadyRecursed)
        {
            if (leveledItem.Entries == null)
                return;

            if (alreadyRecursed.Contains(leveledItem))
                return;

            alreadyRecursed.Add(leveledItem);

            //if (leveledItem.FormKey.ToString().Contains("Alchem"))
            //    Console.WriteLine($"Going through {leveledItem.FormKey}: {leveledItem.EditorID}");

            foreach (var entry in leveledItem.Entries)
            {
                if ((entry?.Data?.Reference?.IsNull).GetValueOrDefault())
                {
                    Console.WriteLine($"WARNING: {leveledItem.FormKey.IDString()} in {leveledItem.FormKey.ModKey.FileName} has null entries.");
                    continue;
                }

                IItemGetter? item = entry?.Data?.Reference?.Resolve(linkCache);

                if (entry?.Data?.Count < 1)
                {
                    Console.WriteLine($"WARNING: {item?.EditorID} on {leveledItem.FormKey.IDString()} in {leveledItem.FormKey.ModKey.FileName} has a zero count.");
                }

                foreach (AbstractScanner scanner in scanners)
                {
                    scanner.HandleLeveledItem(item);
                }

                if (item is ILeveledItemGetter)
                {
                    recurseLeveledItem((ILeveledItemGetter)item, linkCache, scanners, alreadyRecursed);
                }
            }
        }


    }
}
