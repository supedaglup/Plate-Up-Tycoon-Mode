using KitchenData;
using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>Appliance property: Smokey's counter in the HQ recipe shop.</summary>
    public struct CTycoonChefDesk : IApplianceProperty, IAttachableProperty, IComponentData, IModComponent, IPlayerSpecificUISource
    {
        public UnityEngine.Vector3 DrawLocation;

        UnityEngine.Vector3 IPlayerSpecificUISource.DrawLocation => DrawLocation;
    }

    /// <summary>Tag on Smokey, the chef cat behind the recipe shop's counter.</summary>
    public struct CTycoonChef : IComponentData, IModComponent { }

    /// <summary>Tag on a recipe shop pedestal.</summary>
    public struct CTycoonRecipeShelf : IComponentData, IModComponent { }

    /// <summary>Tag on a recipe shop pedestal in the MAIN DISHES section.</summary>
    public struct CTycoonRecipeShelfMain : IComponentData, IModComponent { }

    /// <summary>Tag on a recipe shop pedestal in the DEPENDENT ADD-ONS section.</summary>
    public struct CTycoonRecipeShelfLinked : IComponentData, IModComponent { }

    /// <summary>On a recipe paper in the HQ recipe shop.</summary>
    public struct CTycoonRecipePaper : IComponentData, IModComponent
    {
        public int Dish;
        public bool Paid;
    }
}
