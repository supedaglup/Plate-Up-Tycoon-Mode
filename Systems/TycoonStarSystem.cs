using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Closing time, before the bill: the food critic's verdict.</summary>
    [UpdateInGroup(typeof(EndOfDayProgressionGroup))]
    [UpdateBefore(typeof(TycoonClosingSystem))]
    public class TycoonStarSystem : RestaurantSystem, IModSystem
    {
        private const ViewType STAR_POPUP_VIEW = (ViewType)36;
        private const int OPTION_TRIES = 8;

        internal static int DebugStarChange;

        private EntityQuery _cards;
        private EntityQuery _unlocks;

        protected override void Initialise()
        {
            base.Initialise();
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard)));
            _unlocks = GetEntityQuery(new QueryHelper().All(typeof(CProgressionUnlock)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<SIsNightFirstUpdate>();
            RequireSingletonForUpdate<STycoonReputation>();
            RequireSingletonForUpdate<STycoonEconomy>();
        }

        protected override void OnUpdate()
        {
            int day = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            if (day < 1 || HasSingleton<SIsRestartedDay>()) return;

            STycoonReputation rep = GetSingleton<STycoonReputation>();
            STycoonEconomy economy = GetSingleton<STycoonEconomy>();
            int stars = TycoonEconomy.ClampStars(economy.Stars);
            int change = 0;
            string verdict = "no critic today";

            float looks = TycoonVisibleDecor.DailyBonus(EntityManager, TileManager, Bounds, out string looksDetails);
            float ambience = TycoonReputation.Ambience(looks, stars);
            float ambienceGain = TycoonReputationReport.ServeGains > 0f ? TycoonReputationReport.ServeGains * ambience : 0f;
            if (ambienceGain != 0f) TycoonReputation.Add(ref rep, ambienceGain);
            TycoonReputationReport.Note("Ambience", ambienceGain);
            TycoonNetData.Set(TycoonReputation.AMBIENCE_NET_KEY, Mathf.RoundToInt(ambience * 100f).ToString("+0;-0;0"));
            float quality = TycoonIngredientQuality.RepShare(stars);
            float qualityGain = TycoonReputationReport.ServeGains > 0f ? TycoonReputationReport.ServeGains * quality : 0f;
            if (qualityGain != 0f) TycoonReputation.Add(ref rep, qualityGain);
            TycoonReputationReport.Note("Ingredients", qualityGain);
            TycoonNetData.Set(TycoonIngredientQuality.NET_KEY_PERCENT, Mathf.RoundToInt(quality * 100f).ToString("+0;-0;0"));
            Mod.LogInfo($"Reputation: ingredients {TycoonIngredientQuality.NAMES[TycoonIngredientQuality.Current]} at {stars} star(s) (expected {TycoonIngredientQuality.NAMES[TycoonIngredientQuality.Required(stars)]}): {quality * 100f:+0;-0;0}% = {qualityGain:+0.#;-0.#;0}.");
            Mod.LogInfo($"Reputation: looks of the restaurant {looks:0.##} points ({looksDetails}); ambience {ambience * 100f:+0;-0;0}% at {stars} star(s) (expected {TycoonReputation.AMBIENCE_EXPECTED[Mathf.Clamp(stars, 0, 5)]}): {ambienceGain:+0.#;-0.#;0} of {TycoonReputationReport.ServeGains:0.#} serving.");

            float fullHouse = TycoonReputation.FullHouseShare(TycoonReputation.CustomersToday, TycoonReputationReport.People, TycoonKitchenLoad.Load, stars, TycoonKitchenLoad.Players, out string fullHouseWhy);
            if (fullHouse > 0f && TycoonReputationReport.ServeGains > 0f)
            {
                float bonus = TycoonReputationReport.ServeGains * fullHouse;
                TycoonReputation.Add(ref rep, bonus);
                TycoonReputationReport.Note($"Full House! {fullHouseWhy}, +{fullHouse:P0} of serving", bonus);
                ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), $"Full House! +{bonus:0} reputation", 8f);
            }
            Mod.LogInfo($"Reputation: Full House {(fullHouse > 0f ? $"+{fullHouse:P0}" : "no")} ({fullHouseWhy}).");

            float decay = TycoonReputation.Decay(rep.Reputation, stars) * (1f - TycoonPromotions.DecayCut / 100f);
            float above = Mathf.Max(0f, rep.Reputation - TycoonReputation.TotalFor(stars));
            TycoonReputationReport.DecayAmount = decay;
            TycoonReputationReport.DecayText = $"{TycoonReputation.DECAY_FLAT[stars]:0} at {TycoonReputation.StarsText(stars)} + {TycoonReputation.DECAY_SHARE[stars] * 100f:0}% of the {above:0} above {TycoonReputation.TotalFor(stars):0}"
                + (stars >= TycoonReputation.DECAY_BOOST_FROM_STAR ? $", x{TycoonReputation.DECAY_BOOST:0.00} at {TycoonReputation.DECAY_BOOST_FROM_STAR}+ stars" : "")
                + (TycoonPromotions.DecayCut > 0f ? $", {TycoonPromotions.DecayCut:0}% less from Marketing" : "");
            TycoonReputation.Add(ref rep, -decay);
            string inspection = SettleInspection(ref rep, stars);
            Mod.LogInfo($"Reputation: daily decay -{decay:0.00} at {stars} star(s){inspection}.");

            if (rep.CriticToday == TycoonReputation.CRITIC_FOR_NEW_STAR)
            {
                if (rep.CriticResult == TycoonReputation.RESULT_HAPPY && stars < TycoonEconomy.MAX_STARS) { change = 1; verdict = "critic happy: NEW STAR"; }
                else verdict = rep.CriticResult == TycoonReputation.RESULT_PENDING ? "critic never came" : "critic not happy: no new star yet";
            }
            else if (rep.CriticToday == TycoonReputation.CRITIC_TO_KEEP_STAR)
            {
                bool failed = rep.CriticResult == TycoonReputation.RESULT_UNHAPPY || rep.CriticResult == TycoonReputation.RESULT_LEFT;
                if (failed && stars > TycoonEconomy.MIN_STARS) { change = -1; verdict = "critic unhappy: STAR LOST"; }
                else verdict = rep.CriticResult == TycoonReputation.RESULT_HAPPY ? "critic happy: star kept" : "critic never came: star kept";
            }

            if (DebugStarChange != 0)
            {
                int forced = DebugStarChange;
                DebugStarChange = 0;
                if (forced > 0 && stars < TycoonEconomy.MAX_STARS) { change = 1; verdict = "DEBUG: star awarded"; }
                else if (forced < 0 && stars > TycoonEconomy.MIN_STARS) { change = -1; verdict = "DEBUG: star removed"; }
                else verdict = $"DEBUG: star change ignored (already {TycoonReputation.StarsText(stars)})";
            }

            if (change != 0)
            {
                economy.Stars = stars + change;
                Set(economy);
                TycoonEconomy.CurrentStars = economy.Stars;
                if (change > 0)
                {
                    ShowStarPopup(economy.Stars);
                    ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), $"NEW STAR! {TycoonReputation.StarsText(economy.Stars)}", 10f);
                }
                else
                {
                    ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), $"The critic was unimpressed: star lost ({economy.Stars} left)", 10f);
                }
            }

            rep.LastStarChange = change;
            rep.CriticToday = TycoonReputation.CRITIC_NONE;
            Set(rep);
            TycoonReputation.Current = rep;
            Mod.LogInfo($"Reputation day {day} closed: {rep.GainedToday:+0.00;-0.00} today ({rep.ServedToday} served, {rep.TipsToday} coins in tips), score now {rep.Reputation:0.00}; {verdict}; stars {stars} -> {economy.Stars}.");
        }

        private string SettleInspection(ref STycoonReputation rep, int stars)
        {
            if (!HasSingleton<STycoonInspection>()) return "";
            STycoonInspection inspection = GetSingleton<STycoonInspection>();
            inspection.FineToday = 0;
            string result = "";
            if (inspection.Stage >= 2)
            {
                int problems = inspection.MessesSeen + inspection.FullBinsSeen;
                if (problems > 0)
                {
                    float loss = TycoonReputation.InspectionPenalty(rep.Reputation) * problems;
                    TycoonReputation.Add(ref rep, -loss);
                    TycoonReputationReport.Note($"Health inspection: {problems} problem(s)", -loss);
                    inspection.FineToday = Mathf.CeilToInt(TycoonReputation.INSPECTION_FINE_PER_PROBLEM * problems * TycoonEconomy.CostMultiplier(stars));
                    result = $"; health inspection FAILED ({inspection.MessesSeen} mess(es), {inspection.FullBinsSeen} full bin(s)): "
                        + $"-{loss:0} reputation, fine {inspection.FineToday}";
                }
                else if (inspection.Served)
                {
                    TycoonReputation.Add(ref rep, TycoonReputation.INSPECTION_BONUS);
                    TycoonReputationReport.Note("Health inspection: spotless", TycoonReputation.INSPECTION_BONUS);
                    result = $"; health inspection spotless and served: +{TycoonReputation.INSPECTION_BONUS:0} reputation";
                }
                else result = "; health inspection spotless (inspector not served, no bonus)";
            }
            inspection.Stage = 0;
            Set(inspection);
            return result;
        }

        private void ShowStarPopup(int newStars)
        {
            Entity popup = EntityManager.CreateEntity(typeof(CPopup), typeof(CHideView), typeof(CStarIncreasePopup), typeof(CRequiresView), typeof(CPosition), typeof(CCaptureInput));
            EntityManager.SetComponentData(popup, new CPopup { Priority = Kitchen.PopupPriority.StarIncrease });
            EntityManager.SetComponentData(popup, new CStarIncreasePopup { StarCount = newStars });
            EntityManager.SetComponentData(popup, new CCaptureInput { AllUsers = true });
            EntityManager.SetComponentData(popup, new CRequiresView { Type = STAR_POPUP_VIEW, ViewMode = ViewMode.Screen });
            EntityManager.SetComponentData(popup, new CPosition(new Vector3(0.5f, 0.5f, 0f)));
        }

        [System.Obsolete("Not called any more: Recipe Mastery gives the add-ons.")]
        private void OfferCards(int newStar, int day)
        {
            var known = new HashSet<int> { Mod.STARTING_DISH };
            var have = new HashSet<int>();
            using (NativeArray<Entity> entities = _cards.ToEntityArray(Allocator.Temp))
            using (NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                for (int i = 0; i < cards.Length; i++)
                {
                    known.Add(cards[i].UnlockID);
                    if (cards[i].Vanilla || Has<CTycoonCardApplied>(entities[i])) have.Add(cards[i].UnlockID);
                }
            using (NativeArray<CProgressionUnlock> unlocks = _unlocks.ToComponentDataArray<CProgressionUnlock>(Allocator.Temp))
                for (int i = 0; i < unlocks.Length; i++) { known.Add(unlocks[i].ID); have.Add(unlocks[i].ID); }

            List<Dish> related = RelatedRecipes(known, have);
            Mod.LogInfo($"Stars: upgrades for this restaurant's recipes: {(related.Count == 0 ? "none" : string.Join(", ", related.ConvertAll(d => $"{d.Name} ({d.Type})")))}.");

            var picks = new List<Unlock>();
            TakeRandom(related.FindAll(d => d.Type == DishType.Extra), picks, 2);
            TakeRandom(related, picks, 2);

            if (picks.Count < 2)
                foreach (Unlock u in FromPack(known, newStar, day, allowNewItems: false))
                {
                    if (picks.Count >= 2) break;
                    if (u != null && !picks.Contains(u)) picks.Add(u);
                }

            if (picks.Count == 0) Mod.LogWarning($"Stars: nothing to offer for star {newStar}.");
            foreach (Unlock u in picks) AddOfferedCard(u, newStar);
        }

        private static List<Dish> RelatedRecipes(HashSet<int> known, HashSet<int> have)
        {
            var list = new List<Dish>();
            foreach (Dish d in GameData.Main.Get<Dish>())
            {
                if (d == null || !d.IsUnlockable || d.Type == DishType.Base || known.Contains(d.ID)) continue;
                if (d.Requires == null || d.Requires.Count == 0) continue;
                bool ok = true, onOwnRecipe = false;
                foreach (Unlock r in d.Requires)
                {
                    if (r == null || !have.Contains(r.ID)) { ok = false; break; }
                    if (r is Dish) onOwnRecipe = true;
                }
                if (ok && d.BlockedBy != null)
                    foreach (Unlock b in d.BlockedBy) if (b != null && known.Contains(b.ID)) { ok = false; break; }
                if (ok && onOwnRecipe) list.Add(d);
            }
            return list;
        }

        private static void TakeRandom(List<Dish> from, List<Unlock> into, int upTo)
        {
            var pool = from.FindAll(d => !into.Contains(d));
            while (into.Count < upTo && pool.Count > 0)
            {
                int i = Random.Range(0, pool.Count);
                into.Add(pool[i]);
                pool.RemoveAt(i);
            }
        }

        private IEnumerable<Unlock> FromPack(HashSet<int> known, int newStar, int day, bool allowNewItems)
        {
            int packID = HasSingleton<CUnlockPack>() ? GetSingleton<CUnlockPack>().ActiveUnlockPack : AssetReference.DefaultUnlockPack;
            if (!GameData.Main.TryGet<UnlockPack>(packID, out UnlockPack pack))
            {
                Mod.LogWarning($"Stars: card pack {packID} not found.");
                yield break;
            }
            var days = new List<int> { Mathf.Max(day, 3 * newStar), 3 * newStar };
            for (int d = 3; d <= 15; d += 3) days.Add(d);
            foreach (int askDay in days)
                for (int skip = 0; skip < OPTION_TRIES; skip++)
                {
                    UnlockOptions options = pack.GetOptions(known, new UnlockRequest(askDay, 0), skip);
                    if (options.Unlock1 == null && options.Unlock2 == null) break;
                    foreach (Unlock u in new[] { options.Unlock1, options.Unlock2 })
                        if (u != null && (allowNewItems || !(u is Dish))) yield return u;
                }
        }

        private void AddOfferedCard(Unlock unlock, int star)
        {
            Entity card = EntityManager.CreateEntity(typeof(CTycoonCard));
            EntityManager.SetComponentData(card, new CTycoonCard { UnlockID = unlock.ID, Star = star, Enabled = false, Vanilla = false });
            Mod.LogInfo($"Stars: offered '{unlock.Name}' ({(unlock is Dish dish ? "recipe, " + dish.Type : "card")}) for star {star}; off until switched on at the Management Desk.");
        }
    }
}
