using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Interacting with the FRONT of a Tycoon desk opens its menu, the way the HQ dish cabinet / wardrobe do.</summary>
    [UpdateInGroup(typeof(InteractionGroup))]
    [UpdateBefore(typeof(RotateAppliances))]
    public class TycoonDeskInteractionSystem : InteractionSystem, IModSystem
    {
        protected override InteractionType RequiredType => InteractionType.Act;
        protected override bool AllowAnyMode => true;

        protected override bool IsPossible(ref InteractionData data)
        {
            if (!Has<CPlayer>(data.Interactor)) return false;
            Patches.PurchasingDeskMenu.DeskKind? kind = PurchasingDeskHelpers.KindOf(EntityManager, data.Target);
            if (kind == null) return false;
            if (kind == Patches.PurchasingDeskMenu.DeskKind.Management && !HasSingleton<SIsNightTime>()) return false;

            bool front = PurchasingDeskHelpers.IsInFront(EntityManager, data.Target, data.Interactor, out float dot);
            if (data.ShouldAct)
                Mod.LogInfo($"{kind} Desk: interact attempt (mode {data.Attempt.Mode}, front: {front}, facing score {dot:0.00}).");
            return front;
        }

        protected override void Perform(ref InteractionData data)
        {
            if (PurchasingDeskHelpers.KindOf(EntityManager, data.Target) == Patches.PurchasingDeskMenu.DeskKind.Management
                && Require(data.Interactor, out CPlayer asker) && !Players.Main.Get(asker.ID).IsLocalUser)
            {
                if (Require(data.Target, out CPosition deskAt))
                    ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, deskAt.Position, "Only the host can use the Management Desk");
                Mod.LogInfo($"Management Desk: player {asker.ID} is an online guest; only the host can use it.");
                return;
            }
            CTriggerPlayerSpecificUI trigger = default;
            if (!Require(data.Target, out trigger))
                EntityManager.AddComponent<CTriggerPlayerSpecificUI>(data.Target);

            trigger.IsTriggered = true;
            trigger.TriggerEntity = data.Interactor;
            Set(data.Target, trigger);

            Patches.PurchasingDeskMenu.DeskKind kind = PurchasingDeskHelpers.KindOf(EntityManager, data.Target) ?? Patches.PurchasingDeskMenu.DeskKind.Purchasing;
            if (Require(data.Interactor, out CPlayer player))
            {
                PurchasingDeskHelpers.LastDeskOpened[player.ID] = kind;
                PurchasingDeskHelpers.Opened[player.ID] = (PurchasingDeskHelpers.Opened.TryGetValue(player.ID, out int n) ? n : 0) + 1;
                PurchasingDeskHelpers.SendDesk(player.ID, ((int)kind).ToString());
            }
            Mod.LogInfo($"{kind} Desk: menu requested.");
        }
    }

    public static class PurchasingDeskHelpers
    {
        public const float FRONT_THRESHOLD = 0.5f;

        private const float FRONT_SIGN = -1f;

        public static bool IsInFront(EntityManager em, Entity desk, Entity player, out float dot)
        {
            dot = 0f;
            if (!em.HasComponent<CPosition>(desk) || !em.HasComponent<CPosition>(player)) return false;
            CPosition deskPos = em.GetComponentData<CPosition>(desk);
            CPosition playerPos = em.GetComponentData<CPosition>(player);

            Vector3 forward = deskPos.Forward(1f) * FRONT_SIGN;
            Vector3 toPlayer = playerPos.Position - deskPos.Position;
            forward.y = 0f;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f) return false;

            dot = Vector3.Dot(toPlayer.normalized, forward.normalized);
            return dot >= FRONT_THRESHOLD;
        }

        internal static readonly System.Collections.Generic.Dictionary<int, Patches.PurchasingDeskMenu.DeskKind> LastDeskOpened =
            new System.Collections.Generic.Dictionary<int, Patches.PurchasingDeskMenu.DeskKind>();

        internal static readonly System.Collections.Generic.Dictionary<int, int> Opened = new System.Collections.Generic.Dictionary<int, int>();

        internal static void SendDesk(int playerID, string value) =>
            TycoonNetData.Set("k" + playerID, value + "," + (Opened.TryGetValue(playerID, out int n) ? n : 0));

        internal static Patches.PurchasingDeskMenu.DeskKind? KindOf(EntityManager em, Entity e)
        {
            if (em.HasComponent<CTycoonPurchasingDesk>(e)) return Patches.PurchasingDeskMenu.DeskKind.Purchasing;
            if (em.HasComponent<CTycoonManagementDesk>(e)) return Patches.PurchasingDeskMenu.DeskKind.Management;
            if (em.HasComponent<CTycoonBankDesk>(e)) return Patches.PurchasingDeskMenu.DeskKind.Bank;
            if (em.HasComponent<CTycoonChefDesk>(e)) return Patches.PurchasingDeskMenu.DeskKind.Chef;
            if (em.HasComponent<CTycoonCardDesk>(e)) return Patches.PurchasingDeskMenu.DeskKind.Cards;
            if (em.HasComponent<CTycoonDiscountDesk>(e)) return Patches.PurchasingDeskMenu.DeskKind.Discount;
            return null;
        }

        public static bool IsDesk(EntityManager em, Entity e) => KindOf(em, e) != null;
    }
}
