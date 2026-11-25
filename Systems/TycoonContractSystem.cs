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
    /// <summary>Singleton: the booked Contract.</summary>
    public struct STycoonContract : IComponentData, IModComponent
    {
        public int Slot;
        public bool Pending, Active, Spawned, Resolved;
        public int Groups, Served, Failed;
    }

    /// <summary>Today is a luxury contract's day: walkouts counted from WalkoutsAtStart.</summary>
    public struct STycoonLuxuryDay : IComponentData, IModComponent
    {
        public int WalkoutsAtStart;
    }

    /// <summary>A customer group already dressed for today's luxury contract.</summary>
    public struct CTycoonLuxuryDressed : IComponentData, IModComponent { }

    /// <summary>Tag on a contract customer group.</summary>
    public struct CTycoonContractGroup : IComponentData, IModComponent
    {
        public int Slot;
        public bool Served, Counted;
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Contracts: books, spawns and settles the day's contract group.</summary>
    public class TycoonContractSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _groups;
        private EntityQuery _scheduled;
        private int _customerType;
        private int _lastStarted = -1;
        private string _offersKey = "";
        private EntityQuery _undressed;

        public const float DEPOSIT_SHARE = 0.25f;
        public const float LUXURY_DEPOSIT_SHARE = 0.4f;
        internal static int EarnedCoins;
        internal static float EarnedRep;
        public static int DepositFor(TycoonPromotions.Contract contract) =>
            contract == null ? 0 : Mathf.RoundToInt(contract.RewardCoins * (contract.Luxury ? LUXURY_DEPOSIT_SHARE : DEPOSIT_SHARE) / 5f) * 5;

        public static bool LuxuryActive;
        public static float LuxuryPatience = 1f;

        protected override void Initialise()
        {
            base.Initialise();
            _groups = GetEntityQuery(new QueryHelper().All(typeof(CTycoonContractGroup), typeof(CCustomerGroup)));
            _scheduled = GetEntityQuery(new QueryHelper().All(typeof(CScheduledCustomer), typeof(CCustomerType)));
            _undressed = GetEntityQuery(new QueryHelper().All(typeof(CCustomerGroup)).None(typeof(CTycoonLuxuryDressed)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            LuxuryActive = false;
            if (!_scheduled.IsEmpty)
                using (NativeArray<CCustomerType> types = _scheduled.ToComponentDataArray<CCustomerType>(Allocator.Temp))
                    if (types.Length > 0) _customerType = types[0].Type;

            int today = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            int stars = TycoonEconomy.CurrentStars;
            string offersKey = today + ":" + TycoonCompany.ActiveSlot + ":" + stars;
            if (offersKey != _offersKey)
            {
                _offersKey = offersKey;
                TycoonNetData.Set("cto", string.Join(",", TycoonPromotions.ContractOffers(today, stars)));
            }
            if (!HasSingleton<STycoonContract>()) { TycoonNetData.Set("ct", ""); TycoonNetData.Set("ctb", ""); return; }
            TycoonNetData.Set("ctb", GetSingleton<STycoonContract>().Pending ? GetSingleton<STycoonContract>().Slot.ToString() : "");
            STycoonContract c = GetSingleton<STycoonContract>();
            TycoonPromotions.Contract contract = TycoonPromotions.ContractAt(c.Slot);
            int day = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;

            if (HasSingleton<SIsDayFirstUpdate>() && _lastStarted != day && c.Pending)
            {
                _lastStarted = day;
                c = new STycoonContract { Slot = c.Slot, Active = true };
                EarnedCoins = 0; EarnedRep = 0f;
                Set(c);
                Mod.LogInfo($"Contract: today - {contract?.Name}.");
                if (contract != null && contract.Luxury)
                {
                    int total = HasSingleton<STycoonEconomy>() ? GetSingleton<STycoonEconomy>().WalkoutsTotal : 0;
                    if (HasSingleton<STycoonLuxuryDay>()) EntityManager.DestroyEntity(GetSingletonEntity<STycoonLuxuryDay>());
                    Entity luxury = EntityManager.CreateEntity(typeof(STycoonLuxuryDay));
                    EntityManager.SetComponentData(luxury, new STycoonLuxuryDay { WalkoutsAtStart = total });
                    string text = $"{contract.Name}: every guest today is a VIP!";
                    ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), text.Length > 58 ? text.Substring(0, 58) : text, 8f);
                }
            }
            if (HasSingleton<SIsNightFirstUpdate>() && c.Active)
            {
                if (contract != null && contract.Luxury && !c.Resolved)
                {
                    int walkouts = LuxuryWalkouts();
                    Resolve(ref c, contract, walkouts <= contract.Size, $"the whole day, {walkouts} walkout(s) of {contract.Size} allowed");
                }
                if (HasSingleton<STycoonLuxuryDay>()) EntityManager.DestroyEntity(GetSingletonEntity<STycoonLuxuryDay>());
                if (c.Spawned && !c.Resolved) Resolve(ref c, contract, false, "still waiting when the day ended");
                EntityManager.DestroyEntity(GetSingletonEntity<STycoonContract>());
                TycoonNetData.Set("ct", "");
                return;
            }

            if (contract != null && contract.Luxury && c.Active)
            {
                TycoonNetData.Set("ct", "");
                if (c.Resolved || !HasSingleton<SIsDayTime>()) return;
                LuxuryActive = true;
                LuxuryPatience = contract.Patience / 100f;
                DressEveryone(contract);
                int walkouts = LuxuryWalkouts();
                if (walkouts > contract.Size) Resolve(ref c, contract, false, $"{walkouts} walkout(s), only {contract.Size} allowed");
                return;
            }

            TycoonNetData.Set("ct", c.Active && contract != null && !c.Spawned ? Mathf.RoundToInt(contract.Arrival * 1000f).ToString() : "");
            if (!c.Active || contract == null || !HasSingleton<SIsDayTime>()) return;

            if (!c.Spawned && HasSingleton<STime>() && GetSingleton<STime>().TimeOfDay >= contract.Arrival && !TycoonDayPhases.InOpening)
            {
                c.Groups = Spawn(c.Slot, contract);
                c.Spawned = true;
                Set(c);
            }
            if (c.Spawned && !c.Resolved) Track(ref c, contract);
        }

        private int Spawn(int slot, TycoonPromotions.Contract contract)
        {
            CreateCustomerGroup creator = World.GetExistingSystem<CreateCustomerGroup>();
            CustomerType type = null;
            if (_customerType != 0) GameData.Main.TryGet<CustomerType>(_customerType, out type, false);
            if (type == null) foreach (CustomerType t in GameData.Main.Get<CustomerType>()) { type = t; break; }
            if (creator == null || type == null) { Mod.LogWarning("Contract: could not make the group (no customer type)."); return 0; }

            Entity first = creator.NewGroup(type, contract.Size, false);
            var groups = new List<Entity> { first };
            if (Has<CTycoonParty>(first))
            {
                int party = EntityManager.GetComponentData<CTycoonParty>(first).PartyID;
                using EntityQuery q = EntityManager.CreateEntityQuery(typeof(CTycoonParty), typeof(CCustomerGroup));
                using NativeArray<Entity> all = q.ToEntityArray(Allocator.Temp);
                foreach (Entity g in all) if (g != first && EntityManager.GetComponentData<CTycoonParty>(g).PartyID == party) groups.Add(g);
            }
            bool dressed = Outfit(contract.Outfit, out DataObjectList outfit);
            foreach (Entity g in groups)
            {
                EntityManager.AddComponentData(g, new CTycoonContractGroup { Slot = slot });
                if (!dressed || !EntityManager.HasComponent<CGroupMember>(g)) continue;
                using NativeArray<CGroupMember> members = EntityManager.GetBuffer<CGroupMember>(g).ToNativeArray(Allocator.Temp);
                foreach (CGroupMember m in members)
                    if (EntityManager.Exists(m.Customer) && Has<CPlayerCosmetics>(m.Customer))
                        EntityManager.SetComponentData(m.Customer, new CPlayerCosmetics { Cosmetics = outfit });
            }
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), $"Contract arriving: {Short(contract.Name)}");
            Mod.LogInfo($"Contract: '{contract.Name}' arrived ({contract.Size} guest(s) in {groups.Count} group(s), outfit {contract.Outfit}).");
            return groups.Count;
        }

        private static bool Outfit(string names, out DataObjectList outfit)
        {
            outfit = default;
            if (string.IsNullOrEmpty(names) || names.Trim().ToLowerInvariant() == "none") return false;
            var ids = new FixedListInt64();
            foreach (string raw in names.Split('+'))
            {
                System.Reflection.PropertyInfo p = typeof(KitchenLib.References.PlayerCosmeticReferences).GetProperty(raw.Trim(), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (p == null || p.PropertyType != typeof(int)) { Mod.LogWarning($"Contract: unknown outfit '{raw.Trim()}'."); continue; }
                int id = (int)p.GetValue(null);
                if (GameData.Main.TryGet<PlayerCosmetic>(id, out PlayerCosmetic _, false) && ids.Length < ids.Capacity) ids.Add(id);
            }
            if (ids.Length == 0) return false;
            outfit = new DataObjectList(ids);
            return true;
        }

        private void Track(ref STycoonContract c, TycoonPromotions.Contract contract)
        {
            bool changed = false;
            using (NativeArray<Entity> groups = _groups.ToEntityArray(Allocator.Temp))
                foreach (Entity g in groups)
                {
                    CTycoonContractGroup tag = EntityManager.GetComponentData<CTycoonContractGroup>(g);
                    if (tag.Counted || !(Has<CGroupLeaving>(g) || Has<CGroupStartLeaving>(g))) continue;
                    tag.Counted = true;
                    EntityManager.SetComponentData(g, tag);
                    if (tag.Served) c.Served++; else c.Failed++;
                    changed = true;
                }
            if (c.Failed > 0) { Resolve(ref c, contract, false, "a guest walked out"); return; }
            if (c.Groups > 0 && c.Served >= c.Groups) { Resolve(ref c, contract, true, "everyone served"); return; }
            if (changed) Set(c);
        }

        private void Resolve(ref STycoonContract c, TycoonPromotions.Contract contract, bool success, string why)
        {
            c.Resolved = true;
            Set(c);
            if (contract == null) return;
            int deposit = DepositFor(contract);
            int coins = success ? contract.RewardCoins + deposit : -(contract.FailCoins + Mathf.Max(0, EarnedCoins));
            float rep = success ? contract.RewardRep : -(contract.FailRep + Mathf.Max(0f, EarnedRep));
            Mod.LogInfo($"Contract '{contract.Name}': guests earned {EarnedCoins} coins and {EarnedRep:0.#} reputation today; deposit {deposit}.");
            if (coins != 0 && HasSingleton<SMoney>())
            {
                SMoney money = GetSingleton<SMoney>();
                money.Amount += coins;
                Set(money);
                MoneyTracker.AddEvent(new EntityContext(EntityManager), success ? 0 : Mod.Ledger.Purchases, coins);
            }
            if (rep != 0f && HasSingleton<STycoonReputation>())
            {
                STycoonReputation r = GetSingleton<STycoonReputation>();
                TycoonReputation.Add(ref r, rep);
                TycoonReputationReport.Note((contract.Luxury ? "Luxury contract: " : "Contract: ") + contract.Name, rep);
                Set(r);
                TycoonReputation.Current = r;
            }
            string text = success ? $"{(contract.Luxury ? "Luxury day done" : "Contract done")}! +{coins} coins, +{rep:0} rep"
                : $"{(contract.Luxury ? "Luxury day failed" : "Contract failed")}: {rep:0} rep{(coins != 0 ? $", {coins} coins" : "")}";
            ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, GetFrontDoor(false), text, 6f);
            Mod.LogInfo($"Contract '{contract.Name}': {(success ? "SUCCESS" : "FAILED")} ({why}; {c.Served}/{c.Groups} served) - {coins:+0;-0} coins, {rep:+0;-0} reputation.");
        }

        private static string Short(string s) => s.Length > 34 ? s.Substring(0, 34) : s;

        private int LuxuryWalkouts()
        {
            if (!HasSingleton<STycoonLuxuryDay>() || !HasSingleton<STycoonEconomy>()) return 0;
            return Mathf.Max(0, GetSingleton<STycoonEconomy>().WalkoutsTotal - GetSingleton<STycoonLuxuryDay>().WalkoutsAtStart);
        }

        private void DressEveryone(TycoonPromotions.Contract contract)
        {
            if (_undressed.IsEmpty) return;
            bool dressed = Outfit(contract.Outfit, out DataObjectList outfit);
            using NativeArray<Entity> groups = _undressed.ToEntityArray(Allocator.Temp);
            foreach (Entity g in groups)
            {
                EntityManager.AddComponent<CTycoonLuxuryDressed>(g);
                if (!dressed || !EntityManager.HasComponent<CGroupMember>(g)) continue;
                using NativeArray<CGroupMember> members = EntityManager.GetBuffer<CGroupMember>(g).ToNativeArray(Allocator.Temp);
                foreach (CGroupMember m in members)
                    if (EntityManager.Exists(m.Customer) && Has<CPlayerCosmetics>(m.Customer))
                        EntityManager.SetComponentData(m.Customer, new CPlayerCosmetics { Cosmetics = outfit });
            }
        }

        internal static void Book(EntityManager em, int slot, Vector3 at, float now)
        {
            TycoonPromotions.Contract contract = TycoonPromotions.ContractAt(slot);
            if (contract == null) return;
            if (TycoonEconomy.CurrentStars < TycoonPromotions.CONTRACTS_STAR) { TycoonPromotionDesk.Say(em, now, at, "Contracts open at 4 stars"); return; }
            if (TycoonEconomy.CurrentStars < contract.MinStars) { TycoonPromotionDesk.Say(em, now, at, $"That contract needs {contract.MinStars} stars"); return; }
            using EntityQuery q = em.CreateEntityQuery(typeof(STycoonContract));
            using EntityQuery moneyQuery = em.CreateEntityQuery(typeof(SMoney));
            if (moneyQuery.IsEmpty) return;
            SMoney money = moneyQuery.GetSingleton<SMoney>();
            int refund = 0;
            if (!q.IsEmpty)
            {
                STycoonContract current = q.GetSingleton<STycoonContract>();
                if (current.Active && !current.Resolved) { TycoonPromotionDesk.Say(em, now, at, "Today's contract isn't finished yet"); return; }
                if (current.Pending) refund = DepositFor(TycoonPromotions.ContractAt(current.Slot));
            }
            int deposit = DepositFor(contract);
            if (money.Amount + refund < deposit) { TycoonPromotionDesk.Say(em, now, at, $"The deposit is {deposit} coins"); return; }
            if (!q.IsEmpty) em.DestroyEntity(q.GetSingletonEntity());
            money.Amount += refund - deposit;
            moneyQuery.SetSingleton(money);
            Entity e = em.CreateEntity(typeof(STycoonContract));
            em.SetComponentData(e, new STycoonContract { Slot = slot, Pending = true });
            TycoonPromotionDesk.Say(em, now, at, $"Booked for the next day: {contract.Name} ({deposit} deposit)");
            Mod.LogInfo($"Contract: '{contract.Name}' booked for the next day; deposit {deposit} (refunded {refund}). Cash now {money.Amount}.");
        }

        internal static void Cancel(EntityManager em, Vector3 at, float now)
        {
            using EntityQuery q = em.CreateEntityQuery(typeof(STycoonContract));
            if (q.IsEmpty) return;
            STycoonContract current = q.GetSingleton<STycoonContract>();
            if (!current.Pending) { TycoonPromotionDesk.Say(em, now, at, "Today's contract can't be cancelled"); return; }
            int refund = DepositFor(TycoonPromotions.ContractAt(current.Slot));
            using (EntityQuery moneyQuery = em.CreateEntityQuery(typeof(SMoney)))
                if (!moneyQuery.IsEmpty && refund > 0) { SMoney money = moneyQuery.GetSingleton<SMoney>(); money.Amount += refund; moneyQuery.SetSingleton(money); }
            em.DestroyEntity(q.GetSingletonEntity());
            TycoonPromotionDesk.Say(em, now, at, refund > 0 ? $"Booking cancelled, {refund} deposit back" : "Contract booking cancelled");
            Mod.LogInfo($"Contract: booking cancelled ({refund} deposit back).");
        }
    }
}
