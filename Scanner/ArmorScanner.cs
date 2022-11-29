using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.IO;

namespace Scanner
{
    internal class ArmorScanner : AbstractScanner
    {
        public class ArmorInfo
        {
            public ArmorInfo(IArmorGetter armor)
            {
                this.Armor = armor;
            }

            public IArmorGetter Armor { get; }
            public bool Lootable { get; set; }
            public bool Craftable { get; set; }
            public bool Temperable { get; set; }
        }

        IDictionary<String, ArmorInfo> armors = new Dictionary<String, ArmorInfo>();

        protected override String FileName { get { return "C:\\Users\\Phil\\Desktop\\armors.csv"; } }

        public override void InitialScan(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            foreach (var armor in state.LoadOrder.PriorityOrder.Armor().WinningOverrides())
            {
                armors.Add(ToKey(armor), new ArmorInfo(armor));
            }

            Console.WriteLine($"Found {armors.Count} armors.");
        }

        public override void HandleLeveledItem(IItemGetter? item)
        {
            if (item is not IArmorGetter)
                return;

            IArmorGetter armor = (IArmorGetter)item;

            //IObjectEffectGetter? objectEffect;
            //if (armor.ObjectEffect != null && armor.ObjectEffect.TryResolve<IObjectEffectGetter>(linkCache, out objectEffect))
            //{
            //    if (objectEffect.Effects.Select(x => x.BaseEffect.Resolve(linkCache)).Any(x => (x.Name?.ToString()?.Contains("Alchemy") ?? false) || (x.Name?.ToString()?.Contains("Smithing") ?? false)))
            //    {
            //        Console.WriteLine($"WARNING: Alchemy/Smithing effect found on {armor.Name} ({armor.FormKey.ModKey.FileName}:{armor.FormKey.IDString()}) LVLI: {leveledItem.FormKey.ModKey.FileName} {leveledItem.EditorID}");
            //    }
            //}

            String key = ToKey(armor);
            if (armors.TryGetValue(key, out var existing))
                existing.Lootable = true;
            else
            {
                Console.WriteLine($"How would this even happen: {armor.Name} ({armor.FormKey.ModKey.FileName}:{armor.FormKey.IDString()}) LVLI: {item.FormKey.ModKey.FileName} {item.EditorID}?");
                armors.Add(key, new ArmorInfo((IArmorGetter)item) { Lootable = true });
            }
        }

        public override void HandleConstructible(IConstructibleObjectGetter constructible, IConstructibleGetter createdObject, IKeywordGetter workbenchKeyword)
        {
            if (createdObject is not IArmorGetter)
                return;

            if (workbenchKeyword.EditorID == "CraftingSmithingArmorTable")
            {
                ArmorInfo? existingInfo;
                if (armors.TryGetValue(ToKey(createdObject), out existingInfo))
                {
                    existingInfo.Temperable = true;
                }
                else
                {
                    armors.Add(ToKey(createdObject), new ArmorInfo((IArmorGetter)createdObject) { Temperable = true });
                }
            }
            else 
            {
                ArmorInfo? existingInfo;
                if (armors.TryGetValue(ToKey(createdObject), out existingInfo))
                {
                    existingInfo.Craftable = true;
                }
                else
                {
                    armors.Add(ToKey(createdObject), new ArmorInfo((IArmorGetter)createdObject) { Craftable = true });
                }
            }
        }

        public override void WriteResults(IPatcherState<ISkyrimMod, ISkyrimModGetter> state) 
        {
            using StreamWriter armorsFile = new(FileName, append: false);

            armorsFile.WriteLine("Craftable,Lootable,NotTemplate,Orig Mod,Last Mod,Form ID,Name,Value,Weight,Armor Rating,Material,Type,Category,Slot,Slot Flags,Survival,Vendor Item,Object Effect,Keyword1,Keyword2,Keyword3,Keyword4,Keyword5,Keyword6,Keyword7,Keyword8");

            foreach (var info in armors.Values.Where(info => (info.Armor.MajorFlags & Armor.MajorFlag.NonPlayable) == 0))
            {
                IArmorGetter armor = info.Armor;

                HashSet<string?> keyWords;

                try
                {
                    keyWords = armor.Keywords != null ? armor.Keywords.Select(kw => kw.Resolve(state.LinkCache).EditorID).ToHashSet() : new HashSet<string?>();
                }
                catch
                {
                    Console.WriteLine($"WARNING: could not resolve keywords for ${armor.FormKey}.");
                    continue;
                }

                var armorCategory = keyWords.Where(kw => kw != null && new[] { "ArmorLight", "ArmorHeavy", "ArmorClothing" }.Contains(kw)).FirstOrDefault();
                keyWords.Remove(armorCategory);

                var armorMaterial = keyWords.Where(kw => kw != null && kw.Contains("ArmorMaterial")).FirstOrDefault();
                keyWords.Remove(armorMaterial);

                var armorSlot = keyWords.Where(kw => kw != null && new[] { "ArmorBoots", "ArmorCuirass", "ArmorHelmet", "ArmorGauntlets", "ArmorShield" }.Contains(kw)).FirstOrDefault();
                keyWords.Remove(armorSlot);

                var survival = keyWords.Where(kw => kw != null && kw.StartsWith("Survival")).FirstOrDefault();
                keyWords.Remove(survival);

                var vendorItem = keyWords.Where(kw => kw != null && kw.StartsWith("VendorItem")).FirstOrDefault();
                keyWords.Remove(vendorItem);

                var objectEffect = !armor.ObjectEffect.IsNull ? armor.ObjectEffect.Resolve(state.LinkCache).EditorID : "";

                var winningMod = armor.ToLink().ResolveAllContexts<ISkyrimMod, ISkyrimModGetter, IArmor, IArmorGetter>(state.LinkCache).First().ModKey;

                var slotFlags = "";
                if (armor.BodyTemplate != null)
                {
                    if (Enum.IsDefined(typeof(BipedObjectFlag), (uint)armor.BodyTemplate.FirstPersonFlags))
                    {
                        slotFlags = armor.BodyTemplate.FirstPersonFlags.ToString();
                    }
                    else
                    {
                        for (int idx = 0; idx < 32; idx++)
                        {
                            var flag = (uint)1 << idx;
                            if ((flag & (uint)armor.BodyTemplate.FirstPersonFlags) == flag)
                            {
                                var name = Enum.IsDefined(typeof(BipedObjectFlag), flag) ? ((BipedObjectFlag)flag).ToString() : (idx + 30).ToString();
                                if (slotFlags.Length > 0)
                                    slotFlags += ", ";
                                slotFlags += name;
                            }
                        }
                    }
                }

                armorsFile.WriteLine($"{info.Craftable},{info.Lootable},{armor.TemplateArmor.IsNull},{armor.FormKey.ModKey},{winningMod},0x{armor.FormKey.IDString()},\"{armor.Name}\",{armor.Value},{armor.Weight},{armor.ArmorRating},{armorMaterial},{armor.BodyTemplate?.ArmorType},{armorCategory},{armorSlot},\"{slotFlags}\",{survival},{vendorItem},{objectEffect},{String.Join(',', keyWords)}");
            }
        }
    }
}
