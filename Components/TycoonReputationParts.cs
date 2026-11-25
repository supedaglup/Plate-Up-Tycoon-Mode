using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>Singleton: the restaurant's reputation and today's food critic.</summary>
    public struct STycoonReputation : IComponentData, IModComponent
    {
        public float Reputation;
        public float GainedToday;
        public int ServedToday;
        public int TipsToday;
        public int CriticToday;
        public int CriticResult;
        public float CriticGain;
        public int LastStarChange;
        public float SavedReputation1;
        public float SavedReputation2;
        public float SavedReputation3;
        public float SavedReputation4;
    }

    /// <summary>Singleton: the score the restaurant had at 0 stars when it was promoted to 1, restored if it drops back to 0.</summary>
    public struct STycoonStarZero : IComponentData, IModComponent
    {
        public float SavedReputation0;
    }

    /// <summary>Singleton: this restaurant's reputation is a running total.</summary>
    public struct STycoonRunningReputation : IComponentData, IModComponent
    {
        public int Version;
    }

    /// <summary>Tag from older saves: the appliance's theme points were emptied.</summary>
    public struct CTycoonNoThemePoints : IComponentData, IModComponent { }

    /// <summary>Tag on the customer group that is today's food critic.</summary>
    public struct CTycoonCritic : IComponentData, IModComponent { }

    /// <summary>Tag on the health inspector while they walk their tour.</summary>
    public struct CTycoonInspectorWalker : IComponentData, IModComponent { }

    /// <summary>Tag on the customer group the health inspector becomes after the tour.</summary>
    public struct CTycoonInspector : IComponentData, IModComponent { }

    /// <summary>Singleton: today's health inspection.</summary>
    public struct STycoonInspection : IComponentData, IModComponent
    {
        public int Stage;
        public float ArrivesAt;
        public int MessesSeen;
        public int FullBinsSeen;
        public bool Served;
        public int FineToday;
        public int InspectionsTotal;
    }

    /// <summary>Tag on an offered card not added yet.</summary>
    public struct CTycoonCardPending : IComponentData, IModComponent { }

    /// <summary>Tag on a CTycoonCard entity while the card's effects are in the restaurant.</summary>
    public struct CTycoonCardApplied : IComponentData, IModComponent { }

    /// <summary>On every entity a card's effects created, so switching the card off can remove exactly those.</summary>
    public struct CTycoonCardEffect : IComponentData, IModComponent
    {
        public int UnlockID;
    }

    /// <summary>Debug menu request: add this much reputation.</summary>
    public struct CTycoonDebugReputation : IComponentData, IModComponent
    {
        public float Amount;
    }

    /// <summary>Take-Out: the queue patience a group had left when it stepped out of the line.</summary>
    public struct CTycoonCarriedPatience : IComponentData, IModComponent
    {
        public float Remaining;
        public float Total;
        public int AppliedPhase;
        public float PhaseBudget;
        public float LastWritten;
    }

    /// <summary>One of the hidden entities that tell every player's screen what a selected recipe card still needs.</summary>
    public struct CTycoonNeededItemCarrier : IComponentData, IModComponent
    {
        public int Slot;
    }

    /// <summary>The hidden entity that tells every player's screen about reputation and the critic.</summary>
    public struct CTycoonReputationCarrier : IComponentData, IModComponent { }
}
