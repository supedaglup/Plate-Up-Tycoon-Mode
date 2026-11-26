using HarmonyLib;
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
    /// <summary>Every card can be switched on and off at the Management Desk.</summary>
    public class TycoonCardEffectsSystem : RestaurantSystem, IModSystem
    {
        internal const int SLOTS = 4;
        private static readonly Vector3 CARRIER_POSITION = new Vector3(0f, -50f, 0f);

        internal static bool Blocking { get; private set; }

        internal const int NEEDED_RECIPE = -1;

        internal static HashSet<int> WaitingForRecipe { get; private set; } = new HashSet<int>();

        internal static readonly HashSet<int> Ordered = new HashSet<int>();

        internal static string NeededName(int id) =>
            id == NEEDED_RECIPE ? "a recipe (switch one on at the Management Desk)"
            : GameData.Main.TryGet<Appliance>(id, out Appliance a) && !string.IsNullOrEmpty(a.Name) ? a.Name : id.ToString();

        private EntityQuery _cards;
        private EntityQuery _effects;
        private EntityQuery _pendingDishes;
        private EntityQuery _menuItems;
        private EntityQuery _providers;
        private EntityQuery _variableProviders;
        private EntityQuery _ingredientParcels;
        private EntityQuery _appliances;
        private EntityQuery _applianceParcels;
        private EntityQuery _orders;
        private EntityQuery _tables;
        private EntityQuery _carriers;
        private string _lastReport = "";
        private static int _diningTable;

        protected override void Initialise()
        {
            base.Initialise();
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard)));
            _effects = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCardEffect)));
            _pendingDishes = GetEntityQuery(new QueryHelper().All(typeof(CNewDish)));
            _menuItems = GetEntityQuery(new QueryHelper().All(typeof(CMenuItem)));
            _providers = GetEntityQuery(new QueryHelper().All(typeof(CItemProvider)).None(typeof(CDynamicItemProvider)));
            _variableProviders = GetEntityQuery(new QueryHelper().All(typeof(CVariableProvider)));
            _ingredientParcels = GetEntityQuery(new QueryHelper().All(typeof(CLetterIngredient)));
            _appliances = GetEntityQuery(new QueryHelper().All(typeof(CAppliance)));
            _applianceParcels = GetEntityQuery(new QueryHelper().All(typeof(CLetterAppliance)));
            _orders = GetEntityQuery(new QueryHelper().All(typeof(CTycoonPendingDelivery)));
            _tables = GetEntityQuery(new QueryHelper().All(typeof(CApplianceTable)).None(typeof(CTycoonOrderingCounter)));
            _carriers = GetEntityQuery(new QueryHelper().All(typeof(CTycoonNeededItemCarrier)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            bool night = HasSingleton<SIsNightTime>();
            var needed = new List<int>();
            HashSet<int> items = null, processes = null, owned = null;
            bool dishApplied = false;
            var cardDishes = new HashSet<int>();
            var appliedIDs = new HashSet<int>();
            var waiting = new HashSet<int>();

            using (NativeArray<Entity> entities = _cards.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    CTycoonCard c = EntityManager.GetComponentData<CTycoonCard>(entities[i]);
                    if (TycoonCards.IsRecipe(c.UnlockID)) cardDishes.Add(c.UnlockID);
                    if (c.Vanilla || Has<CTycoonCardApplied>(entities[i])) appliedIDs.Add(c.UnlockID);
                }

                for (int i = 0; i < entities.Length; i++)
                {
                    CTycoonCard card = EntityManager.GetComponentData<CTycoonCard>(entities[i]);
                    if (card.Vanilla || (Mod.TakeOutCard != null && card.UnlockID == Mod.TakeOutCard.ID)) continue;
                    if (Mod.FastCasualCard != null && card.UnlockID == Mod.FastCasualCard.ID) continue;
                    if (Mod.IsRewardCard(card.UnlockID)) continue;
                    if (Mod.IngredientQualityCard != null && card.UnlockID == Mod.IngredientQualityCard.ID) continue;
                    if (!GameData.Main.TryGet<Unlock>(card.UnlockID, out Unlock unlock)) continue;
                    bool applied = Has<CTycoonCardApplied>(entities[i]);

                    bool want = card.Enabled;
                    if (want && unlock is Dish needsParent && needsParent.Requires != null)
                        foreach (Unlock r in needsParent.Requires)
                            if (r is Dish && r.ID != card.UnlockID && !appliedIDs.Contains(r.ID)) { want = false; waiting.Add(card.UnlockID); break; }
                    if (want && !applied && unlock is Dish dish)
                    {
                        if (items == null) Gather(out items, out processes, out owned);
                        List<int> missing = Missing(dish, items, processes, owned);
                        foreach (int id in missing) if (!needed.Contains(id)) needed.Add(id);
                        want = missing.Count == 0;
                    }
                    if (!night) continue;

                    if (want && !applied)
                    {
                        if (unlock is Dish && (dishApplied || !_pendingDishes.IsEmpty)) continue;
                        Apply(entities[i], card, unlock);
                        if (unlock is Dish) dishApplied = true;
                    }
                    else if (!want && applied) Remove(entities[i], card, unlock);
                }
            }
            WaitingForRecipe = waiting;

            bool cardsReady = HasSingleton<STycoonCards>() && GetSingleton<STycoonCards>().StartingCardsGiven;
            if (cardsReady && night && needed.Count == 0 && _menuItems.IsEmpty && _pendingDishes.IsEmpty && !dishApplied)
                needed.Add(NEEDED_RECIPE);

            bool takeOut = Mod.TakeOutCard != null && TycoonCards.IsEnabled(EntityManager, Mod.TakeOutCard.ID);
            bool fastCasual = takeOut && Mod.FastCasualCard != null && TycoonCards.IsEnabled(EntityManager, Mod.FastCasualCard.ID);
            if (cardsReady && (!takeOut || fastCasual) && _tables.IsEmpty)
            {
                if (owned == null) Gather(out items, out processes, out owned);
                int table = DiningTable();
                bool tableOrdered = false;
                foreach (int id in owned) if (IsDiningTable(id)) { tableOrdered = true; break; }
                if (!tableOrdered && table != 0 && !needed.Contains(table)) needed.Add(table);
            }

            bool wasBlocking = Blocking;
            Blocking = needed.Count > 0;
            if (Blocking != wasBlocking) EntityManager.CreateEntity(typeof(CRequireStartDayWarningRecalculation));
            var names = new List<string>();
            foreach (int id in needed) names.Add(NeededName(id) + (Ordered.Contains(id) ? " (on its way)" : ""));
            string report = Blocking ? "Cards: the day can't start until these have arrived: " + string.Join(", ", names) + "." : "";
            if (report != _lastReport)
            {
                _lastReport = report;
                Mod.LogInfo(Blocking ? report : "Cards: nothing needed any more.");
            }
            UpdateCarriers(needed);
        }

        private void Apply(Entity cardEntity, CTycoonCard card, Unlock unlock)
        {
            int id = unlock.ID;
            if (unlock is Dish dish)
            {
                Entity newDish = EntityManager.CreateEntity(typeof(CNewDish), typeof(CTycoonCardEffect));
                EntityManager.SetComponentData(newDish, new CNewDish { ID = dish.ID, ShowRecipe = false });
                TycoonRecipePopupSystem.Pending.Add(dish.ID);
                EntityManager.SetComponentData(newDish, new CTycoonCardEffect { UnlockID = id });
                if (dish.AddsStatuses != null) foreach (RestaurantStatus status in dish.AddsStatuses) AddStatus(status);
            }
            else if (unlock is UnlockCard unlockCard && unlockCard.Effects != null)
            {
                for (int i = 0; i < unlockCard.Effects.Count; i++)
                {
                    UnlockEffect effect = unlockCard.Effects[i];
                    Mod.LogInfo($"Cards: '{unlock.Name}' effect {i + 1}: {Describe(effect)}.");
                    if (effect is ParameterEffect parameters) ChangeParameters(parameters.Parameters, 1);
                    else if (effect is StatusEffect status) AddStatus(status.Status);
                    else if (effect is GlobalEffect)
                    {
                        Entity e = Tagged(id);
                        EntityManager.AddComponentData(e, new CNewEffectUnlock { ID = id, Index = i });
                    }
                    else if (effect is CustomerSpawnEffect spawn)
                    {
                        Entity e = Tagged(id);
                        EntityManager.AddComponentData(e, new CCustomerSpawnModifier { BaseCustomerMultiplier = spawn.Base, PerDayCustomerMultiplier = spawn.PerDay });
                    }
                    else if (effect is EnableGroupEffect group && group.EnableType != null)
                    {
                        Entity e = Tagged(id);
                        EntityManager.AddComponentData(e, new CCustomerSpawnDefinition { Probability = group.Probability });
                        EntityManager.AddComponentData(e, new CCustomerType { Type = group.EnableType.ID });
                    }
                    else if (effect is ThemeAddEffect theme) ChangeTheme(theme.AddsTheme, true);
                    else Mod.LogInfo($"Cards: '{unlock.Name}' effect {effect?.GetType().Name} has no use in Tycoon.");
                }
            }
            if (unlock is ThemeUnlock themeCard) ChangeTheme(themeCard.Type, true);
            if (unlock.ItemRewards != null)
                foreach (var reward in unlock.ItemRewards)
                {
                    if (reward.Item == null) continue;
                    Entity e = Tagged(id);
                    EntityManager.AddComponentData(e, new CUnlockRewardBoost { ItemID = reward.Item.ID, Amount = reward.RewardAmount });
                }
            Entity record = Tagged(id);
            EntityManager.AddComponentData(record, new CProgressionUnlock { ID = id });

            EntityManager.AddComponent<CTycoonCardApplied>(cardEntity);
            Mod.LogInfo($"Cards: '{unlock.Name}' switched ON (applied).");
        }

        private void Remove(Entity cardEntity, CTycoonCard card, Unlock unlock)
        {
            int id = unlock.ID, removed = 0;
            using (NativeArray<Entity> effects = _effects.ToEntityArray(Allocator.Temp))
                for (int i = 0; i < effects.Length; i++)
                {
                    if (EntityManager.GetComponentData<CTycoonCardEffect>(effects[i]).UnlockID != id) continue;
                    EntityManager.DestroyEntity(effects[i]);
                    removed++;
                }
            if (unlock is Dish dish && dish.AddsStatuses != null)
                foreach (RestaurantStatus status in dish.AddsStatuses) RemoveStatus(status);
            if (unlock is ThemeUnlock themeCard) ChangeTheme(themeCard.Type, false);
            if (unlock is UnlockCard unlockCard && unlockCard.Effects != null)
                foreach (UnlockEffect effect in unlockCard.Effects)
                {
                    if (effect is ParameterEffect parameters) ChangeParameters(parameters.Parameters, -1);
                    else if (effect is ThemeAddEffect theme) ChangeTheme(theme.AddsTheme, false);
                    else if (effect is StatusEffect status) RemoveStatus(status.Status);
                }

            EntityManager.RemoveComponent<CTycoonCardApplied>(cardEntity);
            Mod.LogInfo($"Cards: '{unlock.Name}' switched OFF ({removed} game entities removed).");
        }

        private static string Describe(UnlockEffect effect)
        {
            switch (effect)
            {
                case ParameterEffect p:
                    return $"parameters (customers/hour {p.Parameters.CustomersPerHour:+0.##;-0.##;0}, reduction {p.Parameters.CustomersPerHourReduction:+0.##;-0.##;0}, "
                        + $"group size min {p.Parameters.MinimumGroupSize:+0;-0;0} max {p.Parameters.MaximumGroupSize:+0;-0;0}, courses {p.Parameters.CurrentCourses})";
                case StatusEffect s: return $"status {s.Status}";
                case CustomerSpawnEffect c: return $"customer numbers (base {c.Base.Value:+0.##;-0.##;0}, per day {c.PerDay.Value:+0.##;-0.##;0})";
                case EnableGroupEffect g: return $"customer type {(g.EnableType != null ? g.EnableType.name : "none")} ({g.Probability:0.##})";
                case GlobalEffect _: return "global effect " + effect.GetType().Name;
                default: return effect == null ? "none" : effect.GetType().Name;
            }
        }

        private Entity Tagged(int unlockID)
        {
            Entity e = EntityManager.CreateEntity(typeof(CTycoonCardEffect));
            EntityManager.SetComponentData(e, new CTycoonCardEffect { UnlockID = unlockID });
            return e;
        }

        private void ChangeTheme(DecorationType theme, bool on)
        {
            if (on) { SetTheme(theme); Mod.LogInfo($"Cards: restaurant theme {theme} on."); return; }
            if (!HasSingleton<SGlobalStatusList>()) return;
            SGlobalStatusList status = GetSingleton<SGlobalStatusList>();
            status.Theme &= ~theme;
            Set(status);
            Mod.LogInfo($"Cards: restaurant theme {theme} off.");
        }

        private void ChangeParameters(KitchenParameters p, int sign)
        {
            if (!HasSingleton<SKitchenParameters>()) return;
            SKitchenParameters current = GetSingleton<SKitchenParameters>();
            current.Parameters.CustomersPerHour += sign * p.CustomersPerHour;
            current.Parameters.CustomersPerHourReduction += sign * p.CustomersPerHourReduction;
            current.Parameters.MinimumGroupSize = Mathf.Max(1, current.Parameters.MinimumGroupSize + sign * p.MinimumGroupSize);
            current.Parameters.MaximumGroupSize = Mathf.Max(current.Parameters.MinimumGroupSize, current.Parameters.MaximumGroupSize + sign * p.MaximumGroupSize);
            if (sign > 0) current.Parameters.CurrentCourses = Mathf.Max(current.Parameters.CurrentCourses, p.CurrentCourses);
            Set(current);

            if (HasSingleton<STycoonTakeOutGroupSize>())
            {
                STycoonTakeOutGroupSize saved = GetSingleton<STycoonTakeOutGroupSize>();
                saved.OriginalMinimum = Mathf.Max(1, saved.OriginalMinimum + sign * p.MinimumGroupSize);
                saved.OriginalMaximum = Mathf.Max(saved.OriginalMinimum, saved.OriginalMaximum + sign * p.MaximumGroupSize);
                Set(saved);
            }
        }

        private void Gather(out HashSet<int> items, out HashSet<int> processes, out HashSet<int> owned)
        {
            items = new HashSet<int>();
            processes = new HashSet<int>();
            owned = new HashSet<int>();
            using (NativeArray<CItemProvider> providers = _providers.ToComponentDataArray<CItemProvider>(Allocator.Temp))
                for (int i = 0; i < providers.Length; i++) items.Add(providers[i].ProvidedItem);
            using (NativeArray<CVariableProvider> variable = _variableProviders.ToComponentDataArray<CVariableProvider>(Allocator.Temp))
                for (int i = 0; i < variable.Length; i++) { items.Add(variable[i].Provide1); items.Add(variable[i].Provide2); items.Add(variable[i].Provide3); }
            using (NativeArray<CLetterIngredient> letters = _ingredientParcels.ToComponentDataArray<CLetterIngredient>(Allocator.Temp))
                for (int i = 0; i < letters.Length; i++) items.Add(letters[i].IngredientID);

            using (NativeArray<CAppliance> appliances = _appliances.ToComponentDataArray<CAppliance>(Allocator.Temp))
                for (int i = 0; i < appliances.Length; i++) owned.Add(appliances[i].ID);
            using (NativeArray<CLetterAppliance> letters = _applianceParcels.ToComponentDataArray<CLetterAppliance>(Allocator.Temp))
                for (int i = 0; i < letters.Length; i++) owned.Add(letters[i].ApplianceID);
            Ordered.Clear();
            using (NativeArray<CTycoonPendingDelivery> orders = _orders.ToComponentDataArray<CTycoonPendingDelivery>(Allocator.Temp))
                for (int i = 0; i < orders.Length; i++) Ordered.Add(orders[i].ApplianceID);

            foreach (int id in owned)
            {
                if (!GameData.Main.TryGet<Appliance>(id, out Appliance appliance) || appliance.Processes == null) continue;
                foreach (Appliance.ApplianceProcesses p in appliance.Processes)
                    if (p.Validity != ProcessValidity.DoesNotRegister && p.Process != null) processes.Add(p.Process.ID);
            }
        }

        internal static List<int> NeededFor(Dish dish, IEnumerable<int> appliances)
        {
            var owned = new HashSet<int>(appliances);
            var processes = new HashSet<int>();
            foreach (int id in owned)
            {
                if (!GameData.Main.TryGet<Appliance>(id, out Appliance appliance) || appliance.Processes == null) continue;
                foreach (Appliance.ApplianceProcesses p in appliance.Processes)
                    if (p.Validity != ProcessValidity.DoesNotRegister && p.Process != null) processes.Add(p.Process.ID);
            }
            return Missing(dish, new HashSet<int>(), processes, owned);
        }

        private static List<int> Missing(Dish dish, HashSet<int> items, HashSet<int> processes, HashSet<int> owned)
        {
            var missing = new List<int>();
            int defaultProvider = GameData.Main.ReferableObjects.DefaultProvider != null ? GameData.Main.ReferableObjects.DefaultProvider.ID : 0;
            if (dish.MinimumIngredients != null)
                foreach (Item item in dish.MinimumIngredients)
                {
                    if (item == null || items.Contains(item.ID)) continue;
                    GameDataObject provider = item.DedicatedProvider;
                    if (provider == null || provider.ID == defaultProvider || owned.Contains(provider.ID) || missing.Contains(provider.ID)) continue;
                    missing.Add(provider.ID);
                }
            if (dish.RequiredProcesses != null)
                foreach (Process process in dish.RequiredProcesses)
                {
                    if (process == null || processes.Contains(process.ID)) continue;
                    GameDataObject appliance = process.BasicEnablingAppliance;
                    if (appliance == null || owned.Contains(appliance.ID) || missing.Contains(appliance.ID)) continue;
                    missing.Add(appliance.ID);
                }
            return missing;
        }

        internal static bool IsDiningTable(int applianceID)
        {
            if (Mod.OrderingCounter != null && applianceID == Mod.OrderingCounter.ID) return false;
            if (!GameData.Main.TryGet<Appliance>(applianceID, out Appliance appliance) || appliance.Properties == null) return false;
            foreach (IApplianceProperty p in appliance.Properties) if (p is CApplianceTable) return true;
            return false;
        }

        internal static int DiningTable()
        {
            if (_diningTable != 0) return _diningTable;
            foreach (TycoonCatalogue.Entry entry in TycoonCatalogue.Entries)
                if (IsDiningTable(entry.ApplianceID)) { _diningTable = entry.ApplianceID; break; }
            return _diningTable;
        }

        private void UpdateCarriers(List<int> needed)
        {
            if (_carriers.CalculateEntityCount() != SLOTS)
            {
                EntityManager.DestroyEntity(_carriers);
                for (int slot = 0; slot < SLOTS; slot++)
                {
                    Entity e = EntityManager.CreateEntity(typeof(CTycoonNeededItemCarrier), typeof(CRequiresView), typeof(CPosition), typeof(CProgressIndicator), typeof(CDoNotPersist));
                    EntityManager.SetComponentData(e, new CTycoonNeededItemCarrier { Slot = slot });
                    EntityManager.SetComponentData(e, new CRequiresView { Type = ViewType.ProgressView });
                    EntityManager.SetComponentData(e, new CPosition(CARRIER_POSITION));
                }
            }
            using NativeArray<Entity> carriers = _carriers.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < carriers.Length; i++)
            {
                int slot = EntityManager.GetComponentData<CTycoonNeededItemCarrier>(carriers[i]).Slot;
                int applianceID = slot < needed.Count ? needed[slot] : 0;
                int process = Patches.TycoonReputationBar.EncodeNeeded(slot, applianceID, slot == SLOTS - 1 && needed.Count > SLOTS);
                float onItsWay = applianceID != 0 && Ordered.Contains(applianceID) ? 1f : 0f;
                CProgressIndicator current = EntityManager.GetComponentData<CProgressIndicator>(carriers[i]);
                if (current.Process != process || current.Progress != onItsWay) EntityManager.SetComponentData(carriers[i], new CProgressIndicator { Process = process, Progress = onItsWay });
            }
        }
    }

    /// <summary>The day can't start while something switched on still needs appliances.</summary>
    [HarmonyPatch(typeof(StartNewDay), "OnUpdate")]
    public static class TycoonBlockStartDayPatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => !(TycoonSession.IsActive && TycoonCardEffectsSystem.Blocking);
    }

    /// <summary>While something is needed, the game's own start-of-day warning shows.</summary>
    [HarmonyPatch(typeof(ManageStartDayWarnings), "OnUpdate")]
    public static class TycoonNeededItemsWarningPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ManageStartDayWarnings __instance)
        {
            if (!TycoonSession.IsActive || !TycoonCardEffectsSystem.Blocking) return;
            EntityManager em = __instance.EntityManager;
            using EntityQuery query = em.CreateEntityQuery(typeof(SStartDayWarnings));
            if (query.IsEmpty) return;
            Entity e = query.GetSingletonEntity();
            SStartDayWarnings warnings = em.GetComponentData<SStartDayWarnings>(e);
            warnings.SellingRequiredAppliance = (WarningLevel)3;
            em.SetComponentData(e, warnings);
        }
    }

    /// <summary>HandleNewDish in Tycoon: no free parcels and everything it makes for a card's recipe is tagged with that card.</summary>
    [HarmonyPatch(typeof(HandleNewDish), "OnUpdate")]
    public static class TycoonNoFreeRecipeItemsPatch
    {
        internal static bool Running;
        private static readonly HashSet<Entity> Before = new HashSet<Entity>();
        private static int _cardID;

        private static EntityQuery Made(EntityManager em) => em.CreateEntityQuery(new EntityQueryDesc
        {
            Any = new ComponentType[] { typeof(CMenuItem), typeof(CAvailableIngredient), typeof(CPossibleExtra), typeof(CBlockedIngredient) },
        });

        [HarmonyPrefix]
        private static void Prefix(HandleNewDish __instance)
        {
            Running = TycoonSession.IsActive;
            _cardID = 0;
            Before.Clear();
            if (!Running) return;
            EntityManager em = __instance.EntityManager;
            using (EntityQuery dishes = em.CreateEntityQuery(typeof(CNewDish)))
            {
                if (dishes.CalculateEntityCount() != 1) return;
                Entity dish = dishes.GetSingletonEntity();
                if (!em.HasComponent<CTycoonCardEffect>(dish)) return;
                _cardID = em.GetComponentData<CTycoonCardEffect>(dish).UnlockID;
            }
            using EntityQuery made = Made(em);
            using NativeArray<Entity> existing = made.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < existing.Length; i++) Before.Add(existing[i]);
        }

        [HarmonyPostfix]
        private static void Postfix(HandleNewDish __instance)
        {
            if (!Running) return;
            Running = false;
            EntityManager em = __instance.EntityManager;

            if (_cardID != 0)
            {
                int tagged = 0;
                using EntityQuery made = Made(em);
                using NativeArray<Entity> now = made.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < now.Length; i++)
                {
                    if (Before.Contains(now[i]) || em.HasComponent<CTycoonCardEffect>(now[i])) continue;
                    em.AddComponentData(now[i], new CTycoonCardEffect { UnlockID = _cardID });
                    tagged++;
                }
                Mod.LogInfo($"Cards: the recipe made {tagged} menu/ingredient entities (tagged to the card).");
                _cardID = 0;
                Before.Clear();
            }

            using EntityQuery requests = em.CreateEntityQuery(typeof(CNeedsNewIngredient));
            int count = requests.CalculateEntityCount();
            if (count == 0) return;
            em.DestroyEntity(requests);
            Mod.LogInfo($"Cards: {count} free ingredient parcel(s) cancelled (bought at the Purchasing Desk instead).");
        }

        [HarmonyFinalizer]
        private static void Finalizer() => Running = false;
    }

    [HarmonyPatch(typeof(PostHelpers), "CreateApplianceParcel")]
    public static class TycoonNoFreeRecipeParcelPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(int appliance_id, ref Entity __result)
        {
            if (!TycoonNoFreeRecipeItemsPatch.Running) return true;
            __result = Entity.Null;
            Mod.LogInfo($"Cards: free parcel for appliance {appliance_id} cancelled.");
            return false;
        }
    }
}
