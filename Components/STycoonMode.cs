using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>Singleton tag: present only while a Tycoon Mode game is running.</summary>
    public struct STycoonMode : IComponentData, IModComponent
    {
    }

    /// <summary>Singleton tag: the Tycoon starting cash has already been handed out for this run.</summary>
    public struct STycoonStartingMoneyGiven : IComponentData, IModComponent
    {
    }
}

namespace PlateUpTycoon.Components
{
    /// <summary>Tag on the HQ layout map item placed by TycoonFranchiseSetupSystem.</summary>
    public struct CTycoonLayoutMap : Unity.Entities.IComponentData, KitchenMods.IModComponent
    {
    }
}
