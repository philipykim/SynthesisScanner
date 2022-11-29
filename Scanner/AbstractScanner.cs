using System;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;

namespace Scanner
{
    internal abstract class AbstractScanner
    {
        protected abstract String FileName { get; }

        public abstract void InitialScan(IPatcherState<ISkyrimMod, ISkyrimModGetter> state);

        public abstract void HandleLeveledItem(IItemGetter? item);

        public abstract void HandleConstructible(IConstructibleObjectGetter constructible, IConstructibleGetter createdObject, IKeywordGetter workbenchKeyword);

        public abstract void WriteResults(IPatcherState<ISkyrimMod, ISkyrimModGetter> state);

        protected static String ToKey(IItemGetter item)
        {
            return item.FormKey.ModKey + "|" + item.FormKey.IDString();
        }
    }
}
