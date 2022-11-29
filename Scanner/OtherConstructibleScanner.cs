using System.Collections.Generic;
using Mutagen.Bethesda.Skyrim;

namespace Scanner
{
    internal class OtherConstructibleScanner
    {
        public class ThingInfo
        {
            public ThingInfo(IConstructibleGetter constructible, IConstructibleObjectGetter recipe)
            {
                this.Thing = constructible;
                this.Recipes = new List<IConstructibleObjectGetter>();
                this.Recipes.Add(recipe);
            }

            public IConstructibleGetter Thing { get; }
            public List<IConstructibleObjectGetter> Recipes { get; }
        }
    }
}
