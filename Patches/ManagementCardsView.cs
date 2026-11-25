using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using PlateUpTycoon.Components;
using PlateUpTycoon.UI;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Patches
{
    /// <summary>The Management Desk's card manager, in the style of the Cards Manager mod.</summary>
    internal static class ManagementCardsView
    {
        internal static Vector3 FanCentre = new Vector3(0f, 0.3f, -0.5f);
        private static readonly Vector3 RadiusVector = new Vector3(0f, 30f, 0f);
        private static readonly Vector3 SelectedCardPopOffset = new Vector3(0f, 0.6f, -0.2f);
        private static readonly Vector3 StackingZOffset = new Vector3(0f, 0f, -0.12f);
        private const float AngularPitch = 1f;
        private const float PathGap = 0.6f;
        private const float DefaultCardWidth = 2f, DefaultCardHeight = 2.7f;
        private const float CardScale = 0.72f;
        private const float ColumnStep = 1.08f;
        private const float MaxRowWidth = 11f;
        private const float RowStep = 1.25f;
        private const float SelectedScale = 1.12f;
        private const float RowDepth = 0.3f;
        private static readonly Vector2 ScreenPoint = new Vector2(0.5f, 0.58f);
        private const float CardScreenHeight = 0.3f;
        private const int UiLayer = 5;
        internal static int GridRowLength = 2, GridRows = 1;
        private static readonly Color DisabledColour = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color LockedColour = new Color(0.8f, 0.55f, 0.15f);

        private class State
        {
            public CardScrollerElement Scroller;
            public UnlockCardElement Template;
            public GameObject Fan;
            public readonly List<CTycoonCard> Cards = new List<CTycoonCard>();
            public readonly List<UnlockCardElement> Elements = new List<UnlockCardElement>();
            public readonly Dictionary<int, Color> DefaultColours = new Dictionary<int, Color>();
            public readonly List<int> PathIndex = new List<int>();
            public readonly List<int> RowOf = new List<int>(), ColOf = new List<int>();
            public readonly List<int> RowSize = new List<int>();
            public readonly List<string> RowName = new List<string>();
            public float CardWidth, CardHeight;
            public float MaxRowLocal;
            public GameObject Close;
            public Component CloseText;
            public readonly List<Component> RowLabels = new List<Component>();
            public readonly List<string> RowLabelText = new List<string>();
            public bool CloseShownSelected = true;
            public int Selected = -1;
            public string Note;
            public int Mode;
            public readonly List<GridItemPromotion> Promos = new List<GridItemPromotion>();
        }

        private static readonly Dictionary<DishSelectionIndicator, State> Views = new Dictionary<DishSelectionIndicator, State>();
        private static DishSelectionIndicator _lastOpened;

        internal static bool IsOpen(DishSelectionIndicator indicator) => Views.ContainsKey(indicator);
        internal static bool AnyOpen => Views.Count > 0;

        internal static bool Shape(GenericPaginatedGridMenu menu, out int rowLength, out int rows)
        {
            rowLength = GridRowLength; rows = GridRows;
            if (!(AccessTools.Field(typeof(GenericPaginatedGridMenu), "Items").GetValue(menu) is List<IGridItem> items) || items.Count == 0) return true;
            int closes = 0;
            while (closes < items.Count && items[closes] is GridItemDeskClose) closes++;
            if (closes == 0) return true;
            rowLength = closes;
            rows = (items.Count + closes - 1) / closes;
            return true;
        }

        internal static bool IsCardGrid(GenericPaginatedGridMenu menu)
        {
            if (Views.Count == 0 || menu == null) return false;
            if (!(AccessTools.Field(typeof(GenericPaginatedGridMenu), "Items").GetValue(menu) is List<IGridItem> items)) return false;
            foreach (IGridItem item in items) if (item is GridItemDeskCard || (item is GridItemPromotion p && p.CardID != 0)) return true;
            return false;
        }

        internal static GridMenuConfig Open(DishSelectionIndicator indicator, int mode = 0)
        {
            Forget(indicator);
            var state = new State { Mode = mode };
            Views[indicator] = state;
            _lastOpened = indicator;

            var page = (GridMenuPaginatedGenericConfig)(object)ScriptableObject.CreateInstance(typeof(GridMenuPaginatedGenericConfig));
            page.Items = new List<IGridItem> { PurchasingDeskMenu.CloseTile(), PurchasingDeskMenu.CloseTile() };
            GridRowLength = 2;
            GridRows = 1;

            try
            {
                object c = AccessTools.Field(typeof(DishSelectionIndicator), "Container").GetValue(indicator);
                Transform container = c as Transform ?? (c as GameObject)?.transform ?? (c as Component)?.transform;
                if ((object)container == null) { Mod.LogWarning("Card manager: menu has no container."); return page; }

                state.Scroller = ModuleDirectory.Add<CardScrollerElement>(container, Vector2.zero);
                Transform fanParent = (object)state.Scroller.Container != null ? state.Scroller.Container.transform : state.Scroller.transform;
                state.Template = state.Scroller.Card;

                state.Fan = new GameObject("Tycoon Card Manager Fan");
                state.Fan.layer = UiLayer;

                LoadCards(state);
                int length = 2;
                foreach (int n in state.RowSize) length = Mathf.Max(length, n);
                page.Items.Clear();
                for (int k = 0; k < length; k++) page.Items.Add(PurchasingDeskMenu.CloseTile());
                for (int r = 0; r < state.RowSize.Count; r++)
                {
                    var row = new List<int>();
                    for (int i = 0; i < state.Cards.Count; i++) if (state.RowOf[i] == r) row.Add(i);
                    for (int k = 0; k < length; k++)
                    {
                        int i = row[Mathf.Min(k, row.Count - 1)];
                        page.Items.Add(state.Mode == 0 ? (IGridItem)new GridItemDeskCard { UnlockID = state.Cards[i].UnlockID } : state.Promos[i]);
                    }
                }
                GridRowLength = length;
                GridRows = state.RowSize.Count + 1;

                if ((object)state.Scroller.Container != null) state.Scroller.Container.SetActive(false);
                if ((object)state.Template != null) state.Template.gameObject.SetActive(false);

                MakeCloseButton(state);
                TycoonCardPreview.SetLayer(state.Fan.transform, UiLayer);
                PlaceOnScreen(state);
                Mod.LogInfo($"Card manager: {state.Cards.Count} card(s) in {state.RowSize.Count} row(s): {string.Join(", ", state.RowName)}.");
            }
            catch (System.Exception e)
            {
                Mod.LogError("Card manager: could not build the card fan: " + e);
            }
            return page;
        }

        internal static void Forget(DishSelectionIndicator indicator)
        {
            if (!Views.TryGetValue(indicator, out State state)) return;
            Views.Remove(indicator);
            HiddenGrids.RemoveWhere(g => g == null);
            if ((object)state.Fan != null) Object.Destroy(state.Fan);
            if ((object)_lastOpened == (object)indicator) _lastOpened = null;
        }

        private static void LoadCards(State state)
        {
            foreach (UnlockCardElement element in state.Elements)
                if ((object)element != null) Object.Destroy(element.gameObject);
            state.Elements.Clear();
            state.Cards.Clear();
            foreach (Component old in state.RowLabels) if ((object)old != null) Object.Destroy(old.gameObject);
            state.RowLabels.Clear();
            state.RowLabelText.Clear();

            EntityManager em = KitchenLib.Utils.EntityUtils.GetEntityManager();

            if (state.Mode != 0) LoadPromotions(state);
            else
            {
            state.Cards.AddRange(Systems.TycoonCards.All(em));
            state.PathIndex.Clear();
            HashSet<int> dishes = CardDishes(state.Cards);
            int lastRoot = int.MinValue, path = -1;
            foreach (CTycoonCard card in state.Cards)
            {
                int root = Systems.TycoonCards.PathRoot(card.UnlockID, dishes);
                if (root != lastRoot) { path++; lastRoot = root; }
                state.PathIndex.Add(path);
            }
            BuildRows(state, em);
            }
            foreach (CTycoonCard card in state.Cards)
            {
                UnlockCardElement element = Object.Instantiate(state.Template, state.Fan.transform, false);
                element.gameObject.SetActive(true);
                element.transform.localScale = state.Template.transform.localScale * CardScale;
                element.SetUnlock(card.UnlockID);
                if (!state.DefaultColours.ContainsKey(card.UnlockID)) state.DefaultColours[card.UnlockID] = GetColour(element);
                state.Elements.Add(element);
            }
            state.Note = state.Cards.Count > 0 ? null
                : state.Mode == 1 ? "No marketing offers right now."
                : state.Mode == 2 ? "No contracts right now."
                : "This restaurant has no cards yet.";
        }

        private static void LoadPromotions(State state)
        {
            state.Promos.Clear();
            state.RowOf.Clear(); state.ColOf.Clear(); state.RowSize.Clear(); state.RowName.Clear();
            foreach (KeyValuePair<string, List<GridItemPromotion>> row in ManagementDeskMenu.PromotionRows(state.Mode))
            {
                List<GridItemPromotion> cards = row.Value.FindAll(x => x.CardID != 0);
                if (cards.Count == 0) continue;
                int r = state.RowSize.Count;
                state.RowSize.Add(0);
                state.RowName.Add(row.Key);
                foreach (GridItemPromotion p in cards)
                {
                    state.Cards.Add(new CTycoonCard { UnlockID = p.CardID, Enabled = true });
                    state.Promos.Add(p);
                    state.RowOf.Add(r);
                    state.ColOf.Add(state.RowSize[r]);
                    state.RowSize[r]++;
                }
            }
        }

        private static readonly Color MarketingOfferColour = new Color(0.9f, 0.35f, 0.65f), ContractOfferColour = new Color(0.95f, 0.72f, 0.15f),
            RunningColour = new Color(0.35f, 0.8f, 0.4f);

        private static readonly Color LuxuryColour = new Color(1f, 0.78f, 0.05f), RegularContractColour = new Color(0.3f, 0.55f, 0.95f);

        private static Color PromotionColour(GridItemPromotion p) =>
            (p.Kind == ManagementDeskMenu.P_CONTRACT_OFFER || p.Kind == ManagementDeskMenu.P_CONTRACT_BOOKED) && TycoonPromotions.ContractAt(p.Slot) is TycoonPromotions.Contract lc && lc.Luxury ? LuxuryColour
            : p.Kind == ManagementDeskMenu.P_CONTRACT_OFFER ? RegularContractColour
            : p.Kind == ManagementDeskMenu.P_MARKETING_OFFER ? MarketingOfferColour
            : p.Kind == ManagementDeskMenu.P_CONTRACT_OFFER ? ContractOfferColour
            : RunningColour;

        private static void BuildRows(State state, EntityManager em)
        {
            HashSet<int> bought = Systems.TycoonCards.Bought(em);
            int KeyOf(CTycoonCard c) => bought.Contains(c.UnlockID) ? (Systems.TycoonCards.IsRecipe(c.UnlockID) ? 100 : 101) : Mathf.Max(0, c.Star);
            var keys = new List<int>();
            foreach (CTycoonCard c in state.Cards) if (!keys.Contains(KeyOf(c))) keys.Add(KeyOf(c));
            keys.Sort();
            state.RowOf.Clear(); state.ColOf.Clear(); state.RowSize.Clear(); state.RowName.Clear();
            foreach (int k in keys)
            {
                state.RowSize.Add(0);
                state.RowName.Add(k == 0 ? "Starting cards" : k == 100 ? "Bought recipes" : k == 101 ? "Bought customer cards"
                    : k == Systems.TycoonRecipeMastery.CARD_STAR ? "Recipe Mastery unlocks" : $"{k}-star cards");
            }
            foreach (CTycoonCard c in state.Cards)
            {
                int r = keys.IndexOf(KeyOf(c));
                state.RowOf.Add(r);
                state.ColOf.Add(state.RowSize[r]);
                state.RowSize[r]++;
            }
        }

        private static IEnumerable<State> MineFirst()
        {
            if ((object)DeskMenuContext.Current != null && Views.TryGetValue(DeskMenuContext.Current, out State mine)) yield return mine;
            foreach (KeyValuePair<DishSelectionIndicator, State> kv in Views)
                if (!ReferenceEquals(kv.Key, DeskMenuContext.Current)) yield return kv.Value;
        }

        internal static string RowNameFor(int unlockID)
        {
            foreach (State state in MineFirst())
            {
                int i = state.Cards.FindIndex(x => x.UnlockID == unlockID);
                if (i >= 0 && i < state.RowOf.Count && state.RowOf[i] < state.RowName.Count) return state.RowName[state.RowOf[i]];
            }
            return null;
        }

        private static void MeasureCard(State state)
        {
            if (state.CardWidth > 0f || state.Elements.Count == 0 || (object)state.Elements[0] == null || (object)state.Fan == null) return;
            state.CardWidth = DefaultCardWidth * CardScale;
            state.CardHeight = DefaultCardHeight * CardScale;
            try
            {
                Renderer[] renderers = state.Elements[0].GetComponentsInChildren<Renderer>(false);
                if (renderers.Length == 0) return;
                Transform fan = state.Fan.transform;
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                foreach (Renderer r in renderers)
                {
                    Bounds b = r.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                        Vector3 local = fan.InverseTransformPoint(corner);
                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                    }
                }
                float w = max.x - min.x, h = max.y - min.y;
                if (w > 0.1f && h > 0.1f && w < 50f && h < 50f) { state.CardWidth = w; state.CardHeight = h; }
                Mod.LogInfo($"Card manager: a card measures {w:0.##} x {h:0.##} (using {state.CardWidth:0.##} x {state.CardHeight:0.##}).");
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Card manager: could not measure a card: " + e.Message);
            }
        }

        private static readonly HashSet<GridMenu> HiddenGrids = new HashSet<GridMenu>();

        private static void HideGrid(GridMenu menu)
        {
            if (menu == null || HiddenGrids.Contains(menu)) return;
            HiddenGrids.Add(menu);
            try
            {
                if ((object)menu.Panel != null) menu.Panel.gameObject.SetActive(false);
                if (menu.Grid != null)
                {
                    foreach (ModuleInstance module in menu.Grid.Modules)
                    {
                        if (!(module?.Module is Component component) || (object)component == null) continue;
                        foreach (Renderer r in component.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    }
                }
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Card manager: could not hide the tile page: " + e.Message);
            }
        }

        internal static void Sync(DishSelectionIndicator indicator)
        {
            if (!Views.TryGetValue(indicator, out State state)) return;
            DeskMenuContext.Current = indicator;
            if (AccessTools.Field(typeof(DishSelectionIndicator), "GridMenu").GetValue(indicator) is GenericPaginatedGridMenu current && !IsCardGrid(current))
            {
                Forget(indicator);
                return;
            }
            if (AccessTools.Field(typeof(DishSelectionIndicator), "GridMenu").GetValue(indicator) is GridMenu grid) HideGrid(grid);

            if (state.Mode == 0 && CardsChanged(state))
            {
                if (SameCards(state)) RefreshStates(state);
                else { LoadCards(state); TycoonCardPreview.SetLayer(state.Fan.transform, UiLayer); }
            }

            int selected = -1;
            if (AccessTools.Field(typeof(DishSelectionIndicator), "GridMenu").GetValue(indicator) is GridMenu menu
                && menu.Grid != null && menu.Grid.Selected != null)
            {
                if (menu.Grid.Selected.Item is GridItemDeskCard card) selected = state.Cards.FindIndex(x => x.UnlockID == card.UnlockID);
                else if (menu.Grid.Selected.Item is GridItemPromotion promo && promo.CardID != 0) selected = state.Cards.FindIndex(x => x.UnlockID == promo.CardID);
            }
            state.Selected = selected;
            if (state.Mode != 0) ManagementDeskMenu.Disarm(selected >= 0 && selected < state.Promos.Count ? ManagementDeskMenu.ActionCode(state.Promos[selected]) : 0);
            Redraw(state);
            PlaceOnScreen(state, indicator);

            if (selected < 0)
                SetTitle(indicator, state.Note ?? (state.Mode == 0 ? "Close the card manager" : "Close"));
        }

        private static void Redraw(State state)
        {
            MeasureCard(state);
            float w = state.CardWidth > 0f ? state.CardWidth : DefaultCardWidth * CardScale;
            float h = state.CardHeight > 0f ? state.CardHeight : DefaultCardHeight * CardScale;
            int selectedRow = state.Selected >= 0 && state.Selected < state.RowOf.Count ? state.RowOf[state.Selected] : 0;
            for (int i = 0; i < state.Elements.Count; i++)
            {
                UnlockCardElement element = state.Elements[i];
                if ((object)element == null || i >= state.RowOf.Count) continue;
                int row = state.RowOf[i], col = state.ColOf[i], n = Mathf.Max(1, state.RowSize[row]);
                float maxRow = state.MaxRowLocal > 0f ? state.MaxRowLocal : w * MaxRowWidth;
                float step = Mathf.Min(w * ColumnStep, Mathf.Max(w * 0.3f, maxRow - w) / Mathf.Max(1, n - 1));
                int away = row - selectedRow;
                Vector3 position = new Vector3((col - (n - 1) / 2f) * step, -away * h * RowStep, Mathf.Abs(away) * RowDepth - col * 0.01f);
                if (i == state.Selected) position += new Vector3(0f, h * 0.12f, -0.4f);
                element.transform.localPosition = position;
                element.transform.localEulerAngles = Vector3.zero;
                if ((object)state.Template != null)
                    element.transform.localScale = state.Template.transform.localScale * CardScale * (i == state.Selected ? SelectedScale : 1f);
                SetColour(element, state.Mode != 0 && i < state.Promos.Count ? PromotionColour(state.Promos[i]) : ColourFor(state, state.Cards[i]));
                if (state.Mode == 0 && i < state.Cards.Count && GameData.Main.TryGet<Dish>(state.Cards[i].UnlockID, out Dish tagDish, false))
                    TycoonCardNoXpPatch.ShowCustomerCut(element, tagDish, state.Cards[i].Enabled || state.Cards[i].Vanilla);
                else if (state.Mode == 0 && i < state.Cards.Count && TycoonReputation.GroupIncrease(state.Cards[i].UnlockID) > 0)
                    TycoonCardNoXpPatch.ShowGroupSeats(element, state.Cards[i].Enabled || state.Cards[i].Vanilla);
            }

            EnsureRowLabels(state);
            for (int r = 0; r < state.RowLabels.Count; r++)
            {
                Component label = state.RowLabels[r];
                if ((object)label == null) continue;
                int away = r - selectedRow;
                label.transform.localPosition = new Vector3(0f, -away * h * RowStep - h * 0.5f - h * 0.1f, Mathf.Abs(away) * RowDepth - 0.05f);
                bool current = state.Selected >= 0 && away == 0;
                string name = r < state.RowName.Count ? state.RowName[r] : "";
                string text = current ? $"<color=#FFE14D><b>> {name.ToUpperInvariant()}</b></color>" : $"<color=#C8C8C8>{name}</color>";
                if (state.RowLabelText[r] != text)
                {
                    state.RowLabelText[r] = text;
                    label.GetType().GetProperty("text")?.SetValue(label, text);
                }
            }

            if ((object)state.Close != null)
            {
                bool closeSelected = state.Selected < 0;
                int away = -1 - (closeSelected ? -1 : selectedRow);
                state.Close.transform.localPosition = new Vector3(0f, -away * h * RowStep * 0.75f, Mathf.Abs(away) * RowDepth - (closeSelected ? 0.4f : 0f));
                state.Close.transform.localScale = Vector3.one * (closeSelected ? 1.25f : 1f);
                if (closeSelected != state.CloseShownSelected) { state.CloseShownSelected = closeSelected; SetCloseText(state); }
            }
        }

        private static void PlaceOnScreen(State state, DishSelectionIndicator indicator = null)
        {
            float slotX = ScreenPoint.x;
            int sharing = (object)indicator != null ? DeskMenuContext.ScreenSlot(indicator, out slotX) : 1;
            if (sharing <= 1) slotX = ScreenPoint.x;
            if ((object)state.Fan == null) return;
            Camera camera = TycoonCardPreview.MenuCamera();
            if (camera == null) return;
            MeasureCard(state);
            float screenHeight = camera.orthographic ? camera.orthographicSize * 2f : 10f;
            float h = state.CardHeight > 0f ? state.CardHeight : DefaultCardHeight * CardScale;
            float scale = screenHeight * CardScreenHeight / h / (sharing > 1 ? 1.6f : 1f);
            Transform t = state.Fan.transform;
            t.position = camera.ViewportToWorldPoint(new Vector3(slotX, ScreenPoint.y, camera.nearClipPlane + 5f));
            t.rotation = camera.transform.rotation;
            t.localScale = Vector3.one * scale;
            state.MaxRowLocal = screenHeight * camera.aspect * 0.92f / sharing / scale;
        }

        private static void MakeCloseButton(State state)
        {
            if ((object)state.Close != null || state.Elements.Count == 0 || (object)state.Elements[0] == null) return;
            state.Close = new GameObject("Tycoon Card Manager Close");
            state.Close.transform.SetParent(state.Fan.transform, false);

            Component template = null;
            foreach (Component c in state.Elements[0].GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().Name.StartsWith("TextMeshPro")) { template = c; break; }
            if (template == null) { Mod.LogWarning("Card manager: no card text to copy for the Close button."); return; }
            Vector3 world = template.transform.lossyScale, cardWorld = state.Elements[0].transform.lossyScale;
            GameObject text = Object.Instantiate(template.gameObject, state.Close.transform, false);
            foreach (Transform child in text.transform) Object.Destroy(child.gameObject);
            text.transform.localPosition = new Vector3(0f, 0f, -0.06f);
            text.transform.localRotation = Quaternion.identity;
            float k = cardWorld.x != 0f ? world.x / cardWorld.x * CardScale : 1f;
            text.transform.localScale = Vector3.one * k * 1.4f;
            state.CloseText = text.GetComponent(template.GetType());
            System.Type type = state.CloseText.GetType();
            type.GetProperty("richText")?.SetValue(state.CloseText, true);
            type.GetProperty("enableWordWrapping")?.SetValue(state.CloseText, false);
            type.GetProperty("color")?.SetValue(state.CloseText, Color.white);
            System.Reflection.PropertyInfo alignment = type.GetProperty("alignment");
            if (alignment != null) alignment.SetValue(state.CloseText, System.Enum.ToObject(alignment.PropertyType, 514));
            state.CloseShownSelected = false;
            SetCloseText(state);
        }

        private static void EnsureRowLabels(State state)
        {
            if (state.RowLabels.Count == state.RowSize.Count) return;
            foreach (Component old in state.RowLabels) if ((object)old != null) Object.Destroy(old.gameObject);
            state.RowLabels.Clear();
            state.RowLabelText.Clear();
            if (state.Elements.Count == 0 || (object)state.Elements[0] == null) return;
            Component template = null;
            foreach (Component c in state.Elements[0].GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().Name.StartsWith("TextMeshPro")) { template = c; break; }
            if (template == null) return;
            Vector3 world = template.transform.lossyScale, cardWorld = state.Elements[0].transform.lossyScale;
            float k = cardWorld.x != 0f ? world.x / cardWorld.x * CardScale : 1f;
            for (int r = 0; r < state.RowSize.Count; r++)
            {
                GameObject o = Object.Instantiate(template.gameObject, state.Fan.transform, false);
                o.name = "Tycoon Card Manager Row " + r;
                foreach (Transform child in o.transform) Object.Destroy(child.gameObject);
                o.transform.localRotation = Quaternion.identity;
                o.transform.localScale = Vector3.one * k * 1.1f;
                Component text = o.GetComponent(template.GetType());
                System.Type type = text.GetType();
                type.GetProperty("richText")?.SetValue(text, true);
                type.GetProperty("enableWordWrapping")?.SetValue(text, false);
                type.GetProperty("color")?.SetValue(text, Color.white);
                System.Reflection.PropertyInfo alignment = type.GetProperty("alignment");
                if (alignment != null) alignment.SetValue(text, System.Enum.ToObject(alignment.PropertyType, 514));
                state.RowLabels.Add(text);
                state.RowLabelText.Add(null);
            }
            TycoonCardPreview.SetLayer(state.Fan.transform, UiLayer);
        }

        private static void SetCloseText(State state)
        {
            if ((object)state.CloseText == null) return;
            string text = state.CloseShownSelected
                ? "<size=220%><color=#FF3B3B>X</color></size>\n<color=#FFE14D>Close</color>"
                : "<size=220%><color=#C42020>X</color></size>\nClose";
            state.CloseText.GetType().GetProperty("text")?.SetValue(state.CloseText, text);
        }

        internal static void Toggle(int unlockID)
        {
            try
            {
                EntityManager em = KitchenLib.Utils.EntityUtils.GetEntityManager();
                Vector3 position = Vector3.zero;
                int playerID = 0;
                DishSelectionIndicator source = (object)DeskMenuContext.Current != null ? DeskMenuContext.Current : _lastOpened;
                if ((object)source != null && source != null)
                {
                    position = source.transform.position;
                    playerID = (int)AccessTools.Field(typeof(DishSelectionIndicator), "PlayerID").GetValue(source);
                }
                Entity request = em.CreateEntity(typeof(CTycoonCardToggleRequest));
                em.SetComponentData(request, new CTycoonCardToggleRequest { UnlockID = unlockID, PlayerID = playerID, Position = position });
            }
            catch (System.Exception e)
            {
                Mod.LogError("Card manager: could not switch the card: " + e);
            }
        }

        internal static string TitleFor(int unlockID)
        {
            CTycoonCard card = default;
            bool found = false;
            foreach (State state in MineFirst())
            {
                int i = state.Cards.FindIndex(x => x.UnlockID == unlockID);
                if (i >= 0) { card = state.Cards[i]; found = true; break; }
            }
            string name = CardName(unlockID);
            if (!found) return name;
            List<CTycoonCard> all = new List<CTycoonCard>();
            foreach (State s in MineFirst()) { all = s.Cards; break; }
            string path = Systems.TycoonCards.PathName(Systems.TycoonCards.PathRoot(unlockID, CardDishes(all)));
            string group = RowNameFor(unlockID) ?? (card.Star == 0 ? "Starting cards" : $"{card.Star}-star cards");
            bool applied = Systems.TycoonCards.Applied(KitchenLib.Utils.EntityUtils.GetEntityManager()).Contains(unlockID);
            bool recipe = GameData.Main.TryGet<Unlock>(unlockID, out Unlock unlock) && unlock is Dish;
            bool takeOut = Mod.TakeOutCard != null && unlockID == Mod.TakeOutCard.ID;
            string onOff = card.Vanilla ? "ON (added by an older build, stays on)"
                : !card.Enabled ? "OFF - select to switch on"
                : Systems.TycoonCardEffectsSystem.WaitingForRecipe.Contains(unlockID) ? "ON - waiting for its recipe to be switched on"
                : recipe && !applied ? "ON - waiting for its Needed Items; select to switch off"
                : "ON - select to switch off";
            if (takeOut && card.Enabled) onOff += " (sit-down service needs a dining table)";
            if (Mod.FastCasualCard != null && unlockID == Mod.FastCasualCard.ID && !card.Enabled)
                onOff = "OFF - select to switch on (needs Take-Out on and a dining table)";
            float value = card.Star > 0 ? TycoonReputation.CardValue(unlockID) : 0f;
            string worth = value > 0f ? $" (+{value:0.#} reputation per serve)" : "";
            if (value > 0f && (card.Enabled || card.Vanilla) && TycoonReputation.GroupIncrease(unlockID) > 0 && TycoonReputation.GroupSeatShare < 0.999f)
                worth = $" (+{TycoonReputation.CardValueNow(unlockID):0.#} of {value:0.#} reputation per serve: your largest table can't seat its biggest groups)";
            var owned = new HashSet<int>();
            foreach (CTycoonCard c in all) owned.Add(c.UnlockID);
            string mastery = Systems.TycoonRecipeMastery.TitleText(unlockID, owned);
            return $"{group} ({path}): {name}{worth}  -  {onOff}{(mastery != null ? "  -  " + mastery : "")}";
        }

        private static bool CardsChanged(State state)
        {
            List<CTycoonCard> now = Systems.TycoonCards.All(KitchenLib.Utils.EntityUtils.GetEntityManager());
            if (now.Count != state.Cards.Count) return true;
            for (int i = 0; i < now.Count; i++)
                if (now[i].UnlockID != state.Cards[i].UnlockID || now[i].Enabled != state.Cards[i].Enabled) return true;
            return false;
        }

        private static bool SameCards(State state)
        {
            List<CTycoonCard> now = Systems.TycoonCards.All(KitchenLib.Utils.EntityUtils.GetEntityManager());
            if (now.Count != state.Cards.Count) return false;
            for (int i = 0; i < now.Count; i++)
                if (now[i].UnlockID != state.Cards[i].UnlockID) return false;
            return true;
        }

        private static void RefreshStates(State state)
        {
            List<CTycoonCard> now = Systems.TycoonCards.All(KitchenLib.Utils.EntityUtils.GetEntityManager());
            for (int i = 0; i < now.Count && i < state.Cards.Count; i++) state.Cards[i] = now[i];
        }

        private static Color ColourFor(State state, CTycoonCard card)
        {
            if (card.Vanilla) return LockedColour;
            if (!card.Enabled) return DisabledColour;
            return state.DefaultColours.TryGetValue(card.UnlockID, out Color c) ? c : Color.white;
        }

        private static HashSet<int> CardDishes(List<CTycoonCard> cards)
        {
            var set = new HashSet<int>();
            foreach (CTycoonCard c in cards) if (Systems.TycoonCards.IsRecipe(c.UnlockID)) set.Add(c.UnlockID);
            return set;
        }

        private static string CardName(int unlockID) =>
            GameData.Main.TryGet<Dish>(unlockID, out Dish _, false) ? TycoonRecipeShop.NameOf(unlockID)
            : GameData.Main.TryGet<Unlock>(unlockID, out Unlock unlock) && !string.IsNullOrEmpty(unlock.Name) ? unlock.Name : unlockID.ToString();

        private static readonly int TitleColour = Shader.PropertyToID("_Title");

        private static Color GetColour(UnlockCardElement element) =>
            (object)element.Card != null ? element.Card.material.GetColor(TitleColour) : Color.white;

        private static void SetColour(UnlockCardElement element, Color colour)
        {
            if ((object)element.Card != null) element.Card.material.SetColor(TitleColour, colour);
        }

        private static void SetTitle(DishSelectionIndicator indicator, string text)
        {
            object title = AccessTools.Field(typeof(DishSelectionIndicator), "Title").GetValue(indicator);
            title?.GetType().GetProperty("text")?.SetValue(title, text);
        }
    }

    /// <summary>Card manager: after the menu has handled its input, update the fan.</summary>
    [HarmonyPatch(typeof(DishSelectionIndicator), "TakeInput")]
    public static class ManagementCardsSyncPatch
    {
        [HarmonyPostfix]
        private static void Postfix(DishSelectionIndicator __instance)
        {
            if (!ManagementCardsView.IsOpen(__instance)) return;
            try { ManagementCardsView.Sync(__instance); }
            catch (System.Exception e) { Mod.LogError("Card manager: update failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(DishSelectionIndicator), "OnDestroy")]
    public static class ManagementCardsForgetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(DishSelectionIndicator __instance) => ManagementCardsView.Forget(__instance);
    }
}
