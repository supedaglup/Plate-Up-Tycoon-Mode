using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;

namespace PlateUpTycoon.Systems
{
    /// <summary>Debug: adds or removes coins.</summary>
    public class TycoonDebugMoneySystem : GenericSystemBase, IModSystem
    {
        internal static int Pending;

        protected override void OnUpdate()
        {
            if (Pending == 0 || !TycoonSession.IsActive) return;
            int amount = Pending;
            Pending = 0;
            TycoonCompany.EnsureLoaded();
            if (HasSingleton<SMoney>() && HasSingleton<STycoonCompanyLinked>())
            {
                SMoney money = GetSingleton<SMoney>();
                money.Amount += amount;
                Set(money);
                TycoonCompany.Cash = money.Amount;
            }
            else TycoonCompany.Cash += amount;
            TycoonCompany.Save();
            Mod.LogWarning($"DEBUG: {amount:+0;-0} coins; the company now has {TycoonCompany.Cash}.");
        }
    }
}
