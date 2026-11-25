using KitchenData;
using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>Appliance property: Chubs's desk in the HQ Card Market.</summary>
    public struct CTycoonCardDesk : IApplianceProperty, IAttachableProperty, IComponentData, IModComponent, IPlayerSpecificUISource
    {
        public UnityEngine.Vector3 DrawLocation;

        UnityEngine.Vector3 IPlayerSpecificUISource.DrawLocation => DrawLocation;
    }

    /// <summary>Tag on Chubs, the card dealer cat behind the Card Market's desk.</summary>
    public struct CTycoonCardDealer : IComponentData, IModComponent { }

    /// <summary>Tag on a Card Market pedestal.</summary>
    public struct CTycoonCardShelf : IComponentData, IModComponent { }

    /// <summary>On a card in the HQ Card Market.</summary>
    public struct CTycoonCardItem : IComponentData, IModComponent
    {
        public int Card;
        public bool Paid;
    }
}
