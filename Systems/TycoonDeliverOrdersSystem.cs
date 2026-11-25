using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Delivers the appliances ordered before tonight as parcels at the post.</summary>
    public class TycoonDeliverOrdersSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _pending;

        protected override void Initialise()
        {
            base.Initialise();
            _pending = GetEntityQuery(new QueryHelper().All(typeof(CTycoonPendingDelivery)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<SIsNightFirstUpdate>();
        }

        protected override void OnUpdate()
        {
            STycoonOrders state = HasSingleton<STycoonOrders>() ? GetSingleton<STycoonOrders>() : default;
            state.NightCounter++;
            Set(state);

            if (_pending.IsEmpty)
                return;

            List<Vector3> postTiles = GetPostTiles(false);
            var envelopes = World.GetExistingSystem<ProvideStartingEnvelopes>();
            int placedTile = 0;

            using NativeArray<Entity> orders = _pending.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < orders.Length; i++)
            {
                CTycoonPendingDelivery order = EntityManager.GetComponentData<CTycoonPendingDelivery>(orders[i]);
                if (order.ApplianceID == 0)
                {
                    EntityManager.DestroyEntity(orders[i]);
                    continue;
                }
                if (order.OrderedOnNight >= state.NightCounter)
                    continue;

                if (envelopes == null || !envelopes.FindTile(ref placedTile, postTiles, out Vector3 position))
                {
                    Mod.LogWarning("No free post tile for a delivery; remaining orders will arrive tomorrow.");
                    break;
                }

                if (TycoonDecorShop.IsDecor(order.ApplianceID))
                {
                    TycoonDecorShop.CreateItem(EntityManager, order.ApplianceID, position);
                    EntityManager.DestroyEntity(orders[i]);
                    Mod.LogInfo($"Delivered wallpaper/flooring {order.ApplianceID}.");
                    continue;
                }
                PostHelpers.CreateApplianceParcel(EntityManager, position, order.ApplianceID);
                EntityManager.DestroyEntity(orders[i]);
                Mod.LogInfo($"Delivered parcel for appliance {order.ApplianceID}.");
            }
        }
    }
}
