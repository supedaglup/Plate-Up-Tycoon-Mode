using PlateUpTycoon.Components;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>Reputation: how the restaurant progresses.</summary>
    public static class TycoonReputation
    {
        public const float BASE_GAIN = 1f;
        public const float PATIENCE_NEUTRAL = 0.5f;
        public const float PATIENCE_MODIFIER_MAX = 3f;
        public const float MESS_PENALTY = 0.5f;
        public const int MESS_PENALTY_CAP = 6;
        public const float MESS_SHARE_BASE = 0.03f, MESS_SHARE_PER_STAR = 0.01f;

        public static float MessLoss(float messes, int stars, float serveBefore)
        {
            float n = Mathf.Clamp(messes, 0f, MESS_PENALTY_CAP);
            if (n <= 0f) return 0f;
            float share = MESS_SHARE_BASE + MESS_SHARE_PER_STAR * TycoonEconomy.ClampStars(stars);
            return n * Mathf.Max(MESS_PENALTY, share * Mathf.Max(0f, serveBefore));
        }
        public const float FURNITURE_PER_COIN = 0.01f;
        public const float FURNITURE_BONUS_CAP = 6f;
        public const float FURNITURE_PER_COIN_DAY = 0.025f;

        public static readonly float[] AMBIENCE_EXPECTED = { 0f, 2f, 5f, 9f, 14f, 20f };
        public static readonly float[] AMBIENCE_PENALTY = { 0f, 0.10f, 0.20f, 0.25f, 0.30f, 0.35f };
        public const float AMBIENCE_MAX_BONUS = 0.3f;
        public const string AMBIENCE_NET_KEY = "amb";

        public static float Ambience(float looks, int stars)
        {
            int s = TycoonEconomy.ClampStars(stars);
            float expected = AMBIENCE_EXPECTED[Mathf.Clamp(s, 0, AMBIENCE_EXPECTED.Length - 1)];
            if (expected <= 0f) return 0f;
            float ratio = Mathf.Max(0f, looks) / expected;
            if (ratio < 1f) return -AMBIENCE_PENALTY[Mathf.Clamp(s, 0, AMBIENCE_PENALTY.Length - 1)] * (1f - ratio);
            return Mathf.Min(AMBIENCE_MAX_BONUS, AMBIENCE_MAX_BONUS * (ratio - 1f));
        }
        public const float CARD_RECIPE_BASE = 2.16f, CARD_RECIPE_PER_DIFFICULTY = 2.16f;
        public const float CARD_MAIN_BASE = 5.2f, CARD_MAIN_PER_DIFFICULTY = 3.9f;
        public static readonly float[] CARD_BY_EXP_REWARD = { 1.1f, 2.2f, 4.5f, 6.7f };
        public const float CARD_MORE_CUSTOMERS_SMALL = 1.1f, CARD_MORE_CUSTOMERS_LARGE = 2.2f;

        public static float CardValue(KitchenData.Unlock unlock)
        {
            if (unlock == null) return 0f;
            if (Mod.TakeOutCard != null && unlock.ID == Mod.TakeOutCard.ID) return 0f;
            if (Mod.FastCasualCard != null && unlock.ID == Mod.FastCasualCard.ID) return 0f;
            if (Mod.IsRewardCard(unlock.ID)) return 0f;
            if (Mod.IngredientQualityCard != null && unlock.ID == Mod.IngredientQualityCard.ID) return 0f;
            if (TycoonCustomerCards.Of(unlock.ID) is TycoonCustomerCards.Card custom) return custom.Reputation;
            if (unlock is KitchenData.Dish dish)
                return dish.Type == KitchenData.DishType.Base || dish.Type == KitchenData.DishType.Main
                    ? CARD_MAIN_BASE + CARD_MAIN_PER_DIFFICULTY * Mathf.Max(1, dish.RecipeDifficulty())
                    : CARD_RECIPE_BASE + CARD_RECIPE_PER_DIFFICULTY * Mathf.Max(1, dish.RecipeDifficulty());
            if (TycoonCardMarket.IsHeatCard(unlock)) return TycoonCardMarket.HEAT_CARD_VALUE;
            int exp = Mathf.Clamp((int)unlock.ExpReward, 0, CARD_BY_EXP_REWARD.Length - 1);
            float value = CARD_BY_EXP_REWARD[exp];
            if (unlock.CustomerMultiplier == KitchenData.DishCustomerChange.SmallIncrease) value += CARD_MORE_CUSTOMERS_SMALL;
            else if (unlock.CustomerMultiplier == KitchenData.DishCustomerChange.LargeIncrease) value += CARD_MORE_CUSTOMERS_LARGE;
            return value;
        }

        public static float CardValue(int unlockID) =>
            KitchenData.GameData.Main != null && KitchenData.GameData.Main.TryGet<KitchenData.Unlock>(unlockID, out KitchenData.Unlock u) ? CardValue(u) : 0f;
        public const float WALKOUT_PENALTY = 15f;
        public const float FAST_CASUAL_WALKOUT_PENALTY = 6f;
        public const float TAKEOUT_PLATE_REPUTATION = 0.5f;

        public const float FIRST_STAR_THRESHOLD = 300f;
        public const float THRESHOLD_GROWTH = 2f;
        public const float DEMOTE_SHARE = 0.25f;

        public static readonly float[] DECAY_FLAT = { 5f, 15f, 60f, 190f, 380f, 600f };
        public static readonly float[] DECAY_SHARE = { 0.02f, 0.03f, 0.05f, 0.06f, 0.08f, 0.15f };

        public const float REFERENCE_SERVES = 24f, VOLUME_FACTOR_MIN = 0.25f, VOLUME_FACTOR_MAX = 3f;
        public static float CustomersToday;
        public static float ServesPerCustomer = DEFAULT_SERVES_PER_CUSTOMER;
        public const float DEFAULT_SERVES_PER_CUSTOMER = 1.25f, SERVES_PER_CUSTOMER_MAX = 4f;
        public static float ServesToday => CustomersToday * ServesPerCustomer;
        public static float VolumeFactorMax => VOLUME_FACTOR_MAX * (1f + TycoonKitchenLoad.MAIN_CUSTOMER_CUT * Mathf.Max(0f, TycoonKitchenLoad.AddedMains));
        public static float VolumeFactor => CustomersToday > 0.5f ? Mathf.Clamp(REFERENCE_SERVES / ServesToday, VOLUME_FACTOR_MIN, VolumeFactorMax) : 1f;

        internal static int MeasuredServes, MeasuredPeople;
        internal static readonly System.Collections.Generic.HashSet<Unity.Entities.Entity> MeasuredGroups = new System.Collections.Generic.HashSet<Unity.Entities.Entity>();

        internal static readonly System.Collections.Generic.Dictionary<Unity.Entities.Entity, int> GroupServes = new System.Collections.Generic.Dictionary<Unity.Entities.Entity, int>();

        internal static int NoteServe(Unity.Entities.EntityManager em, Unity.Entities.Entity group)
        {
            MeasuredServes++;
            int members = em.HasComponent<Kitchen.CGroupMember>(group) ? Mathf.Max(1, em.GetBuffer<Kitchen.CGroupMember>(group).Length) : 1;
            if (MeasuredGroups.Add(group)) { MeasuredPeople += members; TycoonReputationReport.People += members; }
            GroupServes.TryGetValue(group, out int before);
            GroupServes[group] = before + 1;
            return before / members;
        }

        public const float FULL_MEAL_BONUS = 0.15f;

        public static readonly float[] NORMAL_CUSTOMERS = { 14f, 15f, 17f, 20f, 25f, 35f };
        public const float FULL_HOUSE_SERVED = 0.95f, FULL_HOUSE_MIN_LOAD = 15f;
        public const float FULL_HOUSE_CROWD_SHARE = 0.3f, FULL_HOUSE_MAX = 0.6f;

        public static float NormalCustomers(int stars, int players) =>
            NORMAL_CUSTOMERS[TycoonEconomy.ClampStars(stars)] * (1f + 0.5f * Mathf.Max(0, players - 1)) * TycoonEconomy.CrowdFactor(players);

        public static float FullHouseShare(float customers, float served, float load, int stars, int players, out string why)
        {
            float normal = NormalCustomers(stars, players);
            float crowd = normal > 0f ? customers / normal : 0f;
            float share = customers > 0f ? served / customers : 0f;
            why = $"{customers:0} customers ({crowd:0.00}x a normal {normal:0}), load {load:0.#}, {share:P0} served";
            if (share < FULL_HOUSE_SERVED || crowd <= 1f || load < FULL_HOUSE_MIN_LOAD) return 0f;
            return Mathf.Min(FULL_HOUSE_MAX, FULL_HOUSE_CROWD_SHARE * (crowd - 1f) * load / FULL_HOUSE_MIN_LOAD);
        }

        public const float VARIETY_PER_DISH = 0.04f, VARIETY_MAX = 0.40f;
        public static int MenuDishes { get; internal set; }
        public static float VarietyBonus => TycoonKitchenLoad.PriceBonus(TycoonKitchenLoad.Load);
        internal static int LastCourse;

        public const int DECAY_BOOST_FROM_STAR = 3;
        public const float DECAY_BOOST = 1.15f;

        public static float Decay(float reputation, int stars)
        {
            int s = TycoonEconomy.ClampStars(stars);
            float decay = DECAY_FLAT[s] + DECAY_SHARE[s] * Mathf.Max(0f, reputation - TOTAL_FOR_STAR[s]);
            return s >= DECAY_BOOST_FROM_STAR ? decay * DECAY_BOOST : decay;
        }

        public const float INSPECTION_CHANCE = 0.07f, INSPECTION_CHANCE_PER_STAR = 0.016f;
        public const float INSPECTION_PENALTY_PER_PROBLEM = 10f;
        public const float INSPECTION_PENALTY_SHARE = 0.005f;

        public static float InspectionPenalty(float reputation) => INSPECTION_PENALTY_PER_PROBLEM + INSPECTION_PENALTY_SHARE * Mathf.Max(0f, reputation);
        public const int INSPECTION_FINE_PER_PROBLEM = 5;
        public const float INSPECTION_BONUS = 20f;

        public const float TIP_CHANCE_MIN = 0.05f, TIP_CHANCE_MAX = 0.6f;
        public const float TIP_SHARE_MIN = 0.05f, TIP_SHARE_MAX = 0.25f;

        public const int CRITIC_NONE = 0, CRITIC_FOR_NEW_STAR = 1, CRITIC_TO_KEEP_STAR = 2;
        public const int RESULT_PENDING = 0, RESULT_HAPPY = 1, RESULT_UNHAPPY = 2, RESULT_LEFT = 3;

        public static STycoonReputation Current { get; internal set; }
        public static int FurnitureValue { get; internal set; }
        public static float CardBonus { get; internal set; }
        public static int CountedCards { get; internal set; }

        public static float GroupSeatShare
        {
            get
            {
                if (TycoonNetData.IsHost) return _groupSeatShare;
                return int.TryParse(TycoonNetData.Get(NET_KEY_GROUP_SEATS), out int p) ? Mathf.Clamp01(p / 100f) : 1f;
            }
            internal set => _groupSeatShare = Mathf.Clamp01(value);
        }
        private static float _groupSeatShare = 1f;
        public const string NET_KEY_GROUP_SEATS = "gss";

        public static int GroupIncrease(int unlockID)
        {
            if (KitchenData.GameData.Main == null || !KitchenData.GameData.Main.TryGet<KitchenData.Unlock>(unlockID, out KitchenData.Unlock u)) return 0;
            if (!(u is KitchenData.UnlockCard card) || card.Effects == null) return 0;
            int added = 0;
            foreach (KitchenData.UnlockEffect effect in card.Effects)
                if (effect is KitchenData.ParameterEffect p && p.Parameters.MaximumGroupSize > 0) added += p.Parameters.MaximumGroupSize;
            return added;
        }

        public static float CardValueNow(int unlockID)
        {
            float value = CardValue(unlockID);
            return GroupIncrease(unlockID) > 0 ? value * GroupSeatShare : value;
        }

        public static readonly float[] TOTAL_FOR_STAR = { 0f, 100f, 1000f, 2900f, 6650f, 15850f };
        public const float RISK_SHARE = 0.25f;
        public const float FLOOR = -100f;

        public static float TotalFor(int stars) => TOTAL_FOR_STAR[TycoonEconomy.ClampStars(stars)];

        public static float PromoteScore(int stars) =>
            stars >= TycoonEconomy.MAX_STARS ? 0f : TotalFor(stars + 1);

        public static float DemoteScore(int stars)
        {
            int s = TycoonEconomy.ClampStars(stars);
            if (s <= TycoonEconomy.MIN_STARS) return FLOOR;
            return TotalFor(s) - RISK_SHARE * (TotalFor(s) - TotalFor(s - 1));
        }

        public static float Progress(float reputation, int stars)
        {
            if (stars >= TycoonEconomy.MAX_STARS) return 1f;
            float from = TotalFor(stars), to = TotalFor(stars + 1);
            return Mathf.Clamp01((reputation - from) / (to - from));
        }

        public static float Level(float reputation, int stars) => (TycoonEconomy.ClampStars(stars) - TycoonEconomy.MIN_STARS) + Progress(reputation, stars);

        public static float PatienceModifier(float patienceLeft)
        {
            float p = Mathf.Clamp01(patienceLeft);
            return p >= PATIENCE_NEUTRAL
                ? PATIENCE_MODIFIER_MAX * (p - PATIENCE_NEUTRAL) / (1f - PATIENCE_NEUTRAL)
                : -PATIENCE_MODIFIER_MAX * (PATIENCE_NEUTRAL - p) / PATIENCE_NEUTRAL;
        }

        public static float ServeGain(float patienceLeft, float messes, int furnitureValue, float cardBonus)
        {
            float before = BASE_GAIN + PatienceModifier(patienceLeft) + cardBonus;
            return before - MessLoss(messes, TycoonEconomy.CurrentStars, before);
        }

        public static int Tip(int payment, float level, System.Random random)
        {
            if (payment <= 0) return 0;
            float t = Mathf.Clamp01(level / (TycoonEconomy.MAX_STARS - TycoonEconomy.MIN_STARS));
            float chance = Mathf.Min(0.95f, Mathf.Lerp(TIP_CHANCE_MIN, TIP_CHANCE_MAX, t) + TycoonPromotions.TipChance / 100f);
            if (random.NextDouble() >= chance) return 0;
            float share = Mathf.Lerp(TIP_SHARE_MIN, TIP_SHARE_MAX, t);
            return Mathf.Max(1, Mathf.RoundToInt(payment * share));
        }

        public const float FAST_CASUAL_TIP_CHANCE_FACTOR = 1.5f, FAST_CASUAL_TIP_CHANCE_BONUS = 0.15f, FAST_CASUAL_TIP_SHARE_FACTOR = 1.5f;

        public static int FastCasualTip(int payment, float level, System.Random random)
        {
            if (payment <= 0) return 0;
            float t = Mathf.Clamp01(level / (TycoonEconomy.MAX_STARS - TycoonEconomy.MIN_STARS));
            float chance = Mathf.Min(0.95f, Mathf.Lerp(TIP_CHANCE_MIN, TIP_CHANCE_MAX, t) * FAST_CASUAL_TIP_CHANCE_FACTOR + FAST_CASUAL_TIP_CHANCE_BONUS);
            if (random.NextDouble() >= chance) return 0;
            float share = Mathf.Lerp(TIP_SHARE_MIN, TIP_SHARE_MAX, t) * FAST_CASUAL_TIP_SHARE_FACTOR;
            return Mathf.Max(1, Mathf.RoundToInt(payment * share));
        }

        public static void Add(ref STycoonReputation rep, float amount)
        {
            rep.Reputation = Mathf.Max(FLOOR, rep.Reputation + amount);
            rep.GainedToday += amount;
        }

        public static float SavedAt(STycoonReputation rep, int stars) =>
            stars == 1 ? rep.SavedReputation1 : stars == 2 ? rep.SavedReputation2 : stars == 3 ? rep.SavedReputation3 : stars == 4 ? rep.SavedReputation4 : 0f;

        public static void SaveAt(ref STycoonReputation rep, int stars, float value)
        {
            if (stars == 1) rep.SavedReputation1 = value;
            else if (stars == 2) rep.SavedReputation2 = value;
            else if (stars == 3) rep.SavedReputation3 = value;
            else if (stars == 4) rep.SavedReputation4 = value;
        }

        public static string StarsText(int stars) => stars == 1 ? "1 Star" : $"{stars} Stars";

        public static string CriticText(int criticToday) =>
            criticToday == CRITIC_FOR_NEW_STAR ? "a food critic is coming (new star)"
            : criticToday == CRITIC_TO_KEEP_STAR ? "a food critic is coming (star at risk)" : "no critic";
    }
}
