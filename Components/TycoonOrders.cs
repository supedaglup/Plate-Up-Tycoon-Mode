using KitchenData;
using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>A player asked to order an appliance.</summary>
    public struct CTycoonBlueprintOrderRequest : IComponentData, IModComponent
    {
        public int ApplianceID;
        public int Cost;
        public int PlayerID;
        public UnityEngine.Vector3 Position;
    }

    /// <summary>A purchase announcement everyone sees for a few seconds.</summary>
    public struct CTycoonAnnouncement : IComponentData, IModComponent
    {
        public float ExpiresAt;
    }

    /// <summary>Debug request: end the current service phase now.</summary>
    public struct CTycoonDebugEndDay : IComponentData, IModComponent
    {
    }

    /// <summary>Singleton: counts prep phases so orders arrive "the next day". Saved with the restaurant.</summary>
    public struct STycoonOrders : IComponentData, IModComponent
    {
        public int NightCounter;
    }

    /// <summary>One paid-for appliance waiting to arrive as a parcel.</summary>
    public struct CTycoonPendingDelivery : IComponentData, IModComponent
    {
        public int ApplianceID;
        public int OrderedOnNight;
    }

    /// <summary>Tag on the run's scene entity once the Tycoon starting items were added to the loadout.</summary>
    public struct CTycoonStartingItemsAdded : IComponentData, IModComponent
    {
    }

    /// <summary>Appliance property: marks the Tycoon Purchasing Desk and makes it a source of a player-specific menu.</summary>
    public struct CTycoonPurchasingDesk : IApplianceProperty, IAttachableProperty, IComponentData, IModComponent, IPlayerSpecificUISource
    {
        public UnityEngine.Vector3 DrawLocation;

        UnityEngine.Vector3 IPlayerSpecificUISource.DrawLocation => DrawLocation;
    }
}
