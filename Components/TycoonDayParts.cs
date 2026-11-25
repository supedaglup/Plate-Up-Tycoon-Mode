using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>The hidden entity that tells every player's screen about the Tycoon day.</summary>
    public struct CTycoonPhaseCarrier : IComponentData, IModComponent { }

    /// <summary>The Return to HQ zone outside the restaurant, on a CGroupSelector entity.</summary>
    public struct CTycoonReturnSelector : IComponentData, IModComponent { }
}
