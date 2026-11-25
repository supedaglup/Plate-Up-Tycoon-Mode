using KitchenData;
using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>One card the restaurant owns.</summary>
    public struct CTycoonCard : IComponentData, IModComponent
    {
        public int UnlockID;
        public int Star;
        public bool Enabled;
        public bool Vanilla;
    }

    /// <summary>Tag on a CTycoonCard bought from Smokey or Chubs, the card manager gives bought cards their own rows.</summary>
    public struct CTycoonCardBought : IComponentData, IModComponent
    {
    }

    /// <summary>Singleton: one-time set-up flags for the card system.</summary>
    public struct STycoonCards : IComponentData, IModComponent
    {
        public bool StartingCardsGiven;
    }

    /// <summary>A player asked to flip a card on/off at the Management Desk.</summary>
    public struct CTycoonCardToggleRequest : IComponentData, IModComponent
    {
        public int UnlockID;
        public int PlayerID;
        public UnityEngine.Vector3 Position;
    }

    /// <summary>Tag on the customer-numbers modifier entity the Take-Out card owns.</summary>
    public struct CTycoonSpawnBoost : IComponentData, IModComponent
    {
    }

    /// <summary>Tag on the restaurant while the Take-Out card had put the "customers order immediately" rule on it.</summary>
    public struct STycoonTakeOutStatusApplied : IComponentData, IModComponent
    {
    }

    /// <summary>Tag on the restaurant while the Take-Out card is on.</summary>
    public struct STycoonTakeOutOn : IComponentData, IModComponent
    {
    }

    /// <summary>Singleton while the Take-Out card has set customer groups to one person: the group sizes it replaced.</summary>
    public struct STycoonTakeOutGroupSize : IComponentData, IModComponent
    {
        public int OriginalMinimum;
        public int OriginalMaximum;
    }

    /// <summary>A queued customer group's own patience.</summary>
    public struct CTycoonQueuePatience : IComponentData, IModComponent
    {
        public float Remaining;
        public float Total;
    }

    /// <summary>A patience bar floating over a queued customer group.</summary>
    public struct CTycoonPatienceBar : IComponentData, IModComponent
    {
        public Entity Group;
    }

    /// <summary>Appliance property: the Ordering Counter.</summary>
    public struct CTycoonOrderingCounter : IApplianceProperty, IAttachableProperty, IComponentData, IModComponent
    {
    }

    /// <summary>Appliance property: the Management Desk.</summary>
    public struct CTycoonManagementDesk : IApplianceProperty, IAttachableProperty, IComponentData, IModComponent, IPlayerSpecificUISource
    {
        public UnityEngine.Vector3 DrawLocation;

        UnityEngine.Vector3 IPlayerSpecificUISource.DrawLocation => DrawLocation;
    }
}
