using HarmonyLib;
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
    /// <summary>The Card Market's cards look like a paper with the card on it.</summary>
    public class TycoonCardItemViewSystem : ViewSystemBase, IModSystem
    {
        private const float RESEND = 3f;
        private static readonly System.Type ViewDataType = AccessTools.Inner(typeof(DishChoiceView), "ViewData");
        private static readonly System.Reflection.MethodInfo Send =
            ViewDataType == null ? null : AccessTools.Method(typeof(ViewSystemBase), "SendUpdate")?.MakeGenericMethod(ViewDataType);

        private EntityQuery _cards;
        private readonly Dictionary<Entity, float> _sentAt = new Dictionary<Entity, float>();
        private bool _failed;

        protected override void Initialise()
        {
            base.Initialise();
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCardItem), typeof(CLinkedView)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || _cards.IsEmpty || Send == null || _failed) return;
            float now = Time.RealTotalTime;
            try
            {
                using NativeArray<Entity> entities = _cards.ToEntityArray(Allocator.Temp);
                using NativeArray<CTycoonCardItem> cards = _cards.ToComponentDataArray<CTycoonCardItem>(Allocator.Temp);
                using NativeArray<CLinkedView> views = _cards.ToComponentDataArray<CLinkedView>(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    if (_sentAt.TryGetValue(entities[i], out float at) && now - at < RESEND) continue;
                    _sentAt[entities[i]] = now;
                    object data = System.Activator.CreateInstance(ViewDataType);
                    AccessTools.Field(ViewDataType, "Dish").SetValue(data, cards[i].Card);
                    Send.Invoke(this, new object[] { (ViewIdentifier)views[i], data, MessageType.ViewUpdate, 0 });
                }
                if (_sentAt.Count > 200) _sentAt.Clear();
            }
            catch (System.Exception e)
            {
                _failed = true;
                Mod.LogWarning("Card Market: could not show the cards on their papers: " + e.Message);
            }
        }
    }
}

namespace PlateUpTycoon.Patches
{
    /// <summary>Lets DishChoiceView draw Card Market cards as well as dishes.</summary>
    [HarmonyPatch(typeof(DishChoiceView), "UpdateData")]
    public static class CardPaperViewPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(DishChoiceView __instance, object[] __args)
        {
            try
            {
                object data = __args[0];
                int id = (int)AccessTools.Field(data.GetType(), "Dish").GetValue(data);
                bool onCardModel = __instance.transform.Find("Card") != null;
                if (id == 0 || (!onCardModel && GameData.Main.TryGet<Dish>(id, out Dish _, false)) || !GameData.Main.TryGet<Unlock>(id, out Unlock card, false))
                {
                    UI.TycoonCardPreview.Cards.Remove(__instance);
                    return true;
                }
                UI.TycoonCardPreview.Cards[__instance] = card.ID;

                AccessTools.Field(typeof(DishChoiceView), "Data").SetValue(__instance, data);
                if (AccessTools.Field(typeof(DishChoiceView), "Label").GetValue(__instance) is Component label && label != null)
                    label.GetType().GetProperty("text")?.SetValue(label, card is Dish ? TycoonRecipeShop.NameOf(card.ID) : card.Name);
                Customs.CardItem.Tint(__instance, card.Colour);
                try { Customs.CardItem.ShowCard(__instance, card); }
                catch (System.Exception e) { Mod.LogWarning("Card Market: could not put the real card on: " + e.Message); }
                return false;
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Card Market: could not draw a card paper: " + e.Message);
                return true;
            }
        }
    }
}
