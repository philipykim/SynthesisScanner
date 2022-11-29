using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.IO;

namespace Scanner
{
    internal class FoodScanner : AbstractScanner
    {
        public class FoodInfo
        {
            public FoodInfo(IIngestibleGetter food)
            {
                this.Food = food;
                this.Recipes = new List<IConstructibleObjectGetter>();
            }

            public IIngestibleGetter Food { get; }
            public List<IConstructibleObjectGetter> Recipes { get; }
        }

        public class IngredientInfo
        {
            public IngredientInfo(String? name, int count, uint? value)
            {
                this.Name = name;
                this.Count = count;
                this.Value = value;
            }

            public String? Name { get; }
            public int Count { get; }
            public uint? Value { get; }

            public override String ToString()
            {
                return $"\"{Name}\",{Count},{Value}";
            }
        }

        IDictionary<String, FoodInfo> foods = new Dictionary<String, FoodInfo>();

        protected override string FileName => throw new NotImplementedException();

        public override void InitialScan(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            foreach (var ingestible in state.LoadOrder.PriorityOrder.Ingestible().WinningOverrides())
            {
                if ((ingestible.Flags & Ingestible.Flag.FoodItem) == 0)
                {
                    continue;
                }

                foods.Add(ToKey(ingestible), new FoodInfo(ingestible));
            }
        }

        public override void HandleLeveledItem(IItemGetter? item)
        {
            return;
        }

        public override void HandleConstructible(IConstructibleObjectGetter constructible, IConstructibleGetter createdObject, IKeywordGetter workbenchKeyword)
        {
            if (createdObject is not IIngestibleGetter)
                return;

            var ingestible = (IIngestibleGetter)createdObject;
            if ((ingestible.Flags & Ingestible.Flag.FoodItem) == 0)
                return;

            FoodInfo? existingInfo = foods[ToKey(createdObject)];
            existingInfo.Recipes.Add(constructible);
        }

        public override void WriteResults(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            using StreamWriter foodFile = new("C:\\Users\\Phil\\Desktop\\foods.csv", append: false);
            using StreamWriter recipesFile = new("C:\\Users\\Phil\\Desktop\\recipes.csv", append: false);

            foodFile.WriteLine($"Mod,Form ID,Name,Value,Weight,Effect1,Effect2,Effect3,Effect4,Warmth,VendorItem,Other1,Other2,Other3,Other4,Other5,Other6");

            foreach (var info in foods.Values)
            {
                IIngestibleGetter food = info.Food;

                var effects = food.Effects.Select(effect =>
                {
                    var baseEffect = effect.BaseEffect.Resolve(state.LinkCache);
                    return $"{baseEffect.Name} ({effect?.Data?.Magnitude} : {effect?.Data?.Duration})";
                });

                var effect0 = effects.FirstOrDefault();
                var effect1 = effects.Skip(1).FirstOrDefault();
                var effect2 = effects.Skip(2).FirstOrDefault();
                var effect3 = effects.Skip(3).FirstOrDefault();
                var effect4 = effects.Skip(4).FirstOrDefault();

                var keyWords = food.Keywords != null ? food.Keywords.Select(kw => kw.Resolve(state.LinkCache).EditorID).ToHashSet() : new HashSet<string?>();

                var warmth = keyWords.Where(kw => kw != null && kw.StartsWith("Frostfall")).FirstOrDefault();
                keyWords.Remove(warmth);

                var vendorItem = keyWords.Where(kw => kw != null && kw.StartsWith("VendorItem")).FirstOrDefault();
                keyWords.Remove(vendorItem);

                var winningMod = food.ToLink().ResolveAllContexts<ISkyrimMod, ISkyrimModGetter, IIngestible, IIngestibleGetter>(state.LinkCache).First().ModKey;

                foodFile.WriteLine($"{winningMod},0x{food.FormKey.IDString()},\"{food.Name}\",{food.Value},{food.Weight},{effect0},{effect1},{effect2},{effect3},{warmth},{vendorItem},{String.Join(',', keyWords)}");
            }

            recipesFile.WriteLine("Mod,Form ID,Name,Workbench,Value,Created Count,Value Delta,Ing1,Ing1 Cnt,Ing1 Val,Ing2,Ing2 Cnt,Ing2 Val,Ing3,Ing3 Cnt,Ing3 Val,Ing4,Ing4 Cnt,Ing4 Val,Ing5,Ing5 Cnt,Ing5 Val,Ing6,Ing6 Cnt,Ing6 Val,Ing7,Ing7 Cnt,Ing7 Val,Ing8,Ing8 Cnt,Ing8 Val,Ing9,Ing9 Cnt,Ing9 Val,Ing10,Ing10 Cnt,Ing10 Val,Ing11,Ing11 Cnt,Ing11 Val,Ing12,Ing12 Cnt,Ing12 Val,Ing13,Ing13 Cnt,Ing13 Val,Skill Level,Declutter");

            foreach (var info in foods.Values)
            {
                IIngestibleGetter food = info.Food;

                foreach (var recipe in info.Recipes.Where(recipe => recipe != null))
                {
                    var ingredients = recipe.Items?.Select(entry =>
                    {
                        var count = entry.Item.Count;
                        var itemGetter = entry.Item.Item.Resolve(state.LinkCache);

                        String? name;
                        uint? value;
                        if (itemGetter is IIngestibleGetter)
                        {
                            var ingestible = (IIngestibleGetter)itemGetter;
                            name = ingestible.Name?.ToString();
                            value = ingestible.Value;
                        }
                        else if (itemGetter is IIngredientGetter)
                        {
                            var ingredient = (IIngredientGetter)itemGetter;
                            name = ingredient.Name?.ToString();
                            value = ingredient.Value;
                        }
                        else if (itemGetter is IMiscItemGetter)
                        {
                            var miscItem = (IMiscItemGetter)itemGetter;
                            name = miscItem.Name?.ToString();
                            value = miscItem.Value;
                        }
                        else
                        {
                            Console.WriteLine($"WARNING: Unknown ingredient type {itemGetter}");
                            name = $"{itemGetter}";
                            value = 0;
                        }

                        return new IngredientInfo(name, count, value);
                    }).ToList();

                    if (ingredients == null)
                    {
                        ingredients = new List<IngredientInfo>();
                    }

                    if (ingredients.Any(i => new String[] { "Raw Dragon Meat", "Raw Chaurus Meat", "Raw Troll Meat", "Raw Spider Meat" }.Contains(i.Name)))
                        continue;

                    var winningMod = recipe.AsLink().ResolveAllContexts<ISkyrimMod, ISkyrimModGetter, IConstructibleObject, IConstructibleObjectGetter>(state.LinkCache).First().ModKey;

                    var valueDelta = (recipe.CreatedObjectCount * food.Value) - ingredients.Select(ing => ing.Count * ing.Value).Sum();

                    var conditions = recipe.Conditions.Select(condition =>
                    {
                        if (condition is IConditionFloatGetter)
                        {
                            var cond = (IConditionFloatGetter)condition;
                            var data = (IFunctionConditionDataGetter)cond.Data;
                            data.ParameterOneRecord.TryResolve(state.LinkCache, out var paramOneRecord);
                            var name = (paramOneRecord is IIngestibleGetter) ? ((IIngestibleGetter)paramOneRecord)?.Name?.ToString() : paramOneRecord?.EditorID;
                            var boolOp = (cond.Flags & Condition.Flag.OR) > 0 ? "OR" : "AND";

                            return $"{data.Function}({name}) {OpToString(cond.CompareOperator)} {cond.ComparisonValue} {boolOp}";
                        }
                        else if (condition is IConditionGlobalGetter)
                        {
                            var cond = (IConditionGlobalGetter)condition;
                            var data = (IFunctionConditionDataGetter)cond.Data;
                            data.ParameterOneRecord.TryResolve(state.LinkCache, out var paramOneRecord);
                            var name = (paramOneRecord is IIngestibleGetter) ? ((IIngestibleGetter)paramOneRecord)?.Name?.ToString() : paramOneRecord?.EditorID;
                            var boolOp = (cond.Flags & Condition.Flag.OR) > 0 ? "OR" : "AND";

                            return $"{data.Function}({name}) {OpToString(cond.CompareOperator)} {cond.ComparisonValue.Resolve(state.LinkCache).EditorID} {boolOp}";
                        }
                        else
                        {
                            return "";
                        }
                    }).ToList();

                    if (conditions.Any(c => c.StartsWith("GetGlobalValue(_Seed_CACO_Loaded) <> 2") || c.StartsWith("GetGlobalValue(CACO_CACOIsInstalled) = 0"))
                        || (conditions.Count > 0 && conditions.Last().StartsWith("GetGlobalValue(CACO_PlayerCookingXPEnabled) = 0")))
                        continue;

                    conditions.Remove("GetGlobalValue(CACO_CACOIsInstalled) = 1 AND");
                    conditions.Remove("GetGlobalValue(CACO_CACOAddedRecipes) = 1 AND");
                    conditions.Remove("GetGlobalValue(CACO_PlayerCookingXPEnabled) = 0 OR");
                    conditions.Remove("GetGlobalValue(_DS_Hunterborn__Active) = 1 AND");
                    conditions.Remove("GetGlobalValue(CACO_OptionDeclutterCookingMenu) = 0 OR");

                    var declutter = "";
                    var declutterIdx = conditions.FindIndex(c => c.StartsWith("GetItemCount"));
                    if (declutterIdx >= 0)
                    {
                        while (true)
                        {
                            var declutterCond = conditions[declutterIdx];
                            conditions.RemoveAt(declutterIdx);

                            declutter += (declutter.Length == 0) ? "" : " ";
                            declutter += declutterCond.Replace("GetItemCount(", "").Replace(")", "");

                            if (declutterCond.EndsWith("AND") || conditions.Count == declutterIdx)
                                break;
                        }
                    }

                    var skillLevel = "";
                    var skillLevelIdx = conditions.FindIndex(c => c.StartsWith("GetGlobalValue(CACO_PlayerCookingSkillLVL) >= CACO_FoodCookingSkill"));
                    if (skillLevelIdx > -1)
                    {
                        skillLevel = conditions[skillLevelIdx];
                        if (skillLevelIdx == conditions.Count - 1)
                            skillLevel = skillLevel.Replace(" OR", "");
                        skillLevel = skillLevel.Replace("GetGlobalValue(CACO_PlayerCookingSkillLVL) >= CACO_FoodCookingSkill", "").Replace(" AND", "");
                        conditions.RemoveAt(skillLevelIdx);
                    }

                    conditions = conditions.Select(c => $"\"{c}\"").ToList();

                    var spacer = new String(',', 3 * (13 - ingredients.Count()));

                    recipesFile.WriteLine($"{winningMod},0x{recipe.FormKey.IDString()},\"{food.Name}\",{recipe.WorkbenchKeyword.Resolve(state.LinkCache).EditorID},{food.Value},{recipe.CreatedObjectCount},{valueDelta},{String.Join(",", ingredients)},{spacer}{skillLevel},\"{declutter}\",{String.Join(",", conditions)}");
                }
            }
        }

        private static String OpToString(CompareOperator op)
        {
            switch (op)
            {
                case CompareOperator.EqualTo:
                    return "=";
                case CompareOperator.LessThan:
                    return "<";
                case CompareOperator.LessThanOrEqualTo:
                    return "<=";
                case CompareOperator.GreaterThan:
                    return ">";
                case CompareOperator.GreaterThanOrEqualTo:
                    return ">=";
                case CompareOperator.NotEqualTo:
                    return "<>";
            }
            return "?";
        }
    }
}
