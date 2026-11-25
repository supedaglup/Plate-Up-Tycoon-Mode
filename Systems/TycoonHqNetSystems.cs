using HarmonyLib;
using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>HQ: the coin counter on every player's screen.</summary>
    public class TycoonHqMoneyViewSystem : ViewSystemBase, IModSystem
    {
        private static readonly System.Type ViewDataType = AccessTools.Inner(typeof(MoneyDisplayView), "ViewData");
        private static readonly System.Reflection.MethodInfo Send =
            AccessTools.Method(typeof(ViewSystemBase), "SendUpdate")?.MakeGenericMethod(ViewDataType);

        private EntityQuery _displays;
        private int _sent = int.MinValue;
        private float _nextSend;
        private bool _failed;

        protected override void Initialise()
        {
            base.Initialise();
            _displays = GetEntityQuery(new QueryHelper().All(typeof(CTycoonHqMoney), typeof(CLinkedView)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || _displays.IsEmpty || Send == null || _failed) return;
            TycoonCompany.EnsureLoaded();
            if (TycoonCompany.Cash == _sent && Time.RealTotalTime < _nextSend) return;
            _nextSend = Time.RealTotalTime + 2f;
            try
            {
                using NativeArray<CLinkedView> views = _displays.ToComponentDataArray<CLinkedView>(Allocator.Temp);
                foreach (CLinkedView view in views)
                {
                    object data = System.Activator.CreateInstance(ViewDataType);
                    AccessTools.Field(ViewDataType, "Money").SetValue(data, TycoonCompany.Cash);
                    Send.Invoke(this, new object[] { (ViewIdentifier)view, data, MessageType.ViewUpdate, 0 });
                }
                _sent = TycoonCompany.Cash;
            }
            catch (System.Exception e)
            {
                _failed = true;
                Mod.LogWarning("HQ coin counter: could not send it to the other players: " + e.Message);
            }
        }
    }

    /// <summary>HQ: the company's Total Reputation.</summary>
    public class TycoonCompanyTrackerSystem : FranchiseSystem, IModSystem
    {
        public const string NET_KEY = "tr";
        private const float REFRESH = 2f;
        private float _next;

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || Time.RealTotalTime < _next) return;
            _next = Time.RealTotalTime + REFRESH;
            TycoonBank.Standing s = TycoonBank.Read(EntityManager);
            TycoonNetData.Set(NET_KEY, Mathf.RoundToInt(s.GrossReputation * 100f).ToString());
            if (!TycoonNetData.IsHost) return;
            int total = Mathf.RoundToInt(s.GrossReputation);
            if (total != TycoonCompany.TotalReputation || s.Restaurants != TycoonCompany.RestaurantCount)
            {
                TycoonCompany.TotalReputation = total;
                TycoonCompany.RestaurantCount = s.Restaurants;
                TycoonCompany.MarkDirty();
            }
        }
    }
}
