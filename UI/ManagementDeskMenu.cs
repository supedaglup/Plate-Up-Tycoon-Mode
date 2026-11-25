using Kitchen;
using Kitchen.Modules;
using KitchenData;
using System.Collections.Generic;
using UnityEngine;

namespace PlateUpTycoon.UI
{
    /// <summary>The "Card Manager" tile on the Management Desk's main menu.</summary>
    public struct GridItemManageCards : IGridItem
    {
        public Texture2D Icon;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => Icon;
    }

    /// <summary>One Opening or Closing length to pick.</summary>
    public struct GridItemHoursChoice : IGridItem
    {
        public bool Opening;
        public int Seconds;
        public Texture2D Icon;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => Icon;
    }

    /// <summary>One Dish Pricing level to pick.</summary>
    public struct GridItemPricingChoice : IGridItem
    {
        public int Percent;
        public Texture2D Icon;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => Icon;
    }

    /// <summary>One ingredient quality to pick.</summary>
    public struct GridItemQualityChoice : IGridItem
    {
        public int Tier;
        public Texture2D Icon;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => Icon;
    }

    /// <summary>A Marketing or Contract tile on the Management Desk.</summary>
    public struct GridItemPromotion : IGridItem
    {
        public int Kind;
        public int Slot;
        public int Days;
        public int CardID;
        public Texture2D Icon;
        public int SnapshotKey => 0;
        public Texture2D GetSnapshot() => Icon;
    }

    /// <summary>The Management Desk's main menu.</summary>
    internal static class ManagementDeskMenu
    {
        internal static DishSelectionIndicator Current { get => DeskMenuContext.Current; set => DeskMenuContext.Current = value; }

        private static Texture2D _cardsIcon;
        private static readonly Dictionary<int, Texture2D> Clocks = new Dictionary<int, Texture2D>();

        internal static GridMenuConfig Root(DishSelectionIndicator indicator)
        {
            Current = indicator;
            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = new List<IGridItem>
            {
                Patches.PurchasingDeskMenu.CloseTile(),
                new GridItemManageCards { Icon = _cardsIcon != null ? _cardsIcon : (_cardsIcon = MakeCardsIcon()) },
            };
            if (TycoonUnlocks.HoursUnlocked)
                page.Items.Add(new GridItemDeskFolder { Name = "Set Opening and Closing Times", Page = HoursPage(), IconTexture = Clock(8) });
            if (TycoonDishPricing.Available)
            {
                int p = TycoonDishPricing.Percent;
                page.Items.Add(new GridItemDeskFolder { Name = $"Dish Pricing ({(p == 0 ? "normal" : (p > 0 ? "+" : "") + p + "%")})", Page = PricingPage(), IconTexture = PriceTag(p) });
            }
            if (TycoonIngredientQuality.Available)
                page.Items.Add(new GridItemDeskFolder { Name = $"Ingredient Quality ({TycoonIngredientQuality.NAMES[TycoonIngredientQuality.Current]})", Page = QualityPage(), IconTexture = QualityIcon(TycoonIngredientQuality.Current) });
            if (TycoonUnlocks.Stars >= TycoonPromotions.MARKETING_STAR)
                page.Items.Add(new GridItemPromotion { Kind = P_OPEN_MARKETING, Icon = Poster(2, -1f) });
            if (TycoonUnlocks.Stars >= TycoonPromotions.CONTRACTS_STAR)
                page.Items.Add(new GridItemPromotion { Kind = P_OPEN_CONTRACTS, Icon = ContractIcon() });
            return page;
        }

        internal const int P_MARKETING_OFFER = 1, P_MARKETING_CONFIRM = 2, P_MARKETING_RUNNING = 3,
            P_CONTRACT_OFFER = 4, P_CONTRACT_CONFIRM = 5, P_CONTRACT_BOOKED = 6, P_CONTRACT_CANCEL = 7,
            P_OPEN_MARKETING = 8, P_OPEN_CONTRACTS = 9;

        private static GridMenuPaginatedGenericConfig NewPage()
        {
            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = new List<IGridItem>();
            return page;
        }

        private static List<KeyValuePair<int, int>> Running()
        {
            var list = new List<KeyValuePair<int, int>>();
            string mk = TycoonNetData.Get("mk");
            if (string.IsNullOrEmpty(mk)) return list;
            foreach (string part in mk.Split(';'))
            {
                string[] kv = part.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[0], out int slot) && int.TryParse(kv[1], out int days)) list.Add(new KeyValuePair<int, int>(slot, days));
            }
            return list;
        }

        internal static List<KeyValuePair<string, List<GridItemPromotion>>> PromotionRows(int mode)
        {
            var rows = new List<KeyValuePair<string, List<GridItemPromotion>>>();
            if (mode == 1)
            {
                List<KeyValuePair<int, int>> running = Running();
                var offers = new List<GridItemPromotion>();
                int[] mo = TycoonNetData.Ints("mo");
                if (mo != null)
                    foreach (int slot in mo)
                        if (TycoonPromotions.MarketingAt(slot) != null && slot < Mod.MarketingCardIDs.Length)
                            offers.Add(new GridItemPromotion { Kind = P_MARKETING_OFFER, Slot = slot, CardID = Mod.MarketingCardIDs[slot] });
                var live = new List<GridItemPromotion>();
                foreach (KeyValuePair<int, int> r in running)
                    if (TycoonPromotions.MarketingAt(r.Key) != null && r.Key < Mod.MarketingCardIDs.Length)
                        live.Add(new GridItemPromotion { Kind = P_MARKETING_RUNNING, Slot = r.Key, Days = r.Value, CardID = Mod.MarketingCardIDs[r.Key] });
                rows.Add(new KeyValuePair<string, List<GridItemPromotion>>("Today's marketing offers", offers));
                rows.Add(new KeyValuePair<string, List<GridItemPromotion>>($"Running campaigns ({live.Count}/{TycoonPromotions.MAX_ACTIVE_MARKETING})", live));
            }
            else if (mode == 2)
            {
                var booked = new List<GridItemPromotion>();
                int bookedSlot = int.TryParse(TycoonNetData.Get("ctb") ?? "", out int b2) ? b2 : -1;
                if (TycoonPromotions.ContractAt(bookedSlot) != null && bookedSlot < Mod.ContractCardIDs.Length)
                    booked.Add(new GridItemPromotion { Kind = P_CONTRACT_BOOKED, Slot = bookedSlot, CardID = Mod.ContractCardIDs[bookedSlot] });
                var offers = new List<GridItemPromotion>();
                int[] cto = TycoonNetData.Ints("cto");
                if (cto != null)
                    foreach (int slot in cto)
                        if (slot != bookedSlot && TycoonPromotions.ContractAt(slot) != null && slot < Mod.ContractCardIDs.Length)
                            offers.Add(new GridItemPromotion { Kind = P_CONTRACT_OFFER, Slot = slot, CardID = Mod.ContractCardIDs[slot] });
                rows.Add(new KeyValuePair<string, List<GridItemPromotion>>("Booked for the next day", booked));
                rows.Add(new KeyValuePair<string, List<GridItemPromotion>>("Contracts on offer", offers));
            }
            return rows;
        }

        internal static int ActionCode(GridItemPromotion p)
        {
            switch (p.Kind)
            {
                case P_MARKETING_OFFER: return Systems.TycoonPromotionDesk.BuyMarketing(p.Slot);
                case P_CONTRACT_OFFER: return Systems.TycoonPromotionDesk.BookContract(p.Slot);
                case P_CONTRACT_BOOKED: return Systems.TycoonPromotionDesk.CANCEL_CONTRACT;
                default: return 0;
            }
        }

        internal static int Armed { get => DeskMenuContext.Get<int>("armed"); set => DeskMenuContext.Set("armed", value); }

        internal static int Press(GridItemPromotion p)
        {
            int code = ActionCode(p);
            if (code == 0) return 0;
            if (Armed == code) { Armed = 0; return code; }
            Armed = code;
            return 0;
        }

        internal static void Disarm(int highlightedCode)
        {
            if (Armed != highlightedCode) Armed = 0;
        }

        private static string PromotionTitle(GridItemPromotion p)
        {
            TycoonPromotions.Marketing m = TycoonPromotions.MarketingAt(p.Slot);
            TycoonPromotions.Contract c = TycoonPromotions.ContractAt(p.Slot);
            bool armed = Armed != 0 && Armed == ActionCode(p);
            string days(int n) => $"{n} day{(n == 1 ? "" : "s")}";
            switch (p.Kind)
            {
                case P_OPEN_MARKETING: return "Marketing  -  3 offers a day, up to 3 campaigns at once";
                case P_OPEN_CONTRACTS: return "Select Contracts  -  book one special group for the next day";
                case P_MARKETING_OFFER:
                    if (m == null) return null;
                    if (armed) return $"Select again to confirm: run {m.Name} for {m.Price} coins ({days(m.Days)})";
                    string full = Running().Count >= TycoonPromotions.MAX_ACTIVE_MARKETING ? "  (3 already running)" : "";
                    return $"{m.Name}  -  {m.Price} coins for {days(m.Days)}. Select to buy{full}";
                case P_MARKETING_RUNNING:
                    return m == null ? null : $"Running: {m.Name}  -  {days(p.Days)} left";
                case P_CONTRACT_OFFER:
                    if (c == null) return null;
                    return armed ? $"Select again to confirm: book {c.Name} for the next day"
                        : $"{(c.Luxury ? "LUXURY (all day): " : "")}{c.Name}  -  select to book it for the next day";
                case P_CONTRACT_BOOKED:
                    if (c == null) return null;
                    return armed ? $"Select again to cancel the {c.Name} booking" : $"Booked for the next day: {c.Name}. Select to cancel";
            }
            return null;
        }

        private static int Tier(int price) => price < 100 ? 0 : price < 200 ? 1 : price < 350 ? 2 : 3;

        private static readonly Dictionary<int, Texture2D> Posters = new Dictionary<int, Texture2D>();
        private static Texture2D _contract, _contractBooked;

        private static Texture2D Poster(int tier, float left)
        {
            int key = tier * 100 + (left < 0f ? 99 : Mathf.RoundToInt(Mathf.Clamp01(left) * 10f));
            if (Posters.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
            Color[] tiers = { new Color(0.35f, 0.75f, 0.4f), new Color(0.3f, 0.55f, 0.95f), new Color(0.65f, 0.4f, 0.9f), new Color(0.98f, 0.75f, 0.2f) };
            Color colour = tiers[Mathf.Clamp(tier, 0, 3)];
            const int size = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    Color col = clear;
                    bool card = x >= 24 && x <= 103 && y >= 14 && y <= 118;
                    if (card)
                    {
                        bool border = x < 30 || x > 97 || y < 20 || y > 112;
                        float dx = x - 64f, dy = y - 78f;
                        bool burst = dx * dx + dy * dy <= 22f * 22f;
                        bool lines = y >= 32 && y <= 46 && (y % 7 < 4) && x >= 38 && x <= 90;
                        col = border || burst ? colour : lines ? new Color(0.5f, 0.5f, 0.55f) : Color.white;
                    }
                    if (left >= 0f && y >= 0 && y <= 9 && x >= 14 && x <= 113)
                        col = (x - 14) / 99f <= left ? new Color(0.3f, 0.85f, 0.35f) : new Color(0.25f, 0.25f, 0.3f);
                    t.SetPixel(x, y, col);
                }
            t.Apply();
            Posters[key] = t;
            return t;
        }

        private static Texture2D ContractIcon(bool booked = false)
        {
            if (!booked && _contract != null) return _contract;
            if (booked && _contractBooked != null) return _contractBooked;
            const int size = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var paper = new Color(0.96f, 0.92f, 0.78f);
            var ink = new Color(0.35f, 0.3f, 0.25f);
            Color seal = booked ? new Color(0.3f, 0.75f, 0.35f) : new Color(0.85f, 0.2f, 0.2f);
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    Color col = clear;
                    if (x >= 26 && x <= 101 && y >= 8 && y <= 120)
                    {
                        col = x < 30 || x > 97 || y < 12 || y > 116 ? ink : paper;
                        if (x >= 38 && x <= 90 && y >= 60 && y <= 104 && (y % 9 < 3)) col = ink;
                        float dx = x - 82f, dy = y - 30f;
                        if (dx * dx + dy * dy <= 14f * 14f) col = seal;
                    }
                    t.SetPixel(x, y, col);
                }
            t.Apply();
            if (booked) _contractBooked = t; else _contract = t;
            return t;
        }

        private static GridMenuConfig HoursPage()
        {
            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = new List<IGridItem>
            {
                new GridItemDeskBack { Title = "Go Back" },
                new GridItemDeskFolder { Name = "Edit Opening Times", Page = ChoicesPage(true), IconTexture = Clock(4) },
                new GridItemDeskFolder { Name = "Edit Closing Times", Page = ChoicesPage(false), IconTexture = Clock(12) },
            };
            return page;
        }

        private static GridMenuConfig PricingPage()
        {
            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = new List<IGridItem> { new GridItemDeskBack { Title = "Back" } };
            foreach (int p in TycoonDishPricing.CHOICES)
                page.Items.Add(new GridItemPricingChoice { Percent = p, Icon = PriceTag(p) });
            return page;
        }

        private static readonly Dictionary<int, Texture2D> PriceTags = new Dictionary<int, Texture2D>();

        private static Texture2D PriceTag(int percent)
        {
            if (PriceTags.TryGetValue(percent, out Texture2D cached) && cached != null) return cached;
            Color colour = percent < 0 ? new Color(0.35f, 0.78f, 0.4f) : percent > 0 ? new Color(0.9f, 0.32f, 0.28f) : new Color(0.98f, 0.75f, 0.2f);
            const int size = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var edge = new Color(0.95f, 0.95f, 0.95f);
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    Color col = clear;
                    bool body = x >= 30 && x <= 112 && y >= 34 && y <= 94;
                    bool point = x >= 12 && x < 30 && Mathf.Abs(y - 64) <= (x - 12) * 30f / 18f;
                    if (body || point)
                    {
                        col = colour;
                        if ((x - 26) * (x - 26) + (y - 64) * (y - 64) <= 36) col = clear;
                        if (body && (x <= 33 || x >= 109 || y <= 37 || y >= 91)) col = edge;
                    }
                    t.SetPixel(x, y, col);
                }
            int bars = Mathf.Abs(percent) / 10;
            for (int k = 0; k < bars; k++)
                for (int x = 46 + k * 20; x < 58 + k * 20; x++)
                    for (int y = 50; y < 78; y++) t.SetPixel(x, y, edge);
            t.Apply();
            PriceTags[percent] = t;
            return t;
        }

        private static readonly Dictionary<int, Texture2D> QualityIcons = new Dictionary<int, Texture2D>();

        private static GridMenuConfig QualityPage()
        {
            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = new List<IGridItem> { new GridItemDeskBack { Title = "Back" } };
            for (int tier = TycoonIngredientQuality.BUDGET; tier <= TycoonIngredientQuality.GOURMET; tier++)
                page.Items.Add(new GridItemQualityChoice { Tier = tier, Icon = QualityIcon(tier) });
            return page;
        }

        private static Texture2D QualityIcon(int tier)
        {
            if (QualityIcons.TryGetValue(tier, out Texture2D cached) && cached != null) return cached;
            Color[] colours = { new Color(0.6f, 0.6f, 0.62f), new Color(0.35f, 0.75f, 0.4f), new Color(0.65f, 0.4f, 0.9f), new Color(0.98f, 0.75f, 0.2f) };
            Color colour = colours[Mathf.Clamp(tier, 0, 3)];
            const int size = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var wood = new Color(0.55f, 0.38f, 0.22f);
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    Color col = clear;
                    if (x >= 16 && x <= 111 && y >= 18 && y <= 82)
                        col = (x < 22 || x > 105 || y < 24 || y > 76 || (y >= 47 && y <= 52)) ? wood : colour;
                    t.SetPixel(x, y, col);
                }
            for (int k = 0; k <= tier; k++)
            {
                float cx = 64f + (k - tier / 2f) * 24f, cy = 104f;
                for (int x = 0; x < size; x++)
                    for (int y = 88; y < size; y++)
                    {
                        float dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cy);
                        if (dx + dy <= 10f || (dx <= 3f && dy <= 13f) || (dy <= 3f && dx <= 13f)) t.SetPixel(x, y, new Color(1f, 0.9f, 0.3f));
                    }
            }
            t.Apply();
            QualityIcons[tier] = t;
            return t;
        }

        private static GridMenuConfig ChoicesPage(bool opening)
        {
            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = new List<IGridItem> { new GridItemDeskBack { Title = "Back" } };
            foreach (int s in Systems.TycoonBusinessHoursSystem.CHOICES)
                page.Items.Add(new GridItemHoursChoice { Opening = opening, Seconds = s, Icon = Clock(s) });
            return page;
        }

        internal static string TitleFor(object item)
        {
            if (item is GridItemManageCards) return "Card Manager";
            if (item is GridItemQualityChoice q) return TycoonIngredientQuality.Describe(q.Tier);
            if (item is GridItemPricingChoice pr) return TycoonDishPricing.Describe(pr.Percent);
            if (item is GridItemPromotion promotion) return PromotionTitle(promotion);
            if (item is GridItemHoursChoice c)
            {
                float now = c.Opening ? TycoonDayPhases.OpeningSeconds : TycoonDayPhases.ClosingSeconds;
                string current = Mathf.Approximately(now, c.Seconds) ? "  (current)" : "";
                return $"{(c.Opening ? "Opening" : "Closing")}: {c.Seconds} seconds{current}";
            }
            return null;
        }

        private static Texture2D Clock(int seconds)
        {
            if (Clocks.TryGetValue(seconds, out Texture2D cached) && cached != null) return cached;
            const int size = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var rim = new Color(0.95f, 0.95f, 0.95f, 1f);
            var fill = new Color(0.98f, 0.75f, 0.2f, 1f);
            var back = new Color(0.2f, 0.2f, 0.25f, 1f);
            var clear = new Color(0f, 0f, 0f, 0f);
            float c = (size - 1) / 2f, r = size * 0.4f;
            float share = Mathf.Clamp01(seconds / 15f);
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color col = clear;
                    if (d <= r)
                    {
                        float angle = Mathf.Atan2(dx, dy);
                        if (angle < 0f) angle += 2f * Mathf.PI;
                        col = angle / (2f * Mathf.PI) <= share && share > 0f ? fill : back;
                        if (d > r - 7f) col = rim;
                    }
                    t.SetPixel(x, y, col);
                }
            t.Apply();
            Clocks[seconds] = t;
            return t;
        }

        private static Texture2D MakeCardsIcon()
        {
            const int size = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) t.SetPixel(x, y, clear);
            Color[] colours = { new Color(0.35f, 0.6f, 0.9f), new Color(0.95f, 0.6f, 0.25f), new Color(0.4f, 0.8f, 0.5f) };
            for (int k = 0; k < 3; k++)
            {
                float angle = (k - 1) * 0.35f, ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
                for (int x = 0; x < size; x++)
                    for (int y = 0; y < size; y++)
                    {
                        float dx = x - 64f, dy = y - 50f;
                        float u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                        if (Mathf.Abs(u) > 22f || v < -10f || v > 62f) continue;
                        bool border = Mathf.Abs(u) > 18f || v < -6f || v > 58f;
                        t.SetPixel(x, y, border ? colours[k] : (v > 44f ? colours[k] : Color.white));
                    }
            }
            t.Apply();
            return t;
        }
    }
}
