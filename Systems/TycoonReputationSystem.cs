using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Host side, every restaurant frame: keeps STycoonReputation.</summary>
    public class TycoonReputationSystem : RestaurantSystem, IModSystem
    {
        private static readonly Vector3 CARRIER_POSITION = new Vector3(0f, -50f, 0f);
        private const float CRITIC_LATEST = 0.5f;

        internal static Entity CriticGroup = Entity.Null;
        internal static bool CriticArrivedPending;

        private EntityQuery _scheduled;
        private EntityQuery _carriers;
        private EntityQuery _appliances;
        private EntityQuery _cards;

        protected override void Initialise()
        {
            base.Initialise();
            _scheduled = GetEntityQuery(new QueryHelper().All(typeof(CScheduledCustomer)));
            _carriers = GetEntityQuery(new QueryHelper().All(typeof(CTycoonReputationCarrier)));
            _appliances = GetEntityQuery(new QueryHelper().All(typeof(CAppliance)));
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<STycoonEconomy>();
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<STycoonReputation>())
            {
                Set(new STycoonReputation());
                Mod.LogInfo("Reputation: starting from 0.");
            }
            STycoonReputation rep = GetSingleton<STycoonReputation>();
            int stars = TycoonEconomy.CurrentStars;
            bool changed = false;
            using (NativeArray<Entity> cardEntities = _cards.ToEntityArray(Allocator.Temp))
            using (NativeArray<CTycoonCard> cardData = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                UpdateGroupSeatShare(cardEntities, cardData);
            if (!HasSingleton<STycoonRunningReputation>())
            {
                float before = rep.Reputation;
                rep.Reputation += TycoonReputation.TotalFor(stars);
                Set(new STycoonRunningReputation { Version = 1 });
                changed = true;
                Mod.LogInfo($"Reputation: now a running total ({before:0.00} at {stars} star(s) becomes {rep.Reputation:0.00}).");
            }
            bool practice = HasSingleton<SPracticeMode>();

            if (HasSingleton<SIsDayFirstUpdate>() && !practice)
            {
                StartDay(ref rep, stars);
                changed = true;
            }

            if (HasSingleton<SIsDayTime>() && !practice && rep.CriticToday != TycoonReputation.CRITIC_NONE)
            {
                if (CriticGroup == Entity.Null) BookCritic(rep);
                else if (CriticArrivedPending)
                {
                    CriticArrivedPending = false;
                    Mod.LogInfo("Critic: the food critic (cat) has arrived.");
                    ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), "The food critic has arrived!", 6f);
                }
                else if (rep.CriticResult == TycoonReputation.RESULT_PENDING
                         && (!EntityManager.Exists(CriticGroup) || Has<CGroupStartLeaving>(CriticGroup)))
                {
                    rep.CriticResult = TycoonReputation.RESULT_LEFT;
                    changed = true;
                    Mod.LogInfo("Critic: the food critic left without being served.");
                }
            }

            if (changed) Set(rep);
            TycoonReputation.Current = rep;
            UpdateCarrier(rep, stars);
        }

        private void StartDay(ref STycoonReputation rep, int stars)
        {
            TycoonReputationReport.Reset(HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0);
            rep.GainedToday = 0f;
            rep.ServedToday = 0;
            rep.TipsToday = 0;
            rep.CriticGain = 0f;
            rep.CriticResult = TycoonReputation.RESULT_PENDING;
            rep.LastStarChange = 0;
            CriticGroup = Entity.Null;
            CriticArrivedPending = false;

            if (stars < TycoonEconomy.MAX_STARS && rep.Reputation >= TycoonReputation.PromoteScore(stars))
                rep.CriticToday = TycoonReputation.CRITIC_FOR_NEW_STAR;
            else if (stars > TycoonEconomy.MIN_STARS && rep.Reputation <= TycoonReputation.DemoteScore(stars))
                rep.CriticToday = TycoonReputation.CRITIC_TO_KEEP_STAR;
            else
                rep.CriticToday = TycoonReputation.CRITIC_NONE;

            TycoonReputation.FurnitureValue = CountFurniture();
            CountCards();
            int day = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            Mod.LogInfo($"Reputation day {day}: {rep.Reputation:0.00}, {stars} star(s), next star at {TycoonReputation.PromoteScore(stars):0}, at risk at {TycoonReputation.DemoteScore(stars):0}; "
                + $"furniture worth {TycoonReputation.FurnitureValue} (its reputation comes at closing), "
                + $"{TycoonReputation.CountedCards} card(s) worth +{TycoonReputation.CardBonus:0.##} per serve; {TycoonReputation.CriticText(rep.CriticToday)}.");
        }

        private void BookCritic(STycoonReputation rep)
        {
            if (_scheduled.IsEmpty) return;
            using NativeArray<Entity> entities = _scheduled.ToEntityArray(Allocator.Temp);
            using NativeArray<CScheduledCustomer> scheduled = _scheduled.ToComponentDataArray<CScheduledCustomer>(Allocator.Temp);
            float now = HasSingleton<STime>() ? GetSingleton<STime>().TimeOfDay : 0f;
            var early = new System.Collections.Generic.List<int>();
            var later = new System.Collections.Generic.List<int>();
            for (int i = 0; i < scheduled.Length; i++)
            {
                if (scheduled[i].IsCat) return;
                if (scheduled[i].TimeOfDay <= now + 0.02f) continue;
                if (scheduled[i].TimeOfDay <= CRITIC_LATEST) early.Add(i); else later.Add(i);
            }
            System.Collections.Generic.List<int> pool = early.Count > 0 ? early : later;
            int pick = pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : Random.Range(0, scheduled.Length);
            CScheduledCustomer critic = scheduled[pick];
            critic.IsCat = true;
            EntityManager.SetComponentData(entities[pick], critic);
            Mod.LogInfo($"Critic: booked as the customer due at {critic.TimeOfDay:0.00} of the day ({TycoonReputation.CriticText(rep.CriticToday)}).");
        }

        private int CountFurniture()
        {
            int value = 0;
            using NativeArray<Entity> entities = _appliances.ToEntityArray(Allocator.Temp);
            using NativeArray<CAppliance> appliances = _appliances.ToComponentDataArray<CAppliance>(Allocator.Temp);
            for (int i = 0; i < appliances.Length; i++)
            {
                if (!TycoonCatalogue.TryGetAny(appliances[i].ID, out TycoonCatalogue.Entry entry)) continue;
                if (entry.Category != TycoonCatalogue.Category.Decorations && !Has<CGivesDecoration>(entities[i])) continue;
                value += entry.Cost;
            }
            return value;
        }

        private void CountCards()
        {
            int n = 0, dishes = 0;
            float bonus = 0f;
            using NativeArray<Entity> entities = _cards.ToEntityArray(Allocator.Temp);
            using NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp);
            UpdateGroupSeatShare(entities, cards);
            for (int i = 0; i < cards.Length; i++)
            {
                if ((cards[i].Vanilla || Has<CTycoonCardApplied>(entities[i])) && TycoonCards.IsRecipe(cards[i].UnlockID)) dishes++;
                if (cards[i].Star <= 0 || !(cards[i].Vanilla || Has<CTycoonCardApplied>(entities[i]))) continue;
                float value = TycoonReputation.CardValueNow(cards[i].UnlockID);
                if (value <= 0f) continue;
                n++;
                bonus += value;
            }
            TycoonReputation.CountedCards = n;
            TycoonReputation.MenuDishes = dishes;
            TycoonReputation.CardBonus = bonus;
        }

        private float _loggedShare = -1f;

        private void UpdateGroupSeatShare(NativeArray<Entity> entities, NativeArray<CTycoonCard> cards)
        {
            int added = 0;
            for (int i = 0; i < cards.Length; i++)
                if (cards[i].Vanilla || Has<CTycoonCardApplied>(entities[i])) added += TycoonReputation.GroupIncrease(cards[i].UnlockID);
            float share = 1f;
            int largestGroup = 0, largestTable = 0;
            if (added > 0 && HasSingleton<SKitchenParameters>() && HasSingleton<SLargestTableSize>())
            {
                largestGroup = GetSingleton<SKitchenParameters>().Parameters.MaximumGroupSize;
                largestTable = GetSingleton<SLargestTableSize>().LargestTableSize;
                int overflow = Mathf.Max(0, largestGroup - largestTable);
                share = Mathf.Clamp01((added - overflow) / (float)added);
            }
            TycoonReputation.GroupSeatShare = share;
            TycoonNetData.Set(TycoonReputation.NET_KEY_GROUP_SEATS, Mathf.RoundToInt(share * 100f).ToString());
            if (Mathf.Abs(share - _loggedShare) > 0.001f)
            {
                _loggedShare = share;
                if (added > 0) Mod.LogInfo($"Reputation: group size cards add {added} to the largest group ({largestGroup}); the largest table seats {largestTable}: they earn {share:P0} of their reputation.");
            }
        }

        private void UpdateCarrier(STycoonReputation rep, int stars)
        {
            Entity carrier;
            if (_carriers.IsEmpty)
            {
                carrier = EntityManager.CreateEntity(typeof(CTycoonReputationCarrier), typeof(CRequiresView), typeof(CPosition), typeof(CProgressIndicator), typeof(CDoNotPersist));
                EntityManager.SetComponentData(carrier, new CRequiresView { Type = ViewType.ProgressView });
                EntityManager.SetComponentData(carrier, new CPosition(CARRIER_POSITION));
            }
            else carrier = _carriers.GetSingletonEntity();

            EntityManager.SetComponentData(carrier, new CProgressIndicator
            {
                Progress = TycoonReputation.Progress(rep.Reputation, stars),
                Process = Patches.TycoonReputationBar.Encode(stars, rep.Reputation),
            });
        }
    }
}
