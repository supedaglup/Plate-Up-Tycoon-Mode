using KitchenData;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Chubs's custom customer cards and the game cards he never sells.</summary>
    public static class TycoonCustomerCards
    {
        public const int SLOTS = 20;

        public class Card
        {
            public string Name, Flavour;
            public float Customers, Reputation;
            public int Price;
            public int MinGroup, MaxGroup;
            public bool ChangesGroups => MinGroup != 0 || MaxGroup != 0;
        }

        private static List<Card> _cards;
        private static HashSet<string> _excluded;

        public static List<Card> Cards { get { Load(); return _cards; } }

        public static Card At(int slot) => slot >= 0 && slot < Cards.Count ? Cards[slot] : null;

        public static int SlotOf(int unlockID)
        {
            for (int i = 0; i < Mod.CustomerCardIDs.Length; i++) if (Mod.CustomerCardIDs[i] == unlockID && unlockID != 0) return i;
            return -1;
        }

        public static Card Of(int unlockID) => At(SlotOf(unlockID));

        public static bool IsExcluded(string name)
        {
            Load();
            return !string.IsNullOrEmpty(name) && _excluded.Contains(name.Trim().ToLowerInvariant());
        }

        private static float F(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        private static void Load()
        {
            if (_cards != null) return;
            _cards = new List<Card>();
            _excluded = new HashSet<string>();
            try
            {
                using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PlateUpTycoon.CustomerCards.txt");
                if (stream == null) { Mod.LogWarning("Customer cards: CustomerCards.txt not found."); return; }
                using var reader = new StreamReader(stream);
                string section = "cards", line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant(); continue; }
                    if (section == "excluded") { _excluded.Add(line.ToLowerInvariant()); continue; }
                    string[] p = line.Split('|');
                    for (int i = 0; i < p.Length; i++) p[i] = p[i].Trim();
                    if (p.Length < 5 || _cards.Count >= SLOTS) continue;
                    string[] g = p[2].Split('/');
                    int min = g.Length == 2 && int.TryParse(g[0].Trim().TrimStart('+'), out int a) ? a : 0;
                    int max = g.Length == 2 && int.TryParse(g[1].Trim().TrimStart('+'), out int b) ? b : 0;
                    _cards.Add(new Card { Name = p[0], Customers = F(p[1]), MinGroup = Mathf.Clamp(min, 0, 4), MaxGroup = Mathf.Clamp(Mathf.Max(min, max), 0, 4),
                        Reputation = F(p[3]), Price = Mathf.Max(0, Mathf.RoundToInt(F(p[4]))), Flavour = p.Length > 5 ? p[5] : "" });
                }
                Mod.LogInfo($"Customer cards: {_cards.Count} custom card(s), {_excluded.Count} game card(s) never sold.");
            }
            catch (System.Exception e) { Mod.LogWarning("Customer cards: could not read CustomerCards.txt: " + e.Message); }
        }

        internal static void WriteList(IEnumerable<Unlock> all, System.Func<Unlock, bool> sold)
        {
            if (!PlateUpTycoon.TycoonPreferences.DeveloperMode) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Tycoon Mode - the game's customer cards and Chubs's own ({Mod.BUILD}). SOLD = on Chubs's pedestals; EXCLUDED = in [excluded].");
                foreach (Unlock u in all)
                {
                    if (u == null || u is Dish || string.IsNullOrEmpty(u.Name)) continue;
                    if (u.UnlockGroup != UnlockGroup.Generic || u.CardType != CardType.Default || !u.IsUnlockable || u.HasSubOptions) continue;
                    string status = IsExcluded(u.Name) ? "EXCLUDED" : sold(u) ? "SOLD" : "not sold";
                    sb.AppendLine($"{u.Name} | {status} | customers {u.CustomerMultiplier} | +{TycoonReputation.CardValue(u):0.#} rep/serve | {(u.Description ?? "").Replace(System.Environment.NewLine, " ").Replace((char)10, ' ')}");
                }
                sb.AppendLine();
                sb.AppendLine("Chubs's own cards (Resources/CustomerCards.txt):");
                foreach (Card c in Cards) sb.AppendLine($"{c.Name} | +{c.Customers:0}% customers | groups +{c.MinGroup}/+{c.MaxGroup} | +{c.Reputation:0.#} rep/serve | {c.Price} coins | {c.Flavour}");
                string path = Path.Combine(Application.persistentDataPath, "TycoonChubsCards.txt");
                File.WriteAllText(path, sb.ToString());
                Mod.LogInfo($"Card Market: card list written to {path}.");
            }
            catch (System.Exception e) { Mod.LogWarning("Card Market: could not write the card list: " + e.Message); }
        }
    }
}
