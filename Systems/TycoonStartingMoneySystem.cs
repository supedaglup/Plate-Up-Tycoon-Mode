using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Replaces the vanilla day-1 purse with the Tycoon starting cash, once per run.</summary>
    [UpdateAfter(typeof(ProvideStartingMoney))]
    public class TycoonStartingMoneySystem : RestaurantSystem, IModSystem
    {
        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<SMoney>();
        }

        protected override void OnUpdate()
        {
            if (HasSingleton<STycoonStartingMoneyGiven>())
                return;

            TycoonCompany.EnsureLoaded();
            SMoney money = GetSingleton<SMoney>();
            money.Amount = TycoonCompany.Cash;
            Set(money);
            Set(new STycoonStartingMoneyGiven());

            Mod.LogInfo($"Tycoon starting cash set to the company wallet: {money.Amount}.");
        }
    }
}
