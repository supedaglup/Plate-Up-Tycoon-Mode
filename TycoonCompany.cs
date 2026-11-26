using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>The company: one wallet and one loan shared by every restaurant, held by the host and shown in the HQ too.</summary>
    public static class TycoonCompany
    {
        public const string FILE = "TycoonCompany.txt";

        public static int Cash;
        public static int Outstanding;
        public static int DailyPayment;
        public static int GraceNights;
        public static bool DayOpen;
        public static int DayStartCash;
        public static int TotalBorrowed;
        public static int LoansTaken;
        public static int PersonalLoans;
        public static bool DiscountedDebt;
        private static bool _hasPersonalLoans;
        public static bool MetBanker;
        public static bool BankTutorialSeen;
        public static bool MetChef;
        public static bool ChefTutorialSeen;
        public static bool MetDealer;
        public static bool CardTutorialSeen;
        public static int GuideSeen;
        public static readonly System.Collections.Generic.List<int> RecentProfits = new System.Collections.Generic.List<int>();
        public static bool MetWhiskers;
        public static bool DiscountTutorialSeen;
        public static readonly System.Collections.Generic.List<int> CratesBought = new System.Collections.Generic.List<int>();
        public static readonly System.Collections.Generic.List<int> CratesToDeliver = new System.Collections.Generic.List<int>();
        public static readonly System.Collections.Generic.List<int> CardsBought = new System.Collections.Generic.List<int>();
        public static readonly System.Collections.Generic.List<int> CardsToDeliver = new System.Collections.Generic.List<int>();
        public static readonly System.Collections.Generic.List<int> RecipesBought = new System.Collections.Generic.List<int>();
        public static readonly System.Collections.Generic.List<int> RecipesToDeliver = new System.Collections.Generic.List<int>();
        public static readonly System.Collections.Generic.List<string> SitesBought = new System.Collections.Generic.List<string>();
        public static int ActiveSlot;
        public static int ShopReroll;
        public static int TotalReputation, RestaurantCount;
        public static readonly System.Collections.Generic.List<(int slot, int price)> Restaurants = new System.Collections.Generic.List<(int slot, int price)>();
        public static readonly System.Collections.Generic.Dictionary<int, int> ApplianceValues = new System.Collections.Generic.Dictionary<int, int>();
        public static readonly System.Collections.Generic.Dictionary<int, int> LastTakings = new System.Collections.Generic.Dictionary<int, int>();
        public static readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>> OwnedAppliances = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>>();
        public static readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>> CardNeeds = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>>();

        private static bool _loaded;
        private static float _savedAt = -100f;
        private static bool _dirty;

        private static int _slot = 1;
        public static string FileFor(int slot) => slot <= 1 ? FILE : $"TycoonCompany{slot}.txt";
        public static string FileName => FileFor(_slot);
        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static void SwitchTo(int slot)
        {
            if (slot == _slot) return;
            if (_loaded && _dirty) Save();
            _slot = slot;
            ClearFields();
        }

        public static void WipeCurrent()
        {
            ClearFields();
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                Mod.LogInfo($"Company: {FileName} deleted (company {_slot} wiped).");
            }
            catch (Exception e)
            {
                Mod.LogWarning($"Company: could not delete {FileName}: {e.Message}");
            }
        }

        private static void ClearFields()
        {
            Cash = Mod.STARTING_MONEY;
            Outstanding = DailyPayment = GraceNights = TotalBorrowed = LoansTaken = PersonalLoans = 0;
            DiscountedDebt = _hasPersonalLoans = false;
            DayOpen = false; DayStartCash = 0;
            MetBanker = BankTutorialSeen = MetChef = ChefTutorialSeen = MetDealer = CardTutorialSeen = MetWhiskers = DiscountTutorialSeen = false;
            GuideSeen = 0;
            ActiveSlot = 0;
            ShopReroll = 0;
            TotalReputation = RestaurantCount = 0;
            RecentProfits.Clear();
            CratesBought.Clear(); CratesToDeliver.Clear();
            CardsBought.Clear(); CardsToDeliver.Clear();
            RecipesBought.Clear(); RecipesToDeliver.Clear();
            SitesBought.Clear();
            Restaurants.Clear();
            ApplianceValues.Clear();
            LastTakings.Clear();
            OwnedAppliances.Clear();
            CardNeeds.Clear();
            _loaded = false;
            _dirty = false;
        }

        public static string Summary(int slot)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, FileFor(slot));
                if (!File.Exists(path)) return "New company";
                int cash = 0, reputation = 0, restaurants = 0;
                foreach (string raw in File.ReadAllLines(path))
                {
                    if (raw.StartsWith("Cash=")) int.TryParse(raw.Substring(5), out cash);
                    else if (raw.StartsWith("TotalReputation=")) int.TryParse(raw.Substring(16), out reputation);
                    else if (raw.StartsWith("RestaurantCount=")) int.TryParse(raw.Substring(16), out restaurants);
                }
                return $"{cash} coins, {reputation} reputation, {restaurants} restaurant{(restaurants == 1 ? "" : "s")}";
            }
            catch (Exception)
            {
                return "?";
            }
        }

        public static void RollBackUnfinishedDay(string where)
        {
            EnsureLoaded();
            if (!DayOpen) return;
            Mod.LogInfo($"Company: the last day was not finished; the wallet goes back from {Cash} to {DayStartCash} coins ({where}).");
            Cash = DayStartCash;
            DayOpen = false;
            Save();
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            Cash = Mod.STARTING_MONEY;
            PersonalLoans = 0;
            try
            {
                if (!File.Exists(FilePath)) { Mod.LogInfo($"Company: new company with {Cash} coins."); Save(); return; }
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    int eq = raw.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = raw.Substring(0, eq).Trim(), value = raw.Substring(eq + 1).Trim();
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n);
                    switch (key)
                    {
                        case "Cash": Cash = n; break;
                        case "Outstanding": Outstanding = n; break;
                        case "DailyPayment": DailyPayment = n; break;
                        case "GraceNights": GraceNights = n; break;
                        case "DayOpen": DayOpen = n != 0; break;
                        case "DayStartCash": DayStartCash = n; break;
                        case "TotalBorrowed": TotalBorrowed = n; break;
                        case "LoansTaken": LoansTaken = n; break;
                        case "PersonalLoans": PersonalLoans = n; _hasPersonalLoans = true; break;
                        case "DiscountedDebt": DiscountedDebt = n != 0; break;
                        case "MetBanker": MetBanker = n != 0; break;
                        case "BankTutorialSeen": BankTutorialSeen = n != 0; break;
                        case "MetChef": MetChef = n != 0; break;
                        case "ChefTutorialSeen": ChefTutorialSeen = n != 0; break;
                        case "MetDealer": MetDealer = n != 0; break;
                        case "CardTutorialSeen": CardTutorialSeen = n != 0; break;
                        case "GuideSeen": GuideSeen = n; break;
                        case "RecentProfits":
                            RecentProfits.Clear();
                            foreach (string part in value.Split(','))
                                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int profit)) RecentProfits.Add(profit);
                            break;
                        case "MetWhiskers": MetWhiskers = n != 0; break;
                        case "DiscountTutorialSeen": DiscountTutorialSeen = n != 0; break;
                        case "CratesBought": ReadRepeats(value, CratesBought); break;
                        case "CratesToDeliver": ReadRepeats(value, CratesToDeliver); break;
                        case "CardsBought": ReadList(value, CardsBought); break;
                        case "CardsToDeliver": ReadList(value, CardsToDeliver); break;
                        case "RecipesBought": ReadList(value, RecipesBought); break;
                        case "RecipesToDeliver": ReadList(value, RecipesToDeliver); break;
                        case "SitesBought": SitesBought.Clear(); foreach (string site in value.Split(';')) if (site.Trim().Length > 0) SitesBought.Add(site.Trim()); break;
                        case "ActiveSlot": ActiveSlot = n; break;
                        case "ShopReroll": ShopReroll = n; break;
                        case "TotalReputation": TotalReputation = n; break;
                        case "RestaurantCount": RestaurantCount = n; break;
                        case "ApplianceValues":
                            ApplianceValues.Clear();
                            foreach (string r in value.Split(';'))
                            {
                                string[] part = r.Split(':');
                                if (part.Length == 2 && int.TryParse(part[0], out int slot) && int.TryParse(part[1], out int worth)) ApplianceValues[slot] = worth;
                            }
                            break;
                        case "LastTakings":
                            LastTakings.Clear();
                            foreach (string r in value.Split(';'))
                            {
                                string[] part = r.Split(':');
                                if (part.Length == 2 && int.TryParse(part[0], out int slot) && int.TryParse(part[1], out int amount)) LastTakings[slot] = amount;
                            }
                            break;
                        case "OwnedAppliances": ReadSlotLists(value, OwnedAppliances); break;
                        case "CardNeeds": ReadSlotLists(value, CardNeeds); break;
                        case "Restaurants":
                            Restaurants.Clear();
                            foreach (string r in value.Split(';'))
                            {
                                string[] part = r.Split(':');
                                if (part.Length == 2 && int.TryParse(part[0], out int slot) && int.TryParse(part[1], out int price)) Restaurants.Add((slot, price));
                            }
                            break;
                    }
                }
                if (!_hasPersonalLoans) PersonalLoans = LoansTaken;
                Mod.LogInfo($"Company: {Cash} coins, owes {Outstanding} ({DailyPayment} a day).");
            }
            catch (Exception e)
            {
                Mod.LogWarning("Company: could not read " + FileName + ": " + e.Message);
            }
        }

        public static void Save()
        {
            _dirty = false;
            _savedAt = Time.realtimeSinceStartup;
            try
            {
                File.WriteAllLines(FilePath, new[]
                {
                    "# Tycoon Mode company (money shared by all restaurants). Written by the mod.",
                    "Cash=" + Cash, "Outstanding=" + Outstanding, "DailyPayment=" + DailyPayment, "GraceNights=" + GraceNights,
                    "DayOpen=" + (DayOpen ? 1 : 0), "DayStartCash=" + DayStartCash,
                    "TotalBorrowed=" + TotalBorrowed, "LoansTaken=" + LoansTaken, "PersonalLoans=" + PersonalLoans, "DiscountedDebt=" + (DiscountedDebt ? 1 : 0),
                    "MetBanker=" + (MetBanker ? 1 : 0), "BankTutorialSeen=" + (BankTutorialSeen ? 1 : 0),
                    "MetChef=" + (MetChef ? 1 : 0), "ChefTutorialSeen=" + (ChefTutorialSeen ? 1 : 0),
                    "MetDealer=" + (MetDealer ? 1 : 0), "CardTutorialSeen=" + (CardTutorialSeen ? 1 : 0), "GuideSeen=" + GuideSeen, "RecentProfits=" + string.Join(",", RecentProfits),
                    "MetWhiskers=" + (MetWhiskers ? 1 : 0), "DiscountTutorialSeen=" + (DiscountTutorialSeen ? 1 : 0),
                    "CratesBought=" + string.Join(",", CratesBought), "CratesToDeliver=" + string.Join(",", CratesToDeliver),
                    "CardsBought=" + string.Join(",", CardsBought), "CardsToDeliver=" + string.Join(",", CardsToDeliver),
                    "RecipesBought=" + string.Join(",", RecipesBought), "RecipesToDeliver=" + string.Join(",", RecipesToDeliver),
                    "SitesBought=" + string.Join(";", SitesBought), "ActiveSlot=" + ActiveSlot, "ShopReroll=" + ShopReroll, "TotalReputation=" + TotalReputation, "RestaurantCount=" + RestaurantCount,
                    "Restaurants=" + string.Join(";", Restaurants.ConvertAll(r => r.slot + ":" + r.price)),
                    "ApplianceValues=" + string.Join(";", System.Linq.Enumerable.Select(ApplianceValues, v => v.Key + ":" + v.Value)),
                    "LastTakings=" + string.Join(";", System.Linq.Enumerable.Select(LastTakings, v => v.Key + ":" + v.Value)),
                    "OwnedAppliances=" + WriteSlotLists(OwnedAppliances), "CardNeeds=" + WriteSlotLists(CardNeeds),
                });
            }
            catch (Exception e)
            {
                Mod.LogWarning("Company: could not save " + FileName + ": " + e.Message);
            }
        }

        private static void ReadSlotLists(string value, System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>> into)
        {
            into.Clear();
            foreach (string r in value.Split(';'))
            {
                int colon = r.IndexOf(':');
                if (colon <= 0 || !int.TryParse(r.Substring(0, colon), out int slot)) continue;
                var list = new System.Collections.Generic.List<int>();
                ReadList(r.Substring(colon + 1), list);
                into[slot] = list;
            }
        }

        private static string WriteSlotLists(System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>> lists) =>
            string.Join(";", System.Linq.Enumerable.Select(lists, v => v.Key + ":" + string.Join(",", v.Value)));

        private static void ReadRepeats(string value, System.Collections.Generic.List<int> into)
        {
            into.Clear();
            foreach (string part in value.Split(','))
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && id != 0) into.Add(id);
        }

        private static void ReadList(string value, System.Collections.Generic.List<int> into)
        {
            into.Clear();
            foreach (string part in value.Split(','))
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && id != 0 && !into.Contains(id)) into.Add(id);
        }

        public static void MarkDirty() => _dirty = true;

        public static void SaveIfDirty()
        {
            if (_dirty && Time.realtimeSinceStartup - _savedAt > 2f) Save();
        }

        public static void ResetMoney(string why, int carriedOver = 0)
        {
            EnsureLoaded();
            Cash = Mod.STARTING_MONEY + Math.Max(0, carriedOver);
            DayOpen = false;
            Outstanding = 0;
            DailyPayment = 0;
            GraceNights = 0;
            TotalBorrowed = 0;
            LoansTaken = 0;
            PersonalLoans = 0;
            DiscountedDebt = false;
            RecipesBought.Clear();
            RecipesToDeliver.Clear();
            CardsBought.Clear();
            CardsToDeliver.Clear();
            CratesBought.Clear();
            CratesToDeliver.Clear();
            RecentProfits.Clear();
            SitesBought.Clear();
            Restaurants.Clear();
            ApplianceValues.Clear();
            LastTakings.Clear();
            OwnedAppliances.Clear();
            CardNeeds.Clear();
            Save();
            Mod.LogInfo($"Company: money reset to {Cash} ({carriedOver} carried over) and the loan cleared ({why}).");
        }
    }
}
