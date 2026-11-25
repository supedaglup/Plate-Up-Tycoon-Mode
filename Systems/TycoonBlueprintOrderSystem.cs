using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Processes order requests from the Purchasing Desk: takes the money out of SMoney.</summary>
    public class TycoonBlueprintOrderSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery _requests;

        protected override void Initialise()
        {
            base.Initialise();
            _requests = GetEntityQuery(new QueryHelper().All(typeof(CTycoonBlueprintOrderRequest)));
            RequireSingletonForUpdate<STycoonMode>();
            RequireSingletonForUpdate<SMoney>();
            RequireForUpdate(_requests);
        }

        protected override void OnUpdate()
        {
            int night = HasSingleton<STycoonOrders>() ? GetSingleton<STycoonOrders>().NightCounter : 0;
            int day = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;

            using NativeArray<Entity> requests = _requests.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < requests.Length; i++)
            {
                CTycoonBlueprintOrderRequest request = EntityManager.GetComponentData<CTycoonBlueprintOrderRequest>(requests[i]);
                SMoney money = GetSingleton<SMoney>();
                string player = TycoonPurchaseLog.PlayerName(request.PlayerID);
                string item = TycoonDecorShop.TryGetAny(request.ApplianceID, out TycoonDecorShop.Offer decor) ? decor.Name
                    : TycoonCatalogue.TryGetAny(request.ApplianceID, out TycoonCatalogue.Entry entry) ? entry.Name : request.ApplianceID.ToString();

                if (request.ApplianceID != 0 && money.Amount >= request.Cost)
                {
                    money.Amount -= request.Cost;
                    Set(money);
                    Entity order = EntityManager.CreateEntity(typeof(CTycoonPendingDelivery));
                    EntityManager.SetComponentData(order, new CTycoonPendingDelivery { ApplianceID = request.ApplianceID, OrderedOnNight = night });

                    if (HasSingleton<SIsDayTime>())
                        MoneyTracker.AddEvent(new EntityContext(EntityManager), Mod.Ledger.Purchases, -request.Cost);

                    TycoonPurchaseLog.Add(day, player, item, request.Cost);
                    Announce(request.Position, $"{player} purchased a {item} for {request.Cost}");
                    Mod.LogInfo($"Order placed: {player} bought {item} for {request.Cost}. Cash now {money.Amount}.");
                }
                else
                {
                    Announce(request.Position, $"Not enough money for {item} ({request.Cost})");
                    Mod.LogWarning($"Order refused: {item} costs {request.Cost}, cash is {money.Amount}.");
                }

                EntityManager.DestroyEntity(requests[i]);
            }
        }

        private void Announce(UnityEngine.Vector3 position, string text) => ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, position, text);
    }
}
