using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.IO;

namespace Scanner
{
    internal class IngredientScanner : AbstractScanner
    {
        public class IngredientInfo
        {
            public IngredientInfo(IIngredientGetter ingredient)
            {
                this.Ingredient = ingredient;
            }

            public IIngredientGetter Ingredient { get; }
        }

        IDictionary<String, IngredientInfo> ingredients = new Dictionary<String, IngredientInfo>();

        protected override String FileName { get { return "C:\\Users\\Phil\\Desktop\\ingredients.csv"; } }

        public override void InitialScan(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            foreach (var ingredient in state.LoadOrder.PriorityOrder.Ingredient().WinningOverrides())
            {
                ingredients.Add(ToKey(ingredient), new IngredientInfo(ingredient));
            }

            Console.WriteLine($"Found {ingredients.Count} ingredients.");
        }

        public override void HandleLeveledItem(IItemGetter? item)
        {
            return;
        }

        public override void HandleConstructible(IConstructibleObjectGetter constructible, IConstructibleGetter createdObject, IKeywordGetter workbenchKeyword)
        {
            return;
        }

        public override void WriteResults(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            using StreamWriter ingredientsFile = new(FileName, append: false);

            ingredientsFile.WriteLine("Orig Mod,Last Mod,Form ID,Name,Value,Weight,Effect Name 1,Effect Mag 1,Effect Dur 1,Effect Name 2,Effect Mag 2,Effect Dur 2,Effect Name 3,Effect Mag 3,Effect Dur 3,Effect Name 4,Effect Mag 4,Effect Dur 4");

            foreach (var info in ingredients.Values)
            {
                IIngredientGetter ingredient = info.Ingredient;

                var winningMod = ingredient.ToLink().ResolveAllContexts<ISkyrimMod, ISkyrimModGetter, IIngredient, IIngredientGetter>(state.LinkCache).First().ModKey;

                var effects = ingredient.Effects.Select(effect =>
                {
                    var baseEffect = effect.BaseEffect.Resolve(state.LinkCache);
                    return $"{baseEffect.Name},{effect?.Data?.Magnitude},{effect?.Data?.Duration}";
                });

                var effect0 = effects.FirstOrDefault();
                var effect1 = effects.Skip(1).FirstOrDefault();
                var effect2 = effects.Skip(2).FirstOrDefault();
                var effect3 = effects.Skip(3).FirstOrDefault();
                var effect4 = effects.Skip(4).FirstOrDefault();

                ingredientsFile.WriteLine($"{ingredient.FormKey.ModKey},{winningMod},0x{ingredient.FormKey.IDString()},\"{ingredient.Name}\",{ingredient.IngredientValue},{ingredient.Weight},{effect0},{effect1},{effect2},{effect3}");
            }


            using StreamWriter ingredientsFlatFile = new("C:\\Users\\Phil\\Desktop\\ingredients_flat.csv", append: false);

            ingredientsFile.WriteLine("Orig Mod,Last Mod,Form ID,Name,Value,Weight,Effect Name,Effect Mag,Effect Dur");

            foreach (var info in ingredients.Values)
            {
                IIngredientGetter ingredient = info.Ingredient;

                var winningMod = ingredient.ToLink().ResolveAllContexts<ISkyrimMod, ISkyrimModGetter, IIngredient, IIngredientGetter>(state.LinkCache).First().ModKey;

                var effects = ingredient.Effects.Select(effect =>
                {
                    var baseEffect = effect.BaseEffect.Resolve(state.LinkCache);
                    return $"{baseEffect.Name},{effect?.Data?.Magnitude},{effect?.Data?.Duration}";
                });

                var effect0 = effects.FirstOrDefault();
                var effect1 = effects.Skip(1).FirstOrDefault();
                var effect2 = effects.Skip(2).FirstOrDefault();
                var effect3 = effects.Skip(3).FirstOrDefault();
                var effect4 = effects.Skip(4).FirstOrDefault();

                ingredientsFile.WriteLine($"{ingredient.FormKey.ModKey},{winningMod},0x{ingredient.FormKey.IDString()},\"{ingredient.Name}\",{ingredient.IngredientValue},{ingredient.Weight},{effect0}");
                ingredientsFile.WriteLine($"{ingredient.FormKey.ModKey},{winningMod},0x{ingredient.FormKey.IDString()},\"{ingredient.Name}\",{ingredient.IngredientValue},{ingredient.Weight},{effect1}");
                ingredientsFile.WriteLine($"{ingredient.FormKey.ModKey},{winningMod},0x{ingredient.FormKey.IDString()},\"{ingredient.Name}\",{ingredient.IngredientValue},{ingredient.Weight},{effect2}");
                ingredientsFile.WriteLine($"{ingredient.FormKey.ModKey},{winningMod},0x{ingredient.FormKey.IDString()},\"{ingredient.Name}\",{ingredient.IngredientValue},{ingredient.Weight},{effect3}");
            }
        }
    }
}