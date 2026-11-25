using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>Singleton: the Tycoon economy state.</summary>
    public struct STycoonEconomy : IComponentData, IModComponent
    {
        public int Stars;
        public int NightsInDebt;
        public int WalkoutsToday;
        public int WalkoutsTotal;
    }
}
