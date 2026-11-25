using KitchenData;
using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>Appliance property: Whiskers's desk in the HQ garage.</summary>
    public struct CTycoonDiscountDesk : IApplianceProperty, IAttachableProperty, IComponentData, IModComponent, IPlayerSpecificUISource
    {
        public UnityEngine.Vector3 DrawLocation;

        UnityEngine.Vector3 IPlayerSpecificUISource.DrawLocation => DrawLocation;
    }

    /// <summary>Tag on Whiskers, the discount appliance seller cat.</summary>
    public struct CTycoonDiscountSeller : IComponentData, IModComponent { }

    /// <summary>Tag on one of Whiskers's stands.</summary>
    public struct CTycoonDiscountStand : IComponentData, IModComponent { }

    /// <summary>On an appliance crate from Whiskers's shop.</summary>
    public struct CTycoonApplianceCrate : IComponentData, IModComponent
    {
        public int Appliance;
        public int Price;
        public int Discount;
        public bool Paid;
    }
}
