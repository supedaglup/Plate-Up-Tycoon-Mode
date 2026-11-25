using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Components
{
    /// <summary>Recipe Mastery of one recipe at this restaurant.</summary>
    public struct CTycoonRecipeMastery : IComponentData, IModComponent
    {
        public int Dish;
        public int Level;
        public int XP;
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Recipe Mastery: every item served at this restaurant gives one Mastery point to the recipe its path starts at.</summary>
    public static class TycoonRecipeMastery
    {
        public const int CARD_STAR = 9;
        public const float POINTS_PER_TIER = 0.25f, POINTS_PER_ADDON = 0.2f, SHARE_TO_OTHERS = 0.1f, CATCH_UP_SERVES = 8f, CATCH_UP_MAX = 3f;
        public const float BASE_SERVES = 40f, GROWTH = 1.75f, PER_DIFFICULTY = 0.25f;
        private const int BAR_LENGTH = 10;

        internal static readonly List<int> Pending = new List<int>();
        internal static int DebugLevelUps;
        private static readonly Dictionary<int, List<Dish>> Tracks = new Dictionary<int, List<Dish>>();

        public static void Served(int dish)
        {
            if (dish != 0 && Pending.Count < 5000) Pending.Add(dish);
        }

        public static List<Dish> Track(int root)
        {
            if (Tracks.TryGetValue(root, out List<Dish> cached)) return cached;
            var track = new List<Dish>();
            Tracks[root] = track;
            if (GameData.Main == null || !GameData.Main.TryGet<Dish>(root, out Dish _, false)) return track;

            var inside = new HashSet<int> { root };
            var candidates = new List<Dish>();
            for (bool added = true; added;)
            {
                added = false;
                foreach (Dish d in GameData.Main.Get<Dish>())
                {
                    if (d == null || inside.Contains(d.ID) || !d.IsUnlockable || d.Requires == null || d.Requires.Count == 0) continue;
                    if (TycoonRecipeShop.IsHiddenRecipe(d)) continue;
                    bool ok = true;
                    foreach (Unlock r in d.Requires)
                        if (!(r is Dish) || !inside.Contains(r.ID)) { ok = false; break; }
                    if (!ok) continue;
                    inside.Add(d.ID);
                    candidates.Add(d);
                    added = true;
                }
            }

            foreach (KeyValuePair<int, int> n in Numerals(root))
                if (!inside.Contains(n.Key) && GameData.Main.TryGet<Dish>(n.Key, out Dish nd, false)) { inside.Add(n.Key); candidates.Add(nd); }

            var placed = new HashSet<int> { root };
            while (candidates.Count > 0)
            {
                Dish best = null;
                foreach (Dish d in candidates)
                {
                    bool ready = true;
                    if (NumeralBefore.TryGetValue(d.ID, out int before)) ready = before == 0 || placed.Contains(before) || !inside.Contains(before);
                    else foreach (Unlock r in d.Requires) if (!placed.Contains(r.ID)) { ready = false; break; }
                    if (ready && (best == null || Compare(d, best) < 0)) best = d;
                }
                if (best == null) break;
                track.Add(best);
                placed.Add(best.ID);
                candidates.Remove(best);
            }
            Mod.LogInfo($"Recipe Mastery: {TycoonRecipeShop.NameOf(root)} track = {(track.Count == 0 ? "no add-ons" : string.Join(", ", track.ConvertAll(d => $"{d.Name} (tier {Tier(d)})")))}.");
            return track;
        }

        private static readonly Dictionary<int, int> NumeralBefore = new Dictionary<int, int>();

        private static List<KeyValuePair<int, int>> Numerals(int root)
        {
            var list = new List<KeyValuePair<int, int>>();
            if (!GameData.Main.TryGet<Dish>(root, out Dish rootDish, false)) return list;
            string name = TycoonRecipeShop.DisplayName(rootDish);
            if (string.IsNullOrEmpty(name)) return list;
            string[] numerals = { "I", "II", "III", "IV", "V" };
            foreach (Dish d in TycoonRecipeShop.AllDishes())
            {
                if (d == null || d.ID == root || TycoonRecipeShop.IsHiddenRecipe(d)) continue;
                string n = TycoonRecipeShop.DisplayName(d);
                if (string.IsNullOrEmpty(n) || !n.StartsWith(name + " ")) continue;
                string rest = n.Substring(name.Length + 1);
                int colon = rest.IndexOf(':');
                int k = colon > 0 ? System.Array.IndexOf(numerals, rest.Substring(0, colon).Trim()) : -1;
                if (k >= 0) list.Add(new KeyValuePair<int, int>(d.ID, k));
            }
            list.Sort((a, b) => a.Value.CompareTo(b.Value));
            for (int i = 0; i < list.Count; i++) NumeralBefore[list[i].Key] = i == 0 ? 0 : list[i - 1].Key;
            return list;
        }

        private static HashSet<int> _inTracks;

        public static bool InAnyTrack(int dishID)
        {
            if (_inTracks == null)
            {
                var set = new HashSet<int>();
                foreach (Dish d in TycoonRecipeShop.AllDishes())
                    if (d != null && !IsTutorialPage(d.ID) && TycoonRecipeShop.IsMainDish(d))
                        foreach (Dish addOn in Track(d.ID)) set.Add(addOn.ID);
                _inTracks = set;
                Mod.LogInfo($"Recipe Mastery: {set.Count} add-on(s) are earned by Mastery and not sold by Smokey.");
            }
            return _inTracks.Contains(dishID);
        }

        private static bool IsTutorialPage(int id) =>
            Mod.BankTutorialPages.Contains(id) || Mod.ChefTutorialPages.Contains(id) || Mod.CardTutorialPages.Contains(id);

        internal static void WriteAllTracks()
        {
            if (!PlateUpTycoon.TycoonPreferences.DeveloperMode) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Tycoon Mode - Recipe Mastery add-on order ({Mod.BUILD}).");
                sb.AppendLine("Level | add-on (type, tier) | serves for that level | serves in total");
                var mains = new List<Dish>();
                foreach (Dish d in TycoonRecipeShop.AllDishes())
                    if (d != null && !IsTutorialPage(d.ID) && !TycoonRecipeShop.IsHiddenRecipe(d) && TycoonRecipeShop.IsMainDish(d) && !string.IsNullOrEmpty(TycoonRecipeShop.DisplayName(d))) mains.Add(d);
                mains.Sort((a, b) => string.CompareOrdinal(TycoonRecipeShop.DisplayName(a), TycoonRecipeShop.DisplayName(b)));
                foreach (Dish main in mains)
                {
                    List<Dish> track = Track(main.ID);
                    sb.AppendLine();
                    sb.AppendLine($"== {TycoonRecipeShop.DisplayName(main)} ({track.Count} add-on{(track.Count == 1 ? "" : "s")})");
                    int total = 0;
                    for (int i = 0; i < track.Count; i++)
                    {
                        int need = Need(i, track[i]);
                        total += need;
                        string blocked = track[i].BlockedBy != null && track[i].BlockedBy.Count > 0 ? "  [blocked by: " + string.Join(", ", track[i].BlockedBy.ConvertAll(b => b != null ? b.Name : "?")) + "]" : "";
                        Dish a = track[i];
                        string needs = a.Requires != null ? string.Join(" + ", a.Requires.ConvertAll(r => r != null ? r.Name : "?")) : "";
                        string options = a.HasSubOptions || (object)a.OptionCard1 != null || (object)a.OptionCard2 != null || (object)a.ParentOption != null
                            ? $"  [choice card: options {a.OptionCard1?.Name ?? "-"} / {a.OptionCard2?.Name ?? "-"}, parent {a.ParentOption?.Name ?? "-"}]" : "";
                        sb.AppendLine($"  Lv {i + 1,2} | {a.Name} ({a.Type}, tier {Tier(a)}, id {a.ID}) | {need,4} | {total,5} | needs {needs}{blocked}{options}");
                        if (!string.IsNullOrEmpty(a.Description)) sb.AppendLine($"         card text: {a.Description.Replace(System.Environment.NewLine, " ").Replace((char)10, ' ')}");
                    }
                }
                string path = System.IO.Path.Combine(Application.persistentDataPath, "TycoonMasteryTracks.txt");
                System.IO.File.WriteAllText(path, sb.ToString());
                WriteAllDishes();
                Mod.LogInfo($"Recipe Mastery: every recipe's add-on order written to {path}.");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Recipe Mastery: could not write the add-on order: " + e.Message);
            }
        }

        private static void WriteAllDishes()
        {
            if (!PlateUpTycoon.TycoonPreferences.DeveloperMode) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Tycoon Mode - every dish ({Mod.BUILD}). sold = Smokey can sell it; track = the recipe whose Mastery unlocks it.");
            var trackOf = new Dictionary<int, string>();
            foreach (Dish d in TycoonRecipeShop.AllDishes())
                if (d != null && !TycoonRecipeShop.IsHiddenRecipe(d) && TycoonRecipeShop.IsMainDish(d))
                    foreach (Dish a in Track(d.ID)) trackOf[a.ID] = TycoonRecipeShop.DisplayName(d);
            foreach (Dish d in TycoonRecipeShop.AllDishes())
            {
                if (d == null || Mod.IsTutorialPage(d.ID)) continue;
                string hidden = TycoonRecipeShop.HiddenReason(d);
                string status = hidden != null ? "HIDDEN (" + hidden + ")" : trackOf.TryGetValue(d.ID, out string root) ? "track: " + root : TycoonRecipeShop.IsMainDish(d) ? "sold (main)" : "sold";
                string list(System.Collections.IEnumerable items)
                {
                    var names = new List<string>();
                    if (items != null) foreach (object o in items) if (o is GameDataObject g) names.Add(g is Dish gd ? TycoonRecipeShop.DisplayName(gd) : (g as Unlock)?.Name ?? g.ID.ToString());
                    return names.Count == 0 ? "-" : string.Join(" + ", names);
                }
                sb.AppendLine($"{TycoonRecipeShop.DisplayName(d)} | id {d.ID} | {d.Type} | tier {d.RecipeDifficulty()} | unlockable {d.IsUnlockable} | speedrun {d.IsSpeedrunDish} | picture {(object)d.IconPrefab != null} | {status}");
                sb.AppendLine($"    needs {list(d.Requires)} | blocked by {list(d.BlockedBy)} | also adds {list(d.AlsoAddRecipes)}{(d.HasSubOptions ? " | choice card" : "")} | text: {(d.Description ?? "").Replace(System.Environment.NewLine, " ").Replace((char)10, ' ')}");
                if (d.RequiredProcesses != null && d.RequiredProcesses.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (Process process in d.RequiredProcesses)
                    {
                        if (process == null) continue;
                        string basic = process.BasicEnablingAppliance is Appliance b ? b.Name : "-";
                        parts.Add($"{process.name} (asks for {basic}; done by {string.Join(", ", ApplianceNamesFor(process.ID))})");
                    }
                    sb.AppendLine($"    processes: {string.Join("; ", parts)}");
                }
            }
            string path = System.IO.Path.Combine(Application.persistentDataPath, "TycoonDishes.txt");
            System.IO.File.WriteAllText(path, sb.ToString());
            Mod.LogInfo($"Recipe Mastery: every dish written to {path}.");
        }

        private static List<string> ApplianceNamesFor(int processID)
        {
            var names = new List<string>();
            foreach (Appliance appliance in GameData.Main.Get<Appliance>())
            {
                if (appliance == null || appliance.Processes == null) continue;
                foreach (Appliance.ApplianceProcesses p in appliance.Processes)
                    if (p.Process != null && p.Process.ID == processID && p.Validity != ProcessValidity.DoesNotRegister && !names.Contains(appliance.Name)) names.Add(appliance.Name);
            }
            if (names.Count == 0) names.Add("nothing");
            return names;
        }

        private static int Tier(Dish d) => Mathf.Max(1, d.RecipeDifficulty());

        private static int TypeOrder(Dish d)
        {
            switch (d.Type)
            {
                case DishType.Extra: return 0;
                case DishType.Side: return 1;
                case DishType.Starter: return 2;
                case DishType.Dessert: return 3;
                default: return 4;
            }
        }

        private static int Compare(Dish a, Dish b)
        {
            int c = Tier(a).CompareTo(Tier(b));
            if (c != 0) return c;
            c = TypeOrder(a).CompareTo(TypeOrder(b));
            return c != 0 ? c : string.CompareOrdinal(a.Name ?? "", b.Name ?? "");
        }

        public static Dish Next(int root, HashSet<int> owned)
        {
            foreach (Dish d in Track(root))
            {
                if (owned.Contains(d.ID)) continue;
                bool ok = true;
                if (NumeralBefore.TryGetValue(d.ID, out int before)) ok = before == 0 || owned.Contains(before);
                else if (d.Requires != null) foreach (Unlock r in d.Requires) if (r.ID != root && !owned.Contains(r.ID)) { ok = false; break; }
                if (ok && d.BlockedBy != null)
                    foreach (Unlock b in d.BlockedBy) if (b != null && owned.Contains(b.ID)) { ok = false; break; }
                if (ok) return d;
            }
            return null;
        }

        public static int Need(int level, Dish next)
        {
            float tier = next != null ? Tier(next) : 1f;
            float raw = BASE_SERVES * Mathf.Pow(GROWTH, level) * (1f + PER_DIFFICULTY * (tier - 1f));
            return Mathf.Max(5, Mathf.RoundToInt(raw / 5f) * 5);
        }

        public struct Info { public int Level, XP, Need; public bool Mastered; }

        internal const int NET_KEYS = 4;

        public static bool TryGet(int dish, out Info info)
        {
            info = default;
            for (int k = 0; k < NET_KEYS; k++)
            {
                string value = TycoonNetData.Get("rm" + k);
                if (string.IsNullOrEmpty(value)) continue;
                foreach (string one in value.Split(';'))
                {
                    string[] f = one.Split(':');
                    if (f.Length < 5 || !int.TryParse(f[0], out int id) || id != dish) continue;
                    int.TryParse(f[1], out info.Level);
                    int.TryParse(f[2], out info.XP);
                    int.TryParse(f[3], out info.Need);
                    info.Mastered = f[4] == "1";
                    return true;
                }
            }
            return false;
        }

        public static string BarText(int dish)
        {
            if (!TryGet(dish, out Info info)) return null;
            if (info.Mastered) return "<color=#FFD24D>MASTERED</color>";
            int filled = info.Need > 0 ? Mathf.Clamp(Mathf.FloorToInt(BAR_LENGTH * (float)info.XP / info.Need), 0, BAR_LENGTH) : 0;
            return $"Lv {info.Level} <color=#6CFF6C>{new string('|', filled)}</color><color=#5A5A5A>{new string('|', BAR_LENGTH - filled)}</color>";
        }

        public static string TitleText(int dish, HashSet<int> owned)
        {
            if (!TryGet(dish, out Info info)) return null;
            if (info.Mastered) return $"Mastery Lv {info.Level}: MASTERED (every add-on unlocked)";
            Dish next = Next(dish, owned);
            return $"Mastery Lv {info.Level}: {info.XP}/{info.Need}{(next != null ? ", next: " + next.Name : "")}";
        }
    }

    public class TycoonRecipeMasterySystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _mastery, _cards;
        private float _nextScan;
        private int _nightDone = -1;
        private string _published;
        private readonly Dictionary<int, float> _carry = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _servesToday = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _pointsToday = new Dictionary<int, float>();

        protected override void Initialise()
        {
            base.Initialise();
            _mastery = GetEntityQuery(new QueryHelper().All(typeof(CTycoonRecipeMastery)));
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            int day = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            bool night = HasSingleton<SIsNightFirstUpdate>() && _nightDone != day;
            if (TycoonRecipeMastery.Pending.Count == 0 && TycoonRecipeMastery.DebugLevelUps == 0 && now < _nextScan && !night) return;
            _nextScan = now + 1f;

            var owned = new HashSet<int>();
            var cardDishes = new HashSet<int>();
            using (NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                foreach (CTycoonCard c in cards)
                {
                    owned.Add(c.UnlockID);
                    if (TycoonCards.IsRecipe(c.UnlockID)) cardDishes.Add(c.UnlockID);
                }

            var byDish = new Dictionary<int, Entity>();
            using (NativeArray<Entity> entities = _mastery.ToEntityArray(Allocator.Temp))
                foreach (Entity e in entities) byDish[EntityManager.GetComponentData<CTycoonRecipeMastery>(e).Dish] = e;

            foreach (int dish in cardDishes)
            {
                if (byDish.ContainsKey(dish) || TycoonCards.PathRoot(dish, cardDishes) != dish || TycoonRecipeMastery.Track(dish).Count == 0) continue;
                Entity e = EntityManager.CreateEntity(typeof(CTycoonRecipeMastery));
                EntityManager.SetComponentData(e, new CTycoonRecipeMastery { Dish = dish });
                byDish[dish] = e;
                Mod.LogInfo($"Recipe Mastery: tracking {TycoonRecipeShop.NameOf(dish)}.");
            }

            var vanilla = new HashSet<int>();
            using (NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                foreach (CTycoonCard c in cards) if (c.Vanilla) vanilla.Add(c.UnlockID);
            HashSet<int> applied = TycoonCards.Applied(EntityManager);
            var onMenu = new List<int>();
            foreach (int root in byDish.Keys) if (applied.Contains(root) || vanilla.Contains(root)) onMenu.Add(root);
            float load = Mathf.Max(0.05f, TycoonKitchenLoad.CustomerMultiplier()) / TycoonIngredientQuality.MasteryMultiplier(TycoonEconomy.CurrentStars);
            var own = new Dictionary<int, float>();
            foreach (int credit in TycoonRecipeMastery.Pending)
            {
                int root = TycoonCards.PathRoot(credit, cardDishes);
                float tier = GameData.Main.TryGet<Dish>(credit, out Dish served, false) ? Mathf.Max(1, served.RecipeDifficulty()) : 1f;
                if (root != 0 && byDish.ContainsKey(root))
                {
                    float points = (1f + TycoonRecipeMastery.POINTS_PER_TIER * (tier - 1f) + TycoonRecipeMastery.POINTS_PER_ADDON * AddOnsOn(root, applied)) / load;
                    own[root] = (own.TryGetValue(root, out float o) ? o : 0f) + points;
                    _servesToday[root] = (_servesToday.TryGetValue(root, out float s) ? s : 0f) + 1f;
                }
                else if (onMenu.Count > 0)
                    foreach (int main in onMenu)
                    {
                        float points = (1f + TycoonRecipeMastery.POINTS_PER_TIER * (tier - 1f) + TycoonRecipeMastery.POINTS_PER_ADDON * AddOnsOn(main, applied)) / load / onMenu.Count;
                        own[main] = (own.TryGetValue(main, out float o) ? o : 0f) + points;
                        _servesToday[main] = (_servesToday.TryGetValue(main, out float s) ? s : 0f) + 1f / onMenu.Count;
                    }
            }
            TycoonRecipeMastery.Pending.Clear();
            foreach (KeyValuePair<int, float> p in own) _pointsToday[p.Key] = (_pointsToday.TryGetValue(p.Key, out float t) ? t : 0f) + p.Value;

            if (night)
            {
                var catchUp = new Dictionary<int, float>();
                foreach (KeyValuePair<int, float> p in _pointsToday)
                {
                    float serves = _servesToday.TryGetValue(p.Key, out float s) ? s : 0f;
                    if (serves <= 0f) continue;
                    float factor = Mathf.Clamp(Mathf.Sqrt(TycoonRecipeMastery.CATCH_UP_SERVES / serves), 1f, TycoonRecipeMastery.CATCH_UP_MAX);
                    if (factor > 1.001f) catchUp[p.Key] = p.Value * (factor - 1f);
                }
                if (catchUp.Count > 0)
                    Mod.LogInfo("Recipe Mastery: catch-up for rarely served recipes: " + string.Join(", ", System.Linq.Enumerable.Select(catchUp, c => $"{TycoonRecipeShop.NameOf(c.Key)} +{c.Value:0.#}")) + ".");
                foreach (KeyValuePair<int, float> c in catchUp) own[c.Key] = (own.TryGetValue(c.Key, out float o) ? o : 0f) + c.Value;
                _servesToday.Clear();
                _pointsToday.Clear();
            }

            float total = 0f;
            foreach (float v in own.Values) total += v;
            var gainedPoints = new Dictionary<int, float>(own);
            if (total > 0f)
                foreach (int main in onMenu)
                    gainedPoints[main] = (own.TryGetValue(main, out float mine) ? mine : 0f) + TycoonRecipeMastery.SHARE_TO_OTHERS * (total - (own.TryGetValue(main, out float m2) ? m2 : 0f));
            var gained = new Dictionary<int, int>();
            foreach (KeyValuePair<int, float> p in gainedPoints)
            {
                float sum = p.Value + (_carry.TryGetValue(p.Key, out float c) ? c : 0f);
                int whole = Mathf.FloorToInt(sum);
                _carry[p.Key] = sum - whole;
                if (whole > 0) gained[p.Key] = whole;
            }
            int debugLevels = TycoonRecipeMastery.DebugLevelUps;
            TycoonRecipeMastery.DebugLevelUps = 0;

            var published = new List<string>();
            foreach (KeyValuePair<int, Entity> pair in byDish)
            {
                CTycoonRecipeMastery m = EntityManager.GetComponentData<CTycoonRecipeMastery>(pair.Value);
                Dish next = TycoonRecipeMastery.Next(m.Dish, owned);
                if (next != null)
                {
                    m.XP += gained.TryGetValue(m.Dish, out int g) ? g : 0;
                    for (int k = 0; k < debugLevels && next != null; k++) m.XP = Mathf.Max(m.XP, TycoonRecipeMastery.Need(m.Level, next));
                    while (next != null && m.XP >= TycoonRecipeMastery.Need(m.Level, next))
                    {
                        m.XP -= TycoonRecipeMastery.Need(m.Level, next);
                        m.Level++;
                        Unlock(m, next);
                        owned.Add(next.ID);
                        next = TycoonRecipeMastery.Next(m.Dish, owned);
                        if (debugLevels > 1 && next != null) { debugLevels--; m.XP = Mathf.Max(m.XP, TycoonRecipeMastery.Need(m.Level, next)); }
                    }
                    if (next == null) m.XP = 0;
                    EntityManager.SetComponentData(pair.Value, m);
                }
                int need = next != null ? TycoonRecipeMastery.Need(m.Level, next) : 0;
                published.Add($"{m.Dish}:{m.Level}:{m.XP}:{need}:{(next == null ? 1 : 0)}");
                if (night) Mod.LogInfo($"Recipe Mastery: {TycoonRecipeShop.NameOf(m.Dish)} Lv {m.Level}, {(next == null ? "MASTERED" : $"{m.XP}/{need} points to {next.Name}")}.");
            }
            if (night) _nightDone = day;
            Publish(published);
        }

        private static int AddOnsOn(int root, HashSet<int> applied)
        {
            int n = 0;
            foreach (Dish d in TycoonRecipeMastery.Track(root)) if (applied.Contains(d.ID)) n++;
            return n;
        }

        private void Unlock(CTycoonRecipeMastery m, Dish addOn)
        {
            Entity card = EntityManager.CreateEntity(typeof(CTycoonCard));
            EntityManager.SetComponentData(card, new CTycoonCard { UnlockID = addOn.ID, Star = TycoonRecipeMastery.CARD_STAR, Enabled = false, Vanilla = false });
            string recipe = TycoonRecipeShop.NameOf(m.Dish);
            string text = $"{recipe} Lv {m.Level}: {addOn.Name} unlocked!";
            if (text.Length > 58) text = $"Mastery: {addOn.Name} unlocked!";
            if (text.Length > 58) text = text.Substring(0, 58);
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), text, 6f);
            Mod.LogInfo($"Recipe Mastery: {recipe} reached Lv {m.Level} - '{addOn.Name}' ({addOn.Type}, tier {Mathf.Max(1, addOn.RecipeDifficulty())}) unlocked as a card (off).");
        }

        private void Publish(List<string> entries)
        {
            var keys = new string[TycoonRecipeMastery.NET_KEYS];
            int k = 0;
            var chunk = new System.Text.StringBuilder();
            foreach (string one in entries)
            {
                if (chunk.Length > 0 && chunk.Length + 1 + one.Length > 55)
                {
                    if (k >= keys.Length - 1) break;
                    keys[k++] = chunk.ToString();
                    chunk.Clear();
                }
                if (chunk.Length > 0) chunk.Append(';');
                chunk.Append(one);
            }
            if (k < keys.Length) keys[k] = chunk.ToString();
            string all = string.Join("|", keys);
            if (all == _published) return;
            _published = all;
            for (int i = 0; i < keys.Length; i++) TycoonNetData.Set("rm" + i, keys[i] ?? "");
        }
    }
}
