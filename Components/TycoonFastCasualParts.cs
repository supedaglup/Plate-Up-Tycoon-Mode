using KitchenMods;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>On every customer group made while Take-Out is on: groups are split into one-person groups as they arrive.</summary>
    public struct CTycoonParty : IComponentData, IModComponent
    {
        public int PartyID;
        public int Size;
        public bool FastCasual;
        public int Paid;
    }

    /// <summary>A Fast Casual customer after paying at the counter.</summary>
    public struct CTycoonFastCasual : IComponentData, IModComponent
    {
        public int State;
        public Entity Table;
        public int Seat;
        public UnityEngine.Vector3 WaitSpot;
        public float StateStarted;
        public int Dirt1, Dirt2, Dirt3;
        public bool CoffeeOnly;
        public bool Tipped;
    }

    /// <summary>A Fast Casual customer's meal at the table: seconds left of their eating time.</summary>
    public struct CTycoonFastCasualMeal : IComponentData, IModComponent
    {
        public float Remaining;
        public float Total;
    }

    /// <summary>A patience bar over a Fast Casual customer waiting for a table.</summary>
    public struct CTycoonFastCasualBar : IComponentData, IModComponent
    {
        public Entity Group;
    }

    /// <summary>Part of a Fast Casual customer's food shown on their table while they eat.</summary>
    public struct CTycoonFastCasualFood : IComponentData, IModComponent
    {
        public Entity Group;
    }

    /// <summary>Singleton while Fast Casual Dining is on and Take-Out is on.</summary>
    public struct STycoonFastCasualOn : IComponentData, IModComponent
    {
    }
}
