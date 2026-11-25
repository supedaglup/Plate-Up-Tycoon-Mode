using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>The restaurant selector's questions, with the game's own yes/no pop-up.</summary>
    public class TycoonRestaurantSelectorPopup : GenericChoicePopupManager, IModSystem
    {
        public const PopupType TYPE = (PopupType)91;
        public const PopupType TEXT_SET = PopupType.LoadPreviousSave;
        public const float SELL_SHARE = 0.5f;

        internal static string Title, Text;

        private static readonly Queue<CTycoonSelectorChoice> Asks = new Queue<CTycoonSelectorChoice>();
        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>();
        private static Entity _announceAt = Entity.Null;
        private static string _announcement;

        public override PopupType ManagedType => TYPE;

        internal static void Ask(Entity slotEntity, CLocationChoice slot, bool sell)
        {
            string name = slot.RestaurantName.ToString();
            if (string.IsNullOrEmpty(name)) name = "the restaurant in slot " + slot.Slot;
            Names[slot.Slot] = name;
            int value = sell ? SaleValue(slot.Slot) : 0;
            if (sell)
            {
                Title = "Sell this restaurant?";
                Text = $"The bank buys {name} and everything in it for {value} coins: half of what its appliances and site cost. This can't be undone.";
            }
            else
            {
                Title = "Choose this restaurant?";
                Text = $"{name}, day {slot.Day}. The loading bay will take you there.";
            }
            _announceAt = slotEntity;
            Asks.Enqueue(new CTycoonSelectorChoice { Slot = slot.Slot, Sell = sell, Value = value });
            Mod.LogInfo($"HQ: asking '{Title}' for slot {slot.Slot} ({name}).");
        }

        internal static void Announce(Entity slotEntity, string text)
        {
            _announceAt = slotEntity;
            _announcement = text;
        }

        protected override void OnUpdate()
        {
            if (TycoonSession.IsActive)
            {
                while (Asks.Count > 0) PopupUtilities.RequestManagedPopup(TYPE, Asks.Dequeue());
                if (_announcement != null)
                {
                    if (_announceAt != Entity.Null && Require(_announceAt, out CPosition at))
                        ManageTycoonAnnouncements.Announce(EntityManager, Time.RealTotalTime, at.Position, _announcement, 4f);
                    _announcement = null;
                }
            }
            base.OnUpdate();
        }

        public override Entity CreateNewPopup(Entity request)
        {
            Entity popup = PopupUtilities.CreateGenericPopup(GenericChoiceType.AcceptOrCancel, TEXT_SET, PopupLocation.Centre);
            EntityManager.SetComponentData(popup, new ManagePopups.CManagedPopup { PopupType = TYPE });
            CopyData<CTycoonSelectorChoice>(request, popup);
            Mod.LogInfo("HQ: the restaurant selector's pop-up is up.");
            return popup;
        }

        protected override bool HandleDecision(Entity popup, GenericChoiceDecision decision)
        {
            Title = Text = null;
            if (!Require(popup, out CTycoonSelectorChoice choice)) return true;
            Mod.LogInfo($"HQ: the restaurant selector's pop-up was answered: {decision}.");
            if (decision != GenericChoiceDecision.Accept) return true;
            Names.TryGetValue(choice.Slot, out string name);
            if (choice.Sell) Sell(choice.Slot, name);
            else Choose(choice.Slot, name);
            return true;
        }

        private void Choose(int slot, string name)
        {
            if (!SlotEntity(slot, out Entity entity, out CLocationChoice choice)) return;
            Set(new SSelectedLocation { Valid = true, Selected = choice });
            TycoonCompany.EnsureLoaded();
            TycoonCompany.ActiveSlot = slot;
            TycoonCompany.Save();
            Announce(entity, $"Active restaurant: {name} (day {choice.Day})");
            Mod.LogInfo($"HQ: the active restaurant is now slot {slot} ({name}, day {choice.Day}).");
        }

        private void Sell(int slot, string name)
        {
            if (TycoonSites.SavedRestaurants() <= 1) { Mod.LogWarning("HQ: the company's only restaurant can't be sold."); return; }
            int value = SaleValue(slot);
            TycoonCompany.EnsureLoaded();
            try { Persistence.FullWorld.Clear(slot); }
            catch (System.Exception e) { Mod.LogWarning($"HQ: could not sell slot {slot}: {e.Message}"); return; }
            TycoonCompany.ApplianceValues.Remove(slot);
            TycoonCompany.LastTakings.Remove(slot);
            TycoonCompany.Cash += value;
            TycoonCompany.Restaurants.RemoveAll(r => r.slot == slot);
            if (TycoonCompany.ActiveSlot == slot) TycoonCompany.ActiveSlot = 0;
            TycoonCompany.Save();
            if (SlotEntity(slot, out Entity entity, out CLocationChoice choice))
            {
                choice.State = 0;
                EntityManager.SetComponentData(entity, choice);
                Announce(entity, $"Sold {name} for {value} coins");
            }
            SSelectedLocation selected = GetSingleton<SSelectedLocation>();
            if (selected.Valid && selected.Selected.Slot == slot) Set(new SSelectedLocation { Valid = false });
            Mod.LogInfo($"HQ: sold the restaurant in slot {slot} ({name}) for {value} coins; wallet {TycoonCompany.Cash}.");
        }

        private bool SlotEntity(int slot, out Entity entity, out CLocationChoice choice)
        {
            entity = Entity.Null;
            choice = default;
            using EntityQuery q = EntityManager.CreateEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(CLocationChoice) }, None = new ComponentType[] { typeof(CTycoonAnnouncement) } });
            using NativeArray<Entity> entities = q.ToEntityArray(Allocator.Temp);
            foreach (Entity e in entities)
            {
                CLocationChoice c = EntityManager.GetComponentData<CLocationChoice>(e);
                if (c.Slot != slot) continue;
                entity = e;
                choice = c;
                return true;
            }
            return false;
        }

        internal static int SaleValue(int slot)
        {
            TycoonCompany.EnsureLoaded();
            int appliances = TycoonCompany.ApplianceValues.TryGetValue(slot, out int worth) ? worth : 0;
            int site = TycoonCompany.Restaurants.Find(r => r.slot == slot).price;
            return Mathf.RoundToInt((appliances + site) * SELL_SHARE);
        }
    }

    /// <summary>Interact on a saved restaurant at the selector.</summary>
    public class TycoonChooseRestaurantInteraction : InteractionSystem, IModSystem
    {
        protected override InteractionType RequiredType => InteractionType.Act;
        protected override bool AllowAnyMode => true;

        protected override bool IsPossible(ref InteractionData data)
        {
            if (!TycoonSession.IsActive || !Has<CPlayer>(data.Interactor)) return false;
            return Require(data.Target, out CLocationChoice choice) && (int)choice.State == TycoonResumeRestaurantSystem.STATE_SAVED;
        }

        protected override void Perform(ref InteractionData data) =>
            TycoonRestaurantSelectorPopup.Ask(data.Target, EntityManager.GetComponentData<CLocationChoice>(data.Target), false);
    }

    /// <summary>Pick up on a saved restaurant at the selector: "Sell this restaurant?".</summary>
    public class TycoonSellRestaurantInteraction : InteractionSystem, IModSystem
    {
        protected override InteractionType RequiredType => InteractionType.Grab;
        protected override bool AllowAnyMode => true;

        protected override bool IsPossible(ref InteractionData data)
        {
            if (!TycoonSession.IsActive || !Has<CPlayer>(data.Interactor)) return false;
            return Require(data.Target, out CLocationChoice choice) && (int)choice.State == TycoonResumeRestaurantSystem.STATE_SAVED;
        }

        protected override void Perform(ref InteractionData data)
        {
            CLocationChoice choice = EntityManager.GetComponentData<CLocationChoice>(data.Target);
            if (TycoonSites.SavedRestaurants() <= 1)
            {
                TycoonRestaurantSelectorPopup.Announce(data.Target, "You can't sell your only restaurant");
                return;
            }
            TycoonRestaurantSelectorPopup.Ask(data.Target, choice, true);
        }
    }
}
