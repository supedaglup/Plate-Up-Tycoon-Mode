using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>The day's reputation, itemised.</summary>
    public static class TycoonReputationReport
    {
        public const string TITLE = "Reputation Report";

        public static int Day;
        public static int Serves;
        public static float SumBase, SumPatience, SumMess, SumCards, SumMarketing, SumLuxury, SumFactor;
        public static float ServeGains, ServeLosses, PlateLoss, PricingChange;
        public static int Walkouts, People;
        public static float WalkoutLoss;
        public static float Customers, ServesPerCustomer, Factor;
        public static int Cards;
        public static readonly List<(string label, float amount)> Gains = new List<(string, float)>();
        public static readonly List<(string label, float amount)> Losses = new List<(string, float)>();
        public static string DecayText = "";
        public static float DecayAmount;
        public static float Total, Reputation;
        public static int Stars;

        public static void Reset(int day)
        {
            Day = day;
            Serves = 0; SumBase = SumPatience = SumMess = SumCards = SumMarketing = SumLuxury = SumFactor = 0f;
            ServeGains = ServeLosses = PlateLoss = PricingChange = 0f;
            Walkouts = 0; WalkoutLoss = 0f; People = 0;
            Gains.Clear(); Losses.Clear();
            DecayText = ""; DecayAmount = 0f;
        }

        public static void Serve(float b, float patience, float mess, float cards, float marketing, float luxury, float factor, float final, float plateLoss)
        {
            Serves++;
            SumBase += b; SumPatience += patience; SumMess += mess; SumCards += cards; SumMarketing += marketing; SumLuxury += luxury; SumFactor += factor;
            if (final >= 0f) ServeGains += final; else ServeLosses += final;
            PlateLoss += plateLoss;
        }

        public static void Walkout(int count, float loss) { Walkouts += count; WalkoutLoss += loss; }

        public static void Note(string label, float amount)
        {
            if (Mathf.Abs(amount) < 0.005f) return;
            List<(string label, float amount)> list = amount >= 0f ? Gains : Losses;
            int i = list.FindIndex(x => x.label == label);
            if (i >= 0) list[i] = (label, list[i].amount + amount); else list.Add((label, amount));
        }

        private static string N(float v) => v.ToString("+0.0;-0.0", CultureInfo.InvariantCulture);

        public static string BuildPage(int page)
        {
            var sb = new System.Text.StringBuilder();
            if (page == 0)
            {
                if (Serves > 0)
                {
                    float n = Serves;
                    float perServe = (SumBase + SumPatience + SumMess + SumCards + SumMarketing + SumLuxury) / n;
                    sb.AppendLine($"Day {Day}: {Serves} item{(Serves == 1 ? "" : "s")} served.");
                    sb.Append($"Each serve, on average: base {N(SumBase / n)}, patience {N(SumPatience / n)}");
                    if (SumMess != 0f) sb.Append($", mess {N(SumMess / n)}");
                    sb.Append($", cards {N(SumCards / n)} ({Cards} on)");
                    if (SumMarketing != 0f) sb.Append($", marketing {N(SumMarketing / n)}");
                    if (SumLuxury != 0f) sb.Append($", luxury day {N(SumLuxury / n)}");
                    sb.AppendLine($" = {N(perServe)}.");
                    sb.AppendLine($"Crowd: {Customers:0.#} customers x {ServesPerCustomer:0.00} items each = {Customers * ServesPerCustomer:0.#} serves, so each serve counts x{SumFactor / n:0.00} (24 / serves).");
                    sb.AppendLine($"{TycoonKitchenLoad.Text(TycoonKitchenLoad.Load)} ({TycoonKitchenLoad.Dishes} dishes, automation -{TycoonKitchenLoad.Automation:0.#}, {TycoonKitchenLoad.Players} player(s)).");
                    sb.AppendLine($"Serving: {N(ServeGains)}{(PlateLoss < -0.05f ? $" (Disposable Plates {N(PlateLoss)})" : "")}{(Mathf.Abs(PricingChange) > 0.05f ? $" (Dish Pricing {N(PricingChange)})" : "")}");
                }
                else sb.AppendLine($"Day {Day}: nothing was served.");
                foreach ((string label, float amount) in Gains) sb.AppendLine($"{label}: {N(amount)}");
                return sb.ToString().TrimEnd();
            }
            if (ServeLosses < -0.05f) sb.AppendLine($"Slow or messy serves: {N(ServeLosses)}");
            if (Walkouts > 0) sb.AppendLine($"Walkouts: {Walkouts} x {N(-TycoonReputation.WALKOUT_PENALTY)} = {N(WalkoutLoss)}");
            foreach ((string label, float amount) in Losses) sb.AppendLine($"{label}: {N(amount)}");
            if (DecayAmount != 0f) sb.AppendLine($"Nightly decay: {DecayText} = {N(-DecayAmount)}");
            sb.AppendLine($"Today: {N(Total)}. Reputation now {Reputation:0} ({TycoonReputation.StarsText(Stars)}"
                + (Stars < TycoonEconomy.MAX_STARS ? $", next star at {TycoonReputation.PromoteScore(Stars):0}" : "")
                + (Stars > TycoonEconomy.MIN_STARS ? $", at risk below {TycoonReputation.DemoteScore(Stars):0})." : ")."));
            return sb.ToString().TrimEnd();
        }

        public const int PAGE_MARKER = 777777;

        public static List<(int id, int amount)> Rows()
        {
            var rows = new List<(int id, int amount)>();
            void Add(int id, float amount) { int a = Mathf.RoundToInt(amount); if (a != 0 && id != 0) { int k = rows.FindIndex(r => r.id == id); if (k >= 0) rows[k] = (id, rows[k].amount + a); else rows.Add((id, a)); } }
            Add(Mod.Ledger.RepServing, ServeGains);
            foreach ((string label, float amount) in Gains) Add(RowFor(label), amount);
            Add(Mod.Ledger.RepSlow, ServeLosses);
            Add(Mod.Ledger.RepWalkouts, WalkoutLoss);
            foreach ((string label, float amount) in Losses) Add(RowFor(label), amount);
            Add(Mod.Ledger.RepDecay, -DecayAmount);
            return rows;
        }

        private static int RowFor(string label)
        {
            if (label.StartsWith("Full House")) return Mod.Ledger.RepFullHouse;
            if (label.StartsWith("Furniture") || label.StartsWith("Ambience")) return Mod.Ledger.RepDecor;
            if (label.StartsWith("Ingredients")) return Mod.Ledger.RepIngredients;
            if (label.StartsWith("Marketing")) return Mod.Ledger.RepMarketing;
            if (label.Contains("ontract")) return Mod.Ledger.RepContract;
            if (label.StartsWith("Health inspection")) return Mod.Ledger.RepInspection;
            if (label.StartsWith("Fast Casual")) return Mod.Ledger.RepFastCasual;
            return Mod.Ledger.RepOther;
        }

        public static string Description()
        {
            var sb = new System.Text.StringBuilder($"Reputation for day {Day}.");
            sb.Append($"\nNow {Reputation:0}" + (Stars < TycoonEconomy.MAX_STARS ? $", next star at {TycoonReputation.PromoteScore(Stars):0}" : "")
                + (Stars > TycoonEconomy.MIN_STARS ? $", at risk below {TycoonReputation.DemoteScore(Stars):0}." : "."));
            return sb.ToString();
        }

        public static void PublishPage()
        {
            var parts = new List<string>();
            foreach ((int id, int amount) in Rows()) parts.Add(id + ":" + amount);
            Chunks("rr", string.Join(";", parts));
            Chunks("rd", Description().Replace("\n", "~"));
        }

        private static void Chunks(string key, string text)
        {
            int chunks = Mathf.Min(MAX_CHUNKS, (text.Length + CHUNK - 1) / CHUNK);
            TycoonNetData.Set(key + "n", chunks.ToString());
            for (int i = 0; i < chunks; i++) TycoonNetData.Set(key + i, text.Substring(i * CHUNK, Mathf.Min(CHUNK, text.Length - i * CHUNK)));
        }

        private static string Read(string key)
        {
            if (!int.TryParse(TycoonNetData.Get(key + "n") ?? "", out int chunks)) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < chunks; i++) sb.Append(TycoonNetData.Get(key + i) ?? "");
            return sb.ToString();
        }

        public static List<(int id, int amount)> PageRows()
        {
            if (TycoonNetData.IsHost) return Rows();
            var rows = new List<(int id, int amount)>();
            foreach (string part in Read("rr").Split(';'))
            {
                string[] f = part.Split(':');
                if (f.Length == 2 && int.TryParse(f[0], out int id) && int.TryParse(f[1], out int amount)) rows.Add((id, amount));
            }
            return rows;
        }

        public static string PageDescription() => TycoonNetData.IsHost ? Description() : Read("rd").Replace("~", "\n");

        private static readonly string[] Pages = new string[2];
        private const int CHUNK = 50, MAX_CHUNKS = 14;

        public static void Publish()
        {
            for (int p = 0; p < 2; p++)
            {
                string text = BuildPage(p);
                Pages[p] = text;
                string coded = text.Replace("\n", "~").Replace("\r", "");
                string key = p == 0 ? "rpA" : "rpB";
                int chunks = Mathf.Min(MAX_CHUNKS, (coded.Length + CHUNK - 1) / CHUNK);
                TycoonNetData.Set(key + "n", chunks.ToString());
                for (int i = 0; i < chunks; i++) TycoonNetData.Set(key + i, coded.Substring(i * CHUNK, Mathf.Min(CHUNK, coded.Length - i * CHUNK)));
            }
        }

        public static string PageText(int page)
        {
            if (TycoonNetData.IsHost && !string.IsNullOrEmpty(Pages[page])) return Pages[page];
            string key = page == 0 ? "rpA" : "rpB";
            if (!int.TryParse(TycoonNetData.Get(key + "n") ?? "", out int chunks)) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < chunks; i++) sb.Append(TycoonNetData.Get(key + i) ?? "");
            return sb.ToString().Replace("~", "\n");
        }
    }
}
