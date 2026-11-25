using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Closing time: at the start of each night, works out the day's bill.</summary>
    [UpdateInGroup(typeof(EndOfDayProgressionGroup))]
    [UpdateBefore(typeof(CreateEndOfDayPopup))]
    public class TycoonClosingSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery _appliances;
        private EntityQuery _parcels, _orders, _cards;
        private EntityQuery _players;

        protected override void Initialise()
        {
            base.Initialise();
            _appliances = GetEntityQuery(new QueryHelper().All(typeof(CAppliance)));
            _parcels = GetEntityQuery(new QueryHelper().All(typeof(CLetterAppliance)));
            _players = GetEntityQuery(new QueryHelper().All(typeof(CPlayer)));
            _orders = GetEntityQuery(new QueryHelper().All(typeof(CTycoonPendingDelivery)));
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<SIsNightFirstUpdate>();
            RequireSingletonForUpdate<SMoney>();
        }

        private void NoteForWhiskers(int slot)
        {
            try
            {
                var owned = new System.Collections.Generic.List<int>();
                using (NativeArray<CAppliance> a = _appliances.ToComponentDataArray<CAppliance>(Allocator.Temp))
                    for (int i = 0; i < a.Length; i++) if (!owned.Contains(a[i].ID)) owned.Add(a[i].ID);
                using (NativeArray<CLetterAppliance> p = _parcels.ToComponentDataArray<CLetterAppliance>(Allocator.Temp))
                    for (int i = 0; i < p.Length; i++) if (!owned.Contains(p[i].ApplianceID)) owned.Add(p[i].ApplianceID);
                using (NativeArray<CTycoonPendingDelivery> o = _orders.ToComponentDataArray<CTycoonPendingDelivery>(Allocator.Temp))
                    for (int i = 0; i < o.Length; i++) if (!owned.Contains(o[i].ApplianceID)) owned.Add(o[i].ApplianceID);
                var needs = new System.Collections.Generic.List<int>();
                using (NativeArray<CTycoonCard> cards = _cards.ToComponentDataArray<CTycoonCard>(Allocator.Temp))
                    for (int i = 0; i < cards.Length; i++)
                    {
                        if (!KitchenData.GameData.Main.TryGet<KitchenData.Dish>(cards[i].UnlockID, out KitchenData.Dish dish)) continue;
                        foreach (int id in TycoonCardEffectsSystem.NeededFor(dish, owned)) if (!needs.Contains(id)) needs.Add(id);
                    }
                TycoonCompany.OwnedAppliances[slot] = owned;
                TycoonCompany.CardNeeds[slot] = needs;
                Mod.LogInfo($"Whiskers's notes for slot {slot}: {owned.Count} kind(s) of appliance owned; recipe cards still need: {(needs.Count == 0 ? "nothing" : string.Join(", ", needs.ConvertAll(TycoonDiscountShop.NameOf)))}.");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Whiskers's notes: " + e.Message);
            }
        }

        protected override void OnUpdate()
        {
            int day = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            if (day < 1 || HasSingleton<SIsRestartedDay>())
                return;

            STycoonEconomy economy = HasSingleton<STycoonEconomy>() ? GetSingleton<STycoonEconomy>() : default;

            if (economy.NightsInDebt >= TycoonEconomy.BANKRUPT_AFTER_NIGHTS_IN_DEBT - 1 && GetSingleton<SMoney>().Amount < 0)
            {
                economy.NightsInDebt++;
                Set(economy);
                BankruptcySystem.BankruptNow = true;
                Mod.LogWarning($"Closing day {day}: still {GetSingleton<SMoney>().Amount} coins after a night in debt, before the bill. Bankrupt.");
                return;
            }

            int stars =TycoonEconomy.ClampStars(economy.Stars);

            int appliances = 0, value = 0, ingredients = 0;
            using (NativeArray<CAppliance> owned = _appliances.ToComponentDataArray<CAppliance>(Allocator.Temp))
            {
                for (int i = 0; i < owned.Length; i++)
                {
                    if (!TycoonCatalogue.TryGetAny(owned[i].ID, out TycoonCatalogue.Entry entry)) continue;
                    appliances++;
                    value += entry.Cost;
                    if (entry.Category == TycoonCatalogue.Category.Ingredients) ingredients++;
                }
            }

            int tiles = TileManager.InteriorTileCount();
            SMoney money = GetSingleton<SMoney>();
            int before = money.Amount;
            int takings = TodaysTakings(before);
            int messes = TycoonDayPhases.MessesLeft, trash = TycoonDayPhases.TrashLeft;
            int players = UnityEngine.Mathf.Max(1, _players.CalculateEntityCount());
            int plates = TycoonDayPhases.DisposablePlates;
            TycoonEconomy.Bill bill = TycoonEconomy.CalculateBill(stars, tiles, appliances, value, ingredients, takings, messes, trash, players,
                TycoonReputation.Current.Reputation, plates);

            int loanPayment = 0;
            TycoonCompany.EnsureLoaded();
            if (TycoonCompany.Outstanding > 0 && TycoonCompany.GraceNights > 0)
            {
                TycoonCompany.GraceNights--;
                Mod.LogInfo($"Bank: no loan payment tonight ({TycoonCompany.GraceNights} more payment-free closing(s)).");
            }
            else if (TycoonCompany.Outstanding > 0)
            {
                loanPayment = UnityEngine.Mathf.Min(TycoonCompany.DailyPayment, TycoonCompany.Outstanding);
                TycoonCompany.Outstanding -= loanPayment;
                if (TycoonCompany.Outstanding == 0) { TycoonCompany.DailyPayment = 0; Mod.LogInfo("Bank: the loan is paid off."); }
            }

            int inspectionFine = HasSingleton<STycoonInspection>() ? GetSingleton<STycoonInspection>().FineToday : 0;

            TycoonDishPricingSystem.CloseDay(EntityManager);
            TycoonShopPrices.NoteProfit(takings - bill.Total);
            money.Amount -= bill.Total + loanPayment + inspectionFine;
            Set(money);
            TycoonCompany.Cash = money.Amount;
            if (TycoonCompany.ActiveSlot > 0) TycoonCompany.ApplianceValues[TycoonCompany.ActiveSlot] = value;
            if (TycoonCompany.ActiveSlot > 0) TycoonCompany.LastTakings[TycoonCompany.ActiveSlot] = takings;
            if (TycoonCompany.ActiveSlot > 0) NoteForWhiskers(TycoonCompany.ActiveSlot);
            if (HasSingleton<STycoonReputation>())
                TycoonCompany.TotalReputation += UnityEngine.Mathf.RoundToInt(GetSingleton<STycoonReputation>().GainedToday);
            TycoonCompany.Save();

            economy.Stars = stars;
            economy.NightsInDebt = money.Amount < 0 ? 1 : 0;
            int walkouts = economy.WalkoutsToday;
            economy.WalkoutsToday = 0;
            Set(economy);

            var ctx = new EntityContext(EntityManager);
            MoneyTracker.AddEvent(ctx, Mod.Ledger.CostsHeading, 0);
            AddCost(ctx, Mod.Ledger.Rent, bill.Rent);
            AddCost(ctx, Mod.Ledger.Utilities, bill.Utilities);
            AddCost(ctx, Mod.Ledger.Upkeep, bill.Upkeep);
            AddCost(ctx, Mod.Ledger.Restocking, bill.Restocking);
            AddCost(ctx, Mod.Ledger.Licences, bill.Licences);
            AddCost(ctx, Mod.Ledger.DirtyFloors, bill.DirtyFloors);
            AddCost(ctx, Mod.Ledger.TrashLeft, bill.TrashLeft);
            AddCost(ctx, Mod.Ledger.Wages, bill.Wages);
            AddCost(ctx, Mod.Ledger.Loan, loanPayment);
            AddCost(ctx, Mod.Ledger.InspectionFine, inspectionFine);
            MoneyTracker.AddEvent(ctx, Mod.Ledger.Debt, economy.NightsInDebt);

            STycoonReputation rep = HasSingleton<STycoonReputation>() ? GetSingleton<STycoonReputation>() : default;
            MoneyTracker.AddEvent(ctx, Mod.Ledger.ReputationGained, UnityEngine.Mathf.RoundToInt(rep.GainedToday * 100f));
            MoneyTracker.AddEvent(ctx, Mod.Ledger.ReputationTotal, UnityEngine.Mathf.RoundToInt(rep.Reputation * 100f));
            MoneyTracker.AddEvent(ctx, Mod.Ledger.StarChange, rep.LastStarChange);

            Mod.LogInfo($"Closing day {day} ({stars} star): takings {takings} (tips {rep.TipsToday}); bill {bill.Total} = utilities {bill.Utilities} ({appliances} appliances) + upkeep {bill.Upkeep} (worth {value}) "
                + $"+ restocking {bill.Restocking} ({ingredients} ingredient sources, {plates} disposable plates) + licences {bill.Licences} + dirty floors {bill.DirtyFloors} ({messes} messes) + trash {bill.TrashLeft} ({trash} items) + staff wages {bill.Wages} ({players} player(s)). "
                + $"Loan payment {loanPayment} ({TycoonCompany.Outstanding} still owed). Cash {before} -> {money.Amount}. Nights in debt: {economy.NightsInDebt}. Walkouts today: {walkouts}.");
        }

        private static void AddCost(EntityContext ctx, int id, int cost)
        {
            if (cost > 0) MoneyTracker.AddEvent(ctx, id, -cost);
        }

        private int TodaysTakings(int cashNow)
        {
            int earned = HasSingleton<SMoneyEarningsTracker>() ? cashNow - GetSingleton<SMoneyEarningsTracker>().OldAmount : 0;
            int spent = 0;
            MoneyTracker tracker = World.GetExistingSystem<MoneyTracker>();
            if (tracker != null)
            {
                try
                {
                    CMoneyTrackRecord record = tracker.GetRecord();
                    for (int i = 0; i < record.Identifiers.Count && i < record.Amounts.Count; i++)
                    {
                        int id = record.Identifiers[i];
                        bool spending = id == Mod.Ledger.Purchases || !Mod.Ledger.IsTycoonId(id);
                        if (record.Amounts[i] < 0 && spending) spent -= record.Amounts[i];
                    }
                }
                catch (System.Exception) { }
            }
            return UnityEngine.Mathf.Max(0, earned + spent);
        }
    }
}
