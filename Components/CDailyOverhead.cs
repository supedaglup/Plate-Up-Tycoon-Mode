using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>Unused; kept so older saves still load.</summary>
    public struct CDailyOverhead : IComponentData, IModComponent
    {
        public int Rent;

        public int Total => Rent;
    }
}
