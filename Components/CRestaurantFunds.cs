using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>Unused; kept so older saves still load.</summary>
    public struct CRestaurantFunds : IComponentData, IModComponent
    {
        public int Money;
    }
}
