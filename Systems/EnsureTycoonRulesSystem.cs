using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Restaurant rules that are switched on by a singleton.</summary>
    public class EnsureTycoonRulesSystem : GenericSystemBase, IModSystem
    {
        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<SKitchenStatus>();
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<SCheatNoLosing>())
            {
                Set(new SCheatNoLosing());
                Mod.LogInfo("Tycoon: losing from customer patience switched off (bankruptcy is the fail state).");
            }

            if (!HasSingleton<STycoonEconomy>())
                Set(new STycoonEconomy { Stars = TycoonEconomy.MIN_STARS });

            TycoonEconomy.CurrentStars = TycoonEconomy.ClampStars(GetSingleton<STycoonEconomy>().Stars);
        }
    }
}
