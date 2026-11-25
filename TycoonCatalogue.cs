using KitchenData;
using KitchenLib.References;
using System.Collections.Generic;

namespace PlateUpTycoon
{
    /// <summary>The appliances players can order from the Purchasing Desk, at Tycoon prices.</summary>
    public static class TycoonCatalogue
    {
        /// <summary>Fallback grouping from the game's shop tags, used only for appliances the menu layout does not mention.</summary>
        public enum Category { Cooking, Ingredients, Kitchen, DiningRoom, Decorations, Cleaning, Automation, Research, Footwear, Miscellaneous }

        public struct Entry
        {
            public int ApplianceID;
            public string Name;
            public int Cost;
            public Category Category;
            public bool IsUpgrade;
        }

        private static List<Entry> _all;
        private static List<Entry> _withoutUpgrades;

        private static List<Entry> _sold;
        private static List<Entry> _soldWithoutUpgrades;

        public static bool NeverSold(Entry e) => e.Category == Category.Research;

        public static IReadOnlyList<Entry> Entries
        {
            get
            {
                if (_all == null) Build();
                return TycoonUnlocks.UpgradesForSale ? _sold : _soldWithoutUpgrades;
            }
        }

        public static bool TryGet(int applianceID, out Entry entry)
        {
            foreach (Entry e in Entries)
            {
                if (e.ApplianceID == applianceID) { entry = e; return true; }
            }
            entry = default;
            return false;
        }

        public static bool TryGetAny(int applianceID, out Entry entry)
        {
            if (_all == null) Build();
            foreach (Entry e in _all)
            {
                if (e.ApplianceID == applianceID) { entry = e; return true; }
            }
            entry = default;
            return false;
        }

        public static List<Entry> InCategory(Category category)
        {
            var list = new List<Entry>();
            foreach (Entry e in Entries)
                if (e.Category == category) list.Add(e);
            return list;
        }

        private static void Build()
        {
            _all = new List<Entry>();
            foreach (Appliance appliance in GameData.Main.Get<Appliance>())
            {
                bool sellable = appliance.IsPurchasable && !appliance.PreventSale;
                bool upgrade = !sellable && (appliance.IsPurchasableAsUpgrade || appliance.IsAnUpgrade);
                if ((!sellable && !upgrade) || appliance.PurchaseCost <= 0)
                    continue;
                if (Mod.IsTycoonDesk(appliance.ID))
                    continue;
                string name = string.IsNullOrEmpty(appliance.Name) ? appliance.ID.ToString() : appliance.Name;
                Category category = Categorise(appliance);
                _all.Add(new Entry
                {
                    ApplianceID = appliance.ID,
                    Name = name,
                    Cost = TycoonPricing.Price(appliance, category, upgrade),
                    Category = category,
                    IsUpgrade = upgrade,
                });
            }
            _all.Sort((a, b) => a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost) : string.CompareOrdinal(a.Name, b.Name));
            _withoutUpgrades = _all.FindAll(e => !e.IsUpgrade);
            _sold = _all.FindAll(e => !NeverSold(e));
            _soldWithoutUpgrades = _withoutUpgrades.FindAll(e => !NeverSold(e));
            Mod.LogInfo($"Appliance catalogue: never sold (blueprint furniture): {string.Join(", ", _all.FindAll(NeverSold).ConvertAll(e => e.Name))}.");

            Mod.LogInfo($"Appliance catalogue built: {_withoutUpgrades.Count} shop appliances + {_all.Count - _withoutUpgrades.Count} upgrades.");
            TycoonPricing.WritePriceList(_all);
        }

        private static HashSet<int> _ingredientSources;

        private static HashSet<int> IngredientSources
        {
            get
            {
                if (_ingredientSources != null) return _ingredientSources;
                _ingredientSources = new HashSet<int>();
                foreach (System.Reflection.PropertyInfo p in typeof(ApplianceReferences).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    if (p.Name.StartsWith("Source") && p.PropertyType == typeof(int))
                        _ingredientSources.Add((int)p.GetValue(null));
                }
                return _ingredientSources;
            }
        }

        public static Category Categorise(int applianceID)
        {
            if (!GameData.Main.TryGet<Appliance>(applianceID, out Appliance appliance)) return Category.Miscellaneous;
            return Categorise(appliance);
        }

        private static Category Categorise(Appliance appliance)
        {
            if (IngredientSources.Contains(appliance.ID))
                return Category.Ingredients;

            ShoppingTags tags = appliance.ShoppingTags;
            if ((tags & (ShoppingTags.Office | ShoppingTags.BlueprintStore | ShoppingTags.BlueprintUpgrader)) != 0) return Category.Research;
            if ((tags & ShoppingTags.Automation) != 0) return Category.Automation;
            if ((tags & ShoppingTags.Plumbing) != 0) return Category.Cleaning;
            if ((tags & ShoppingTags.Cooking) != 0) return Category.Cooking;
            if ((tags & ShoppingTags.FrontOfHouse) != 0) return Category.DiningRoom;
            if ((tags & ShoppingTags.Decoration) != 0) return Category.Decorations;
            if ((tags & (ShoppingTags.Basic | ShoppingTags.Technology)) != 0) return Category.Kitchen;
            return Category.Miscellaneous;
        }
    }
}
