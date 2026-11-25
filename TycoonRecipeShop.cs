using KitchenData;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>The HQ recipe shop: Smokey the chef cat sells recipes in the old trophy room.</summary>
    public static class TycoonRecipeShop
    {
        public const int MAIN_PRICE = 150, MAIN_PRICE_PER_DIFFICULTY = 50;
        public const int OTHER_PRICE = 280, OTHER_PRICE_PER_DIFFICULTY = 140;
        public const int MAX_SHELVES = 24;

        public static int Price(Dish dish)
        {
            int difficulty = Mathf.Max(0, dish.RecipeDifficulty());
            return TycoonShopPrices.ApplyRecipe(IsMainDish(dish)
                ? MAIN_PRICE + MAIN_PRICE_PER_DIFFICULTY * difficulty
                : OTHER_PRICE + OTHER_PRICE_PER_DIFFICULTY * difficulty);
        }

        public static string NameOf(int dishID) =>
            GameData.Main.TryGet<Dish>(dishID, out Dish d, false) && !string.IsNullOrEmpty(DisplayName(d)) ? DisplayName(d) : $"recipe {dishID}";

        private static Dictionary<int, string> _referenceNames;

        public static string DisplayName(Dish d)
        {
            if (d == null) return "";
            string name = d.Name;
            if (!string.IsNullOrEmpty(name) && name != "?") return name;
            try
            {
                UnlockInfo info = d.Info != null && d.Info.Has(Locale.English) ? d.Info.Get(Locale.English) : null;
                if (info != null && !string.IsNullOrEmpty(info.Name)) return info.Name;
            }
            catch (System.Exception) { }
            if (_referenceNames == null)
            {
                _referenceNames = new Dictionary<int, string>();
                foreach (System.Reflection.PropertyInfo p in typeof(KitchenLib.References.DishReferences).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    if (p.PropertyType != typeof(int)) continue;
                    try
                    {
                        int id = (int)p.GetValue(null);
                        string n = p.Name.EndsWith("Base") && p.Name.Length > 4 ? p.Name.Substring(0, p.Name.Length - 4) : p.Name;
                        _referenceNames[id] = System.Text.RegularExpressions.Regex.Replace(n, "(?<=[a-z])(?=[A-Z])", " ");
                    }
                    catch (System.Exception) { }
                }
            }
            return _referenceNames.TryGetValue(d.ID, out string reference) ? reference : "";
        }

        public static HashSet<int> Owned(out int day)
        {
            var owned = new HashSet<int> { Mod.STARTING_DISH };
            day = 1;
            try
            {
                foreach ((World world, Kitchen.SaveState state) in Kitchen.Persistence.FullWorld.All())
                {
                    if (state != Kitchen.SaveState.Loaded || world == null) continue;
                    EntityManager em = world.EntityManager;
                    using (EntityQuery q = em.CreateEntityQuery(typeof(CTycoonCard)))
                    using (NativeArray<CTycoonCard> cards = q.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                        for (int i = 0; i < cards.Length; i++) owned.Add(cards[i].UnlockID);
                    using (EntityQuery q = em.CreateEntityQuery(typeof(Kitchen.SDay)))
                        if (!q.IsEmpty) day = Mathf.Max(day, q.GetSingleton<Kitchen.SDay>().Day);
                }
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Recipe shop: could not read the saved restaurants: " + e.Message);
            }
            TycoonCompany.EnsureLoaded();
            foreach (int id in TycoonCompany.RecipesToDeliver) owned.Add(id);
            return owned;
        }

        private static bool _loggedPool;

        public static List<Dish> AllDishes()
        {
            var byId = new Dictionary<int, Dish>();
            foreach (Dish d in GameData.Main.Get<Dish>()) if (d != null) byId[d.ID] = d;
            var extra = new List<string>();
            var missing = new List<string>();
            foreach (System.Reflection.PropertyInfo p in typeof(KitchenLib.References.DishReferences).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (p.PropertyType != typeof(int)) continue;
                int id;
                try { id = (int)p.GetValue(null); } catch (System.Exception) { continue; }
                if (byId.ContainsKey(id)) continue;
                if (GameData.Main.TryGet<Dish>(id, out Dish d, false) && d != null) { byId[id] = d; extra.Add(p.Name); }
                else if (GameData.Main.TryGet<Unlock>(id, out Unlock u, false) && u != null) missing.Add($"{p.Name} (an {u.GetType().Name}, not a Dish)");
                else missing.Add(p.Name);
            }
            if (!_loggedAll)
            {
                _loggedAll = true;
                Mod.LogInfo($"Recipe shop: {byId.Count} dish(es) in all; found only by ID: {string.Join(", ", extra)}; not dishes in this game: {string.Join(", ", missing)}.");
            }
            return new List<Dish>(byId.Values);
        }

        private static bool _loggedAll;

        public static List<Dish> ForSale(HashSet<int> owned)
        {
            var list = new List<Dish>();
            if (!_loggedPool)
            {
                _loggedPool = true;
                var noIcon = new List<string>();
                var have = new List<string>();
                int all = 0;
                var unnamed = new List<string>();
                foreach (Dish d in AllDishes())
                {
                    if (d == null) continue;
                    if (string.IsNullOrEmpty(d.Name) || d.Name == "?") unnamed.Add($"{d.ID}={DisplayName(d)} ({d.Type}{(d.IsSpeedrunDish ? ", speedrun" : "")})");
                    if (string.IsNullOrEmpty(DisplayName(d))) continue;
                    all++;
                    if ((object)d.IconPrefab == null) noIcon.Add($"{d.Name} ({d.Type})");
                    else if (owned.Contains(d.ID)) have.Add($"{d.Name} ({d.Type})");
                }
                Mod.LogInfo($"Recipe shop: {all} named dish(es) in the game; no icon (sold since ds, name only): {string.Join(", ", noIcon)}; already owned: {string.Join(", ", have)}.");
                var sections = new[] { new List<string>(), new List<string>(), new List<string>() };
                foreach (Dish d in AllDishes())
                    if (d != null && !string.IsNullOrEmpty(DisplayName(d))) sections[SectionOf(d)].Add($"{DisplayName(d)} ({d.Type}{(d.IsSpeedrunDish ? ", speedrun" : "")})");
                Mod.LogInfo($"Recipe shop: dishes the game data leaves unnamed (named by Tycoon): {string.Join(", ", unnamed)}.");
                for (int i = 0; i < 3; i++) Mod.LogInfo($"Recipe shop: {SECTION_NAMES[i]} ({sections[i].Count}): {string.Join(", ", sections[i])}.");
                Systems.TycoonRecipeMastery.WriteAllTracks();
            }
            foreach (Dish d in AllDishes())
            {
                if (d == null || string.IsNullOrEmpty(DisplayName(d)) || owned.Contains(d.ID)) continue;
                if (Mod.IsTutorialPage(d.ID)) continue;
                if (Systems.TycoonRecipeMastery.InAnyTrack(d.ID)) continue;
                if (IsHiddenRecipe(d)) continue;
                list.Add(d);
            }
            list.Sort((a, b) => a.ID.CompareTo(b.ID));
            return list;
        }

        public const int SECTION_MAINS = 0, SECTION_OWN = 1, SECTION_LINKED = 2;
        public static readonly string[] SECTION_NAMES = { "main dishes", "add-ons on their own", "add-ons for a dish" };
        public static readonly string[] SECTION_SIGNS = { "MAIN DISHES", "STARTERS & DESSERTS", "ADD-ONS FOR A DISH" };
        public static readonly float[] SECTION_SHARE = { 0.4f, 0.25f, 0.35f };

        public static readonly HashSet<string> HiddenRecipeNames = new HashSet<string>();

        public static bool IsHiddenRecipe(Dish d) => HiddenReason(d) != null;

        public static string HiddenReason(Dish d)
        {
            if (d == null) return "missing";
            string own = OwnHiddenReason(d);
            if (own != null) return own;
            if (d.Requires != null)
                foreach (Unlock r in d.Requires)
                    if (r is Dish rd && rd.ID != d.ID && OwnHiddenReason(rd) is string why && why != "companion") return $"needs a hidden dish ({DisplayName(rd)}: {why})";
            return null;
        }

        private static string OwnHiddenReason(Dish d)
        {
            if (Mod.IsTutorialPage(d.ID)) return "Tycoon page";
            if (d.Name != null && HiddenRecipeNames.Contains(d.Name)) return "hidden by name";
            if (!HasGameName(d)) return "no name in the game (unused)";
            if (Companions().Contains(d.ID)) return "companion";
            if (IsDefaultOption(d)) return "default choice (the base recipe already works this way)";
            return null;
        }

        public static bool IsDefaultOption(Dish d)
        {
            string name = DisplayName(d);
            if (string.IsNullOrEmpty(name)) return false;
            int at = name.IndexOf(" I: ", System.StringComparison.Ordinal);
            if (at <= 0) return false;
            string recipe = name.Substring(0, at);
            foreach (Dish other in AllDishes())
                if (other != null && other.ID != d.ID && other.Type == DishType.Base && DisplayName(other) == recipe) return true;
            return false;
        }

        public static bool HasGameName(Dish d)
        {
            if (!string.IsNullOrEmpty(d.Name) && d.Name != "?") return true;
            try
            {
                UnlockInfo info = d.Info != null && d.Info.Has(Locale.English) ? d.Info.Get(Locale.English) : null;
                return info != null && !string.IsNullOrEmpty(info.Name);
            }
            catch (System.Exception) { return false; }
        }

        private static HashSet<int> _companions;

        public static HashSet<int> Companions()
        {
            if (_companions != null) return _companions;
            var set = new HashSet<int>();
            foreach (Dish d in AllDishes())
                if (d?.AlsoAddRecipes != null)
                    foreach (var a in d.AlsoAddRecipes)
                        if (a != null && a.ID != d.ID) set.Add(a.ID);
            _companions = set;
            return set;
        }

        public static bool IsMain(int dishID) => GameData.Main.TryGet<Dish>(dishID, out Dish d, false) && IsMainDish(d);

        public static bool IsMainDish(Dish d)
        {
            if (d == null || (d.Type != DishType.Base && d.Type != DishType.Main)) return false;
            if (d.Requires != null) foreach (Unlock r in d.Requires) if (r is Dish && r.ID != d.ID) return false;
            return true;
        }

        public static int SectionOf(Dish d)
        {
            if (IsMainDish(d)) return SECTION_MAINS;
            bool needsDish = false;
            if (d.Requires != null) foreach (Unlock r in d.Requires) if (r is Dish) { needsDish = true; break; }
            if ((d.Type == DishType.Starter || d.Type == DishType.Dessert) && !needsDish) return SECTION_OWN;
            return SECTION_LINKED;
        }

        public static int SectionOf(int dishID) => GameData.Main.TryGet<Dish>(dishID, out Dish d, false) ? SectionOf(d) : SECTION_LINKED;

        public static int KindOrder(Dish d)
        {
            switch (d.Type)
            {
                case DishType.Base: return 0;
                case DishType.Main: return 1;
                case DishType.Starter: return 2;
                case DishType.Side: return 3;
                case DishType.Extra: return 4;
                case DishType.Dessert: return 5;
                default: return 6;
            }
        }

        public static void GroupByKind(List<Dish> dishes) =>
            dishes.Sort((a, b) => KindOrder(a) != KindOrder(b) ? KindOrder(a).CompareTo(KindOrder(b)) : string.CompareOrdinal(a.Name, b.Name));

        public static int[] Sizes(int spots, int[] available)
        {
            int usable = Mathf.Max(0, spots);
            var size = new int[3];
            for (int i = 0; i < 3; i++) size[i] = Mathf.Min(available[i], Mathf.FloorToInt(usable * SECTION_SHARE[i]));
            int left = usable - size[0] - size[1] - size[2];
            for (int round = 0; round < 3 && left > 0; round++)
                foreach (int i in new[] { SECTION_MAINS, SECTION_LINKED, SECTION_OWN })
                {
                    int add = Mathf.Min(left, available[i] - size[i]);
                    if (add <= 0) continue;
                    size[i] += add;
                    left -= add;
                }
            return size;
        }

        public static List<int>[] Sections(List<Vector3> positions, Vector3 centre, Vector3 door, int[] sizes)
        {
            float start = Mathf.Atan2(door.z - centre.z, door.x - centre.x);
            var order = new List<int>();
            for (int i = 0; i < positions.Count; i++) order.Add(i);
            float Angle(int i)
            {
                float a = Mathf.Atan2(positions[i].z - centre.z, positions[i].x - centre.x) - start;
                while (a < 0f) a += 2f * Mathf.PI;
                return a;
            }
            order.Sort((x, y) => Angle(x).CompareTo(Angle(y)));
            var result = new[] { new List<int>(), new List<int>(), new List<int>() };
            int k = 0;
            for (int section = 0; section < 3; section++)
            {
                for (int n = 0; n < sizes[section] && k < order.Count; n++) result[section].Add(order[k++]);
            }
            return result;
        }

        public static List<Dish> Today(List<Dish> forSale, int shelves, int day, int salt = 0)
        {
            var pool = new List<Dish>(forSale);
            TycoonCompany.EnsureLoaded();
            var random = new System.Random(day * 7919 + 13 + TycoonCompany.ShopReroll * 104723 + salt * 7727);
            var picks = new List<Dish>();
            while (picks.Count < shelves && pool.Count > 0)
            {
                int i = random.Next(pool.Count);
                picks.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return picks;
        }
    }
}
