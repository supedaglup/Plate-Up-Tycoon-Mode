using KitchenData;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Tycoon appliance prices, read from Resources/AppliancePrices.txt.</summary>
    public static class TycoonPricing
    {
        public static float CategoryMultiplier(TycoonCatalogue.Category category)
        {
            switch (category)
            {
                case TycoonCatalogue.Category.Ingredients: return 0.9f;
                case TycoonCatalogue.Category.Kitchen: return 1.5f;
                case TycoonCatalogue.Category.Cleaning: return 2f;
                case TycoonCatalogue.Category.DiningRoom: return 2f;
                case TycoonCatalogue.Category.Cooking: return 2.5f;
                case TycoonCatalogue.Category.Decorations: return 2.5f;
                case TycoonCatalogue.Category.Footwear: return 2f;
                case TycoonCatalogue.Category.Automation: return 4f;
                case TycoonCatalogue.Category.Research: return 4f;
                default: return 2f;
            }
        }

        public static readonly float[] UPGRADE_MULTIPLIER = { 1f, 3f, 5f };

        public const int ROUND_TO = 5;
        public const int MINIMUM_PRICE = 5;

        private static Dictionary<int, int> _depth;

        public const string RESOURCE_NAME = "PlateUpTycoon.AppliancePrices.txt";
        private static Dictionary<string, int> _list;

        public static bool IsListed(Appliance appliance) => List.ContainsKey(Clean(appliance.Name));

        public static int Price(Appliance appliance, TycoonCatalogue.Category category, bool upgradeOnly)
        {
            if (List.TryGetValue(Clean(appliance.Name), out int listed)) return listed;
            int vanilla = appliance.PurchaseCost;
            if (IsTycoonAppliance(appliance.ID)) return vanilla;
            int depth = UpgradeDepth(appliance.ID);
            if (upgradeOnly && depth == 0) depth = 1;
            float multiplier = CategoryMultiplier(category) * UPGRADE_MULTIPLIER[Mathf.Clamp(depth, 0, UPGRADE_MULTIPLIER.Length - 1)];
            int price = Mathf.RoundToInt(vanilla * multiplier / ROUND_TO) * ROUND_TO;
            return Mathf.Max(MINIMUM_PRICE, price);
        }

        private static string Clean(string name) => (name ?? "").Trim().ToLowerInvariant();

        private static Dictionary<string, int> List
        {
            get
            {
                if (_list != null) return _list;
                _list = new Dictionary<string, int>();
                try
                {
                    using Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(RESOURCE_NAME);
                    if (stream == null) { Mod.LogWarning("Prices: AppliancePrices.txt is missing from the build; using the formula."); return _list; }
                    using var reader = new StreamReader(stream);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length == 0 || line.StartsWith("#")) continue;
                        int eq = line.LastIndexOf('=');
                        if (eq <= 0 || !int.TryParse(line.Substring(eq + 1).Trim(), out int price))
                        {
                            Mod.LogWarning("Prices: could not read this line of AppliancePrices.txt: " + line);
                            continue;
                        }
                        _list[Clean(line.Substring(0, eq))] = Mathf.Max(0, price);
                    }
                    Mod.LogInfo($"Prices: {_list.Count} appliance price(s) from AppliancePrices.txt.");
                }
                catch (System.Exception e)
                {
                    Mod.LogWarning("Prices: could not read AppliancePrices.txt: " + e.Message);
                }
                return _list;
            }
        }

        public static int UpgradeDepth(int applianceID)
        {
            if (_depth == null) BuildDepths();
            return _depth.TryGetValue(applianceID, out int d) ? d : 0;
        }

        private static bool IsTycoonAppliance(int id) =>
            Mod.IsTycoonDesk(id) || (Mod.OrderingCounter != null && id == Mod.OrderingCounter.ID);

        private static void BuildDepths()
        {
            _depth = new Dictionary<int, int>();
            var upgradeTargets = new HashSet<int>();
            var all = new List<Appliance>();
            foreach (Appliance a in GameData.Main.Get<Appliance>())
            {
                all.Add(a);
                if (a.Upgrades != null) foreach (Appliance u in a.Upgrades) if (u != null && u.ID != a.ID) upgradeTargets.Add(u.ID);
            }

            var queue = new Queue<Appliance>();
            foreach (Appliance a in all)
                if (!upgradeTargets.Contains(a.ID)) { _depth[a.ID] = 0; queue.Enqueue(a); }
            while (queue.Count > 0)
            {
                Appliance a = queue.Dequeue();
                if (a.Upgrades == null) continue;
                foreach (Appliance u in a.Upgrades)
                {
                    if (u == null || _depth.ContainsKey(u.ID)) continue;
                    _depth[u.ID] = _depth[a.ID] + 1;
                    queue.Enqueue(u);
                }
            }
            foreach (int id in upgradeTargets) if (!_depth.ContainsKey(id)) _depth[id] = 1;
        }

        internal static void WritePriceList(IEnumerable<TycoonCatalogue.Entry> entries)
        {
            if (!PlateUpTycoon.TycoonPreferences.DeveloperMode) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Tycoon Mode prices ({Mod.BUILD}). Columns: category | appliance | game price -> Tycoon price | from AppliancePrices.txt (list) or the formula | upgrade depth");
                var list = new List<TycoonCatalogue.Entry>(entries);
                list.Sort((a, b) => a.Category != b.Category ? a.Category.CompareTo(b.Category) : a.Cost.CompareTo(b.Cost));
                foreach (TycoonCatalogue.Entry e in list)
                {
                    bool found = GameData.Main.TryGet<Appliance>(e.ApplianceID, out Appliance a);
                    int vanilla = found ? a.PurchaseCost : 0;
                    string source = found && IsListed(a) ? "list" : "formula";
                    sb.AppendLine($"{e.Category,-14} | {e.Name,-32} | {vanilla,5} -> {e.Cost,5} | {source,-7} | {UpgradeDepth(e.ApplianceID)}{(e.IsUpgrade ? " (upgrade only)" : "")}");
                }
                string path = Path.Combine(Application.persistentDataPath, "TycoonPrices.txt");
                File.WriteAllText(path, sb.ToString());
                Mod.LogInfo($"Price list written to {path}.");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Could not write the price list: " + e.Message);
            }
            WriteApplianceList();
        }

        internal static void WriteApplianceList()
        {
            if (!PlateUpTycoon.TycoonPreferences.DeveloperMode) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Tycoon Mode - what each cooking/working appliance does in the game data ({Mod.BUILD}).");
                sb.AppendLine("Process lines: process x speed (1 = normal), auto = works without a player holding the button, validity.");
                var all = new List<Appliance>();
                foreach (Appliance a in GameData.Main.Get<Appliance>())
                    if (a != null && a.Processes != null && a.Processes.Count > 0) all.Add(a);
                all.Sort((x, y) => string.CompareOrdinal(x.Name ?? "", y.Name ?? ""));
                foreach (Appliance a in all)
                {
                    string shop = TycoonCatalogue.TryGetAny(a.ID, out TycoonCatalogue.Entry e) ? $"Tycoon price {e.Cost} ({e.Category})" : "not sold at the desk";
                    var upgrades = new List<string>();
                    if (a.Upgrades != null) foreach (Appliance u in a.Upgrades) if (u != null) upgrades.Add(u.Name);
                    string weak = a.WeakVariant is Appliance w ? w.Name : "-";
                    sb.AppendLine($"{a.Name} | id {a.ID} | {shop} | game price {a.PurchaseCost} | purchasable {a.IsPurchasable} | as upgrade {a.IsPurchasableAsUpgrade} | upgrades to: {(upgrades.Count == 0 ? "-" : string.Join(", ", upgrades))} | weak variant: {weak}");
                    var procs = new List<string>();
                    foreach (Appliance.ApplianceProcesses p in a.Processes)
                        if (p.Process != null) procs.Add($"{p.Process.name} x{p.Speed:0.##}{(p.IsAutomatic ? " auto" : "")} ({p.Validity})");
                    sb.AppendLine($"    processes: {string.Join("; ", procs)}");
                    var props = new List<string>();
                    if (a.Properties != null) foreach (IApplianceProperty p in a.Properties) if (p != null) props.Add(p.GetType().Name);
                    sb.AppendLine($"    properties: {(props.Count == 0 ? "-" : string.Join(", ", props))}");
                    var text = new List<string>();
                    if (!string.IsNullOrEmpty(a.Description)) text.Add(a.Description);
                    if (a.Sections != null) foreach (var sec in a.Sections) text.Add($"{sec.Title}: {sec.Description}");
                    if (text.Count > 0) sb.AppendLine($"    text: {string.Join(" | ", text).Replace(System.Environment.NewLine, " ").Replace((char)10, ' ')}");
                }
                string path = Path.Combine(Application.persistentDataPath, "TycoonAppliances.txt");
                File.WriteAllText(path, sb.ToString());
                Mod.LogInfo($"Appliance list written to {path} ({all.Count} appliances).");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Could not write the appliance list: " + e.Message);
            }
        }
    }
}
