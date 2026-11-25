using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>On a restaurant blueprint in the HQ. Source/Layout/Setting rebuild exactly the same blueprint.</summary>
    public struct CTycoonBlueprint : IComponentData, IModComponent
    {
        public int Source;
        public int Layout;
        public int Setting;
        public int Size;
        public int Price;
        public bool Paid;
    }

    /// <summary>On the restaurant selector's yes/no pop-up: which saved restaurant, and choose it or sell it.</summary>
    public struct CTycoonSelectorChoice : IComponentData, IModComponent, Kitchen.IManagedPopupData
    {
        public int Slot;
        public bool Sell;
        public int Value;
    }

    /// <summary>Tag on Tycoon's stand-in "has garage" upgrade.</summary>
    public struct CTycoonGarageStandIn : IComponentData, IModComponent { }

    /// <summary>Tag on a blueprint pedestal behind the bank counter.</summary>
    public struct CTycoonBlueprintShelf : IComponentData, IModComponent { }

    /// <summary>Singleton: how this restaurant began.</summary>
    public struct STycoonRestaurantOrigin : IComponentData, IModComponent
    {
        public int StartingDish;
        public bool FromBlueprint;
    }
}
