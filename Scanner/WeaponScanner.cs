using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.IO;

namespace Scanner
{
    internal class WeaponScanner : AbstractScanner
    {
        public class WeaponInfo
        {
            public WeaponInfo(IWeaponGetter weapon)
            {
                this.Weapon = weapon;
            }

            public IWeaponGetter Weapon { get; }
            public bool Lootable { get; set; }
            public bool Craftable { get; set; }
            public bool Temperable { get; set; }
        }

        IDictionary<String, WeaponInfo> weapons = new Dictionary<String, WeaponInfo>();

        protected override String FileName { get { return "C:\\Users\\Phil\\Desktop\\weapons.csv"; } }

        public override void InitialScan(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            foreach (var weapon in state.LoadOrder.PriorityOrder.Weapon().WinningOverrides())
            {
                weapons.Add(ToKey(weapon), new WeaponInfo(weapon));
            }

            Console.WriteLine($"Found {weapons.Count} weapons.");
        }

        public override void HandleLeveledItem(IItemGetter? item)
        {
            if (item is not IWeaponGetter)
                return;

            IWeaponGetter weapon = (IWeaponGetter)item;

            String key = ToKey(weapon);
            if (weapons.TryGetValue(key, out var existing))
                existing.Lootable = true;
            else
            {
                Console.WriteLine($"How would this even happen: {weapon.Name} ({weapon.FormKey.ModKey.FileName}:{weapon.FormKey.IDString()}) LVLI: {item.FormKey.ModKey.FileName} {item.EditorID}?");
                weapons.Add(key, new WeaponInfo((IWeaponGetter)item) { Lootable = true });
            }
        }

        public override void HandleConstructible(IConstructibleObjectGetter constructible, IConstructibleGetter createdObject, IKeywordGetter workbenchKeyword)
        {
            if (!(createdObject is IWeaponGetter))
                return;

            if (workbenchKeyword.EditorID == "CraftingSmithingSharpeningWheel")
            {
                WeaponInfo? existingInfo;
                if (weapons.TryGetValue(ToKey(createdObject), out existingInfo))
                {
                    existingInfo.Temperable = true;
                }
                else
                {
                    weapons.Add(ToKey(createdObject), new WeaponInfo((IWeaponGetter)createdObject) { Temperable = true });
                }
            }
            else
            {
                WeaponInfo? existingInfo;
                if (weapons.TryGetValue(ToKey(createdObject), out existingInfo))
                {
                    existingInfo.Craftable = true;
                }
                else
                {
                    weapons.Add(ToKey(createdObject), new WeaponInfo((IWeaponGetter)createdObject) { Craftable = true });
                }
            }
        }

        public override void WriteResults(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            using StreamWriter weaponsFile = new(FileName, append: false);

            weaponsFile.WriteLine("Craftable,Lootable,NotTemplate,Mod,Form ID,Name,Skill,Value,Weight,Damage,Reach,Speed,Crit Dmg,Crit Mult,Type,Material,Vendor Item,Spike,,Keyword1,Keyword2,Keyword3,Keyword4,Keyword5,Keyword6,Keyword7,Keyword8");

            foreach (var info in weapons.Values.Where(info => (info.Weapon.MajorFlags & Weapon.MajorFlag.NonPlayable) == 0))
            {
                IWeaponGetter weapon = info.Weapon;

                HashSet<string?> keyWords;

                try
                {
                    keyWords = weapon.Keywords != null ? weapon.Keywords.Select(kw => kw.Resolve(state.LinkCache).EditorID).ToHashSet() : new HashSet<string?>();
                }
                catch
                {
                    Console.WriteLine($"WARNING: could not resolve keywords for ${weapon.FormKey}.");
                    continue;
                }

                var weaponType = keyWords.Where(kw => kw != null && kw.StartsWith("WeapType")).FirstOrDefault();
                keyWords.Remove(weaponType);

                var weaponMaterial = keyWords.Where(kw => kw != null && (kw.Contains("WeapMaterial") || kw.Contains("WeaponMaterial"))).FirstOrDefault();
                keyWords.Remove(weaponMaterial);

                var vendorItem = keyWords.Where(kw => kw != null && kw.StartsWith("VendorItem")).FirstOrDefault();
                keyWords.Remove(vendorItem);

                var spike = keyWords.Where(kw => kw != null && kw.StartsWith("SPIKE")).FirstOrDefault();
                keyWords.Remove(spike);

                var winningMod = weapon.ToLink().ResolveAllContexts<ISkyrimMod, ISkyrimModGetter, IWeapon, IWeaponGetter>(state.LinkCache).First().ModKey;

                var baseWeapon = weapon.Template.IsNull ? weapon : weapon.Template.Resolve(state.LinkCache);

                weaponsFile.WriteLine($"{info.Craftable},{info.Lootable},{weapon.Template.IsNull},{winningMod},0x{weapon.FormKey.IDString()},\"{weapon.Name}\",{baseWeapon.Data?.Skill},{weapon.BasicStats?.Value},{baseWeapon.BasicStats?.Weight},{baseWeapon.BasicStats?.Damage},{baseWeapon.Data?.Reach},{baseWeapon.Data?.Speed},{baseWeapon.Critical?.Damage},{baseWeapon.Critical?.PercentMult},{weaponType},{weaponMaterial},{vendorItem},{spike},{String.Join(',', keyWords)}");
            }
        }
    }
}
