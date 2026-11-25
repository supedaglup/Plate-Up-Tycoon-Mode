using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Spawns the Purchasing Desk's on-screen order menu for the player who triggered the desk.</summary>
    public class ManagePurchasingDeskIndicators : PlayerSpecificUIIndicator<CTycoonPurchasingDesk, CDishSelectionInfo>, IModSystem
    {
        protected override ViewType ViewType => ViewType.DishSelectorUI;

        protected override CDishSelectionInfo GetInfo(Entity source, CTycoonPurchasingDesk selector, CTriggerPlayerSpecificUI trigger, CPlayer player)
        {
            Mod.LogInfo($"Purchasing Desk: opening menu for player {player.ID} (prices x{TycoonShopPrices.Multiplier:0.00}).");
            return new CDishSelectionInfo
            {
                Player = player,
                PlayerEntity = trigger.TriggerEntity,
            };
        }

        protected override bool ShouldDismiss(CDishSelectionInfo info)
        {
            if (info.Dish != 0)
            {
                PlaceOrder(info.Dish, info.Player.PlayerID, info.PlayerEntity);
                return true;
            }
            return base.ShouldDismiss(info);
        }

        private void PlaceOrder(int applianceID, int playerID, Entity playerEntity)
        {
            UnityEngine.Vector3 position = Require(playerEntity, out CPosition at) ? at.Position : UnityEngine.Vector3.zero;

            if (TycoonDecorShop.TryGet(applianceID, out TycoonDecorShop.Offer decor))
            {
                Entity decorRequest = EntityManager.CreateEntity(typeof(CTycoonBlueprintOrderRequest));
                EntityManager.SetComponentData(decorRequest, new CTycoonBlueprintOrderRequest { ApplianceID = decor.DecorID, Cost = decor.Cost, PlayerID = playerID, Position = position });
                Mod.LogInfo($"Purchasing Desk: order requested for {decor.Name} ({decor.Cost}).");
                return;
            }
            if (!TycoonCatalogue.TryGet(applianceID, out TycoonCatalogue.Entry entry))
            {
                Mod.LogWarning($"Purchasing Desk: appliance {applianceID} is not in the catalogue.");
                return;
            }
            Entity request = EntityManager.CreateEntity(typeof(CTycoonBlueprintOrderRequest));
            EntityManager.SetComponentData(request, new CTycoonBlueprintOrderRequest
            {
                ApplianceID = entry.ApplianceID,
                Cost = TycoonShopPrices.OrderPrice(entry),
                PlayerID = playerID,
                Position = position,
            });
            Mod.LogInfo($"Purchasing Desk: order requested for {entry.Name} ({TycoonShopPrices.OrderPrice(entry)}, full {entry.Cost}).");
        }
    }

    /// <summary>The Management Desk's menu, same plumbing as the Purchasing Desk's.</summary>
    public class ManageManagementDeskIndicators : PlayerSpecificUIIndicator<CTycoonManagementDesk, CDishSelectionInfo>, IModSystem
    {
        protected override ViewType ViewType => ViewType.DishSelectorUI;

        protected override CDishSelectionInfo GetInfo(Entity source, CTycoonManagementDesk selector, CTriggerPlayerSpecificUI trigger, CPlayer player)
        {
            Mod.LogInfo($"Management Desk: opening menu for player {player.ID}.");
            return new CDishSelectionInfo
            {
                Player = player,
                PlayerEntity = trigger.TriggerEntity,
            };
        }

        protected override bool ShouldDismiss(CDishSelectionInfo info)
        {
            if (info.Dish != 0 && TycoonPromotionDesk.Handle(EntityManager, info.Dish, info.Player.PlayerID,
                    Require(info.PlayerEntity, out CPosition promoAt) ? promoAt.Position : UnityEngine.Vector3.zero, Time.RealTotalTime))
                return true;
            if (info.Dish != 0 && TycoonIngredientQuality.Decode(info.Dish, out int qualityTier))
            {
                TycoonIngredientQualitySystem.Pick(EntityManager, qualityTier);
                return true;
            }
            if (info.Dish != 0 && TycoonDishPricing.Decode(info.Dish, out int pricePercent))
            {
                TycoonDishPricingSystem.Pick(EntityManager, pricePercent);
                return true;
            }
            if (info.Dish != 0 && TycoonBusinessHoursSystem.Decode(info.Dish, out bool opening, out int seconds))
            {
                STycoonBusinessHours hours = HasSingleton<STycoonBusinessHours>() ? GetSingleton<STycoonBusinessHours>()
                    : new STycoonBusinessHours { Opening = (int)TycoonBusinessHoursSystem.DEFAULT_OPENING, Closing = (int)TycoonBusinessHoursSystem.DEFAULT_CLOSING };
                if (opening) hours.Opening = seconds; else hours.Closing = seconds;
                Set(hours);
                Mod.LogInfo($"Management Desk: {(opening ? "Opening" : "Closing")} set to {seconds} s.");
                return true;
            }
            if (info.Dish != 0)
            {
                UnityEngine.Vector3 position = Require(info.PlayerEntity, out CPosition at) ? at.Position : UnityEngine.Vector3.zero;
                Entity request = EntityManager.CreateEntity(typeof(CTycoonCardToggleRequest));
                EntityManager.SetComponentData(request, new CTycoonCardToggleRequest
                {
                    UnlockID = info.Dish,
                    PlayerID = info.Player.PlayerID,
                    Position = position,
                });
                Mod.LogInfo($"Management Desk: card {info.Dish} toggle requested.");
                return true;
            }
            return base.ShouldDismiss(info);
        }
    }
}
