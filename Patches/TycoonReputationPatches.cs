using HarmonyLib;
using Kitchen;
using PlateUpTycoon.Components;
using PlateUpTycoon.Systems;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Patches
{
    /// <summary>Reputation and tips on every customer served.</summary>
    [HarmonyPatch(typeof(GrantMoneyForSatisfactions), "GrantReward")]
    public static class TycoonServeReputationPatch
    {
        private static readonly System.Random Random = new System.Random();
        public const int COFFEE_BONUS = 0;
        private static readonly Dictionary<int, bool> CoffeeFamily = new Dictionary<int, bool>();

        internal static bool IsCoffeeFamily(int dishID)
        {
            if (dishID == 0) return false;
            if (CoffeeFamily.TryGetValue(dishID, out bool known)) return known;
            bool yes = Needs(dishID, 0);
            CoffeeFamily[dishID] = yes;
            return yes;
        }

        private static bool Needs(int dishID, int depth)
        {
            if (dishID == Mod.STARTING_DISH) return true;
            if (depth > 6 || !KitchenData.GameData.Main.TryGet<KitchenData.Dish>(dishID, out KitchenData.Dish dish) || dish.Requires == null) return false;
            foreach (KitchenData.Unlock r in dish.Requires)
                if (r is KitchenData.Dish d && Needs(d.ID, depth + 1)) return true;
            return false;
        }

        public const float EXTRA_INGREDIENT_SHARE = 0.2f;
        private static int _extrasLogged;

        private static int ExtrasIn(EntityManager em, CItem ordered)
        {
            if (!ordered.Items.IsValid || ordered.Items.Count <= 1) return 0;
            var extras = new HashSet<int>();
            using (EntityQuery q = em.CreateEntityQuery(typeof(CPossibleExtra)))
            using (Unity.Collections.NativeArray<CPossibleExtra> all = q.ToComponentDataArray<CPossibleExtra>(Unity.Collections.Allocator.Temp))
                for (int i = 0; i < all.Length; i++) extras.Add(all[i].Ingredient);
            if (extras.Count == 0) return 0;
            int count = 0;
            for (int i = 0; i < ordered.Items.Count; i++) if (extras.Contains(ordered.Items[i])) count++;
            return count;
        }

        public const int EXTRA_RECIPE_BONUS = 1;

        internal static int ExtraRecipeBonus(int dishID)
        {
            if (dishID == 0 || dishID == Mod.STARTING_DISH) return 0;
            if (!KitchenData.GameData.Main.TryGet<KitchenData.Dish>(dishID, out KitchenData.Dish dish, false)) return 0;
            if (TycoonRecipeShop.IsMainDish(dish)) return 0;
            return EXTRA_RECIPE_BONUS + Mathf.Max(1, dish.RecipeDifficulty());
        }

        private static readonly System.Collections.Generic.HashSet<int> PayLogged = new System.Collections.Generic.HashSet<int>();

        private static int _varietyLogged;
        private static int _mealLogged;

        private static void LogPayOnce(int dishID, int payment, int extra)
        {
            if (!PayLogged.Add(dishID)) return;
            string name = KitchenData.GameData.Main.TryGet<KitchenData.Dish>(dishID, out KitchenData.Dish d, false) ? $"{d.Name} ({d.Type})" : dishID.ToString();
            Mod.LogInfo($"Pay: an item of {name} paid {payment} coin(s) (Tycoon extra-recipe bonus {extra}).");
        }

        [HarmonyPrefix]
        private static void Prefix(GrantMoneyForSatisfactions __instance, Entity group, out int __state)
        {
            __state = 0;
            if (!TycoonSession.IsActive) return;
            EntityManager em = __instance.EntityManager;
            if (em.Exists(group) && em.HasComponent<CGroupReward>(group)) __state = em.GetComponentData<CGroupReward>(group).Amount;
        }

        [HarmonyPostfix]
        private static void Postfix(GrantMoneyForSatisfactions __instance, EntityContext ctx, Entity group, int credit_dish, CItem ordered_item, int __state)
        {
            if (!TycoonSession.IsActive) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                if (!em.Exists(group)) return;
                using EntityQuery repQuery = em.CreateEntityQuery(typeof(STycoonReputation));
                if (repQuery.IsEmpty) return;
                Entity repEntity = repQuery.GetSingletonEntity();
                STycoonReputation rep = em.GetComponentData<STycoonReputation>(repEntity);

                float patienceLeft = 1f;
                if (em.HasComponent<CTycoonCarriedPatience>(group))
                {
                    CTycoonCarriedPatience pool = em.GetComponentData<CTycoonCarriedPatience>(group);
                    if (pool.Total > 0f) patienceLeft = Mathf.Clamp01(pool.Remaining / pool.Total);
                }
                else if (em.HasComponent<CPatience>(group))
                {
                    CPatience patience = em.GetComponentData<CPatience>(group);
                    if (patience.StartTime > 0f) patienceLeft = Mathf.Clamp01(patience.RemainingTime / patience.StartTime);
                }
                float messes;
                using (EntityQuery messQuery = em.CreateEntityQuery(typeof(CMess)))
                {
                    TycoonMesses.Count(em, messQuery, out int dirty, out int wet);
                    messes = dirty + TycoonMesses.WET_FLOOR_WEIGHT * wet;
                }

                float gain = TycoonReputation.ServeGain(patienceLeft, messes, TycoonReputation.FurnitureValue, TycoonReputation.CardBonus);
                gain += TycoonPromotions.RepPerServe;
                float luxuryPart = Systems.TycoonContractSystem.LuxuryActive ? TycoonPromotions.LUXURY_REP_PER_SERVE : 0f;
                gain += luxuryPart;
                TycoonReputation.LastCourse = TycoonReputation.NoteServe(em, group);
                float factorUsed = gain > 0f ? TycoonReputation.VolumeFactor : 1f;
                gain *= factorUsed;
                if (luxuryPart > 0f) Systems.TycoonContractSystem.EarnedRep += luxuryPart * factorUsed;
                PlateUpTycoon.Systems.TycoonRecipeMastery.Served(credit_dish);
                if (em.HasComponent<CTycoonContractGroup>(group))
                {
                    CTycoonContractGroup contract = em.GetComponentData<CTycoonContractGroup>(group);
                    contract.Served = true;
                    em.SetComponentData(group, contract);
                }
                float pricingFactor = TycoonDishPricing.ReputationFactorNow;
                if (gain > 0f && pricingFactor != 1f) { float change = gain * (pricingFactor - 1f); gain += change; TycoonReputationReport.PricingChange += change; }
                float plateLoss = 0f;
                if (Systems.TakeOutPlateServe.Current && gain > 0f) { plateLoss = gain * (TycoonReputation.TAKEOUT_PLATE_REPUTATION - 1f); gain += plateLoss; }
                TycoonReputationReport.Customers = TycoonReputation.CustomersToday;
                TycoonReputationReport.ServesPerCustomer = TycoonReputation.ServesPerCustomer;
                TycoonReputationReport.Cards = TycoonReputation.CountedCards;
                TycoonReputationReport.Serve(TycoonReputation.BASE_GAIN, TycoonReputation.PatienceModifier(patienceLeft),
                    -TycoonReputation.MessLoss(messes, TycoonEconomy.CurrentStars, TycoonReputation.BASE_GAIN + TycoonReputation.PatienceModifier(patienceLeft) + TycoonReputation.CardBonus), TycoonReputation.CardBonus,
                    TycoonPromotions.RepPerServe, luxuryPart, factorUsed, gain, plateLoss);
                TycoonReputation.Add(ref rep, gain);
                if (em.HasComponent<CTycoonContractGroup>(group)) Systems.TycoonContractSystem.EarnedRep += gain;
                rep.ServedToday++;

                if (em.HasComponent<CTycoonInspector>(group))
                {
                    using EntityQuery inspectionQuery = em.CreateEntityQuery(typeof(STycoonInspection));
                    if (!inspectionQuery.IsEmpty)
                    {
                        Entity e = inspectionQuery.GetSingletonEntity();
                        STycoonInspection inspection = em.GetComponentData<STycoonInspection>(e);
                        inspection.Served = true;
                        em.SetComponentData(e, inspection);
                        Mod.LogInfo("Health inspector: served.");
                    }
                }

                bool critic = em.HasComponent<CTycoonCritic>(group);
                if (critic)
                {
                    rep.CriticGain += gain;
                    rep.CriticResult = rep.CriticGain >= 0f ? TycoonReputation.RESULT_HAPPY : TycoonReputation.RESULT_UNHAPPY;
                    Mod.LogInfo($"Critic: served ({gain:+0.#;-0.#} reputation: {patienceLeft:P0} patience left, {messes} mess(es)); {(rep.CriticResult == TycoonReputation.RESULT_HAPPY ? "happy" : "UNHAPPY")}.");
                }

                int payment = em.HasComponent<CGroupReward>(group) ? em.GetComponentData<CGroupReward>(group).Amount - __state : 0;
                if (COFFEE_BONUS > 0 && payment > 0 && em.HasComponent<CGroupReward>(group) && IsCoffeeFamily(credit_dish))
                {
                    CGroupReward boosted = em.GetComponentData<CGroupReward>(group);
                    boosted.Amount += COFFEE_BONUS;
                    em.SetComponentData(group, boosted);
                    payment += COFFEE_BONUS;
                }
                int extraPay = ExtraRecipeBonus(credit_dish);
                if (extraPay > 0 && em.HasComponent<CGroupReward>(group))
                {
                    CGroupReward boosted = em.GetComponentData<CGroupReward>(group);
                    boosted.Amount += extraPay;
                    em.SetComponentData(group, boosted);
                    payment += extraPay;
                }
                int starPay = payment > 0 ? TycoonEconomy.PAY_PER_STAR * TycoonEconomy.CurrentStars : 0;
                if (starPay > 0 && em.HasComponent<CGroupReward>(group))
                {
                    CGroupReward boosted = em.GetComponentData<CGroupReward>(group);
                    boosted.Amount += starPay;
                    em.SetComponentData(group, boosted);
                    payment += starPay;
                }
                int extras = ExtrasIn(em, ordered_item);
                int extrasPay = extras > 0 && payment > 0 ? extras * Mathf.Max(1, Mathf.RoundToInt(payment * EXTRA_INGREDIENT_SHARE)) : 0;
                if (extrasPay > 0)
                {
                    Systems.TycoonExtrasPaySystem.Queue(group, extrasPay);
                    if (_extrasLogged++ < 5) Mod.LogInfo($"Pay: {extras} extra(s) in the order will pay {extrasPay} more after the order.");
                }
                float priceBonus = TycoonPromotions.PriceBonus + (TycoonUnlocks.FiveStarPricesOn ? TycoonUnlocks.FIVE_STAR_PRICE_BONUS : 0)
                    + TycoonDishPricing.Percent
                    + TycoonReputation.VarietyBonus * 100f
                    + (Systems.TycoonContractSystem.LuxuryActive ? TycoonPromotions.LUXURY_PAY_BONUS : 0);
                int pricePay = payment > 0 && priceBonus != 0f ? Mathf.RoundToInt(payment * priceBonus / 100f) : 0;
                if (Systems.TycoonContractSystem.LuxuryActive && payment > 0 && em.HasComponent<CGroupReward>(group))
                    Systems.TycoonContractSystem.EarnedCoins += Mathf.RoundToInt(payment * TycoonPromotions.LUXURY_PAY_BONUS / 100f);
                if (_varietyLogged != TycoonReputation.MenuDishes && TycoonReputation.MenuDishes > 1)
                {
                    _varietyLogged = TycoonReputation.MenuDishes;
                    Mod.LogInfo($"Pay: {TycoonKitchenLoad.Text(TycoonKitchenLoad.Load)} ({TycoonReputation.MenuDishes} dishes on the menu).");
                }
                if (pricePay != 0 && em.HasComponent<CGroupReward>(group))
                {
                    CGroupReward boosted = em.GetComponentData<CGroupReward>(group);
                    boosted.Amount = Mathf.Max(0, boosted.Amount + pricePay);
                    em.SetComponentData(group, boosted);
                    payment = Mathf.Max(0, payment + pricePay);
                }
                int course = TycoonReputation.LastCourse;
                int mealPay = payment > 0 && course > 0 ? Mathf.RoundToInt(payment * TycoonReputation.FULL_MEAL_BONUS * course) : 0;
                if (mealPay > 0 && em.HasComponent<CGroupReward>(group))
                {
                    CGroupReward boosted = em.GetComponentData<CGroupReward>(group);
                    boosted.Amount += mealPay;
                    em.SetComponentData(group, boosted);
                    payment += mealPay;
                    if (_mealLogged++ < 5) Mod.LogInfo($"Pay: Full Meal bonus +{mealPay} (course {course + 1}).");
                }
                LogPayOnce(credit_dish, payment, extraPay);
                if (em.HasComponent<CTycoonContractGroup>(group)) Systems.TycoonContractSystem.EarnedCoins += Mathf.Max(0, payment) + Mathf.Max(0, extrasPay);
                bool fastCasual = em.HasComponent<CTycoonParty>(group) && em.GetComponentData<CTycoonParty>(group).FastCasual;
                if (fastCasual)
                {
                    CTycoonParty party = em.GetComponentData<CTycoonParty>(group);
                    party.Paid += Mathf.Max(0, payment);
                    em.SetComponentData(group, party);
                }
                int tip = fastCasual ? 0 : TycoonReputation.Tip(payment, TycoonReputation.Level(rep.Reputation, TycoonEconomy.CurrentStars), Random);
                tip = Mathf.RoundToInt(tip * TycoonIngredientQuality.TipMultiplier(TycoonEconomy.CurrentStars));
                if (tip > 0)
                {
                    CGroupReward reward = em.GetComponentData<CGroupReward>(group);
                    reward.Amount += tip;
                    em.SetComponentData(group, reward);
                    CommitCompletedGroups.AddEvent(ctx, group, Mod.Ledger.Tips, tip);
                    rep.TipsToday += tip;
                }

                em.SetComponentData(repEntity, rep);
                TycoonReputation.Current = rep;
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Reputation: could not score a serve: " + e.Message);
            }
        }
    }

    /// <summary>Tags the food critic and health inspector groups when they spawn.</summary>
    [HarmonyPatch(typeof(CreateCustomerGroup), "NewGroup")]
    public static class TycoonCriticSpawnPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CreateCustomerGroup __instance, bool is_cat, Entity __result)
        {
            if (!TycoonSession.IsActive || !is_cat) return;
            EntityManager em = __instance.EntityManager;
            if (!em.Exists(__result)) return;
            if (TycoonHealthInspectorSystem.InspectorGroupPending)
            {
                TycoonHealthInspectorSystem.InspectorGroupPending = false;
                if (TycoonHealthInspectorSystem.InspectorGroupExists(em)) return;
                em.AddComponent<CTycoonInspector>(__result);
                SpecialCats.Dress(em, __result, SpecialCats.INSPECTOR_HATS, TycoonHealthInspectorSystem.TourEndPosition);
                Mod.LogInfo("Health inspector: now a customer in the queue.");
                return;
            }
            if (TycoonReputationSystem.CriticGroup != Entity.Null) return;
            if (TycoonReputation.Current.CriticToday == TycoonReputation.CRITIC_NONE) return;
            em.AddComponent<CTycoonCritic>(__result);
            SpecialCats.Dress(em, __result, SpecialCats.CRITIC_HATS, null);
            TycoonReputationSystem.CriticGroup = __result;
            TycoonReputationSystem.CriticArrivedPending = true;
        }
    }

    /// <summary>The special cats' hats, by name.</summary>
    public static class SpecialCats
    {
        public static readonly string[] BANKER_HATS = { "Crown Hat", "Crown" };
        public static readonly string[] INSPECTOR_HATS = { "Glasses Hat", "Glasses" };
        public static readonly string[] CRITIC_HATS = { "Pirate Hat", "Cowboy Hat" };
        public static readonly string[] CHEF_HATS = { "Tall Chef Hat", "Chef" };
        public static readonly string[] DEALER_HATS = { "Visor Hat", "Baseball Cap", "Visor" };
        public static readonly string[] WHISKERS_HATS = { "Beanie Pink", "Cat Beanie", "Bunny Ear Hat" };
        public const float BANKER_SCALE = 1.003f, INSPECTOR_SCALE = 1.006f, CRITIC_SCALE = 1.009f, CHEF_SCALE = 1.012f, DEALER_SCALE = 1.015f, WHISKERS_SCALE = 1.018f;

        public static string[] HatsForScale(float scale)
        {
            if (Mathf.Abs(scale - BANKER_SCALE) < 0.001f) return BANKER_HATS;
            if (Mathf.Abs(scale - INSPECTOR_SCALE) < 0.001f) return INSPECTOR_HATS;
            if (Mathf.Abs(scale - CRITIC_SCALE) < 0.001f) return CRITIC_HATS;
            if (Mathf.Abs(scale - CHEF_SCALE) < 0.001f) return CHEF_HATS;
            if (Mathf.Abs(scale - DEALER_SCALE) < 0.001f) return DEALER_HATS;
            if (Mathf.Abs(scale - WHISKERS_SCALE) < 0.001f) return WHISKERS_HATS;
            return null;
        }

        public static float ScaleFor(string[] hats) =>
            hats == BANKER_HATS ? BANKER_SCALE : hats == INSPECTOR_HATS ? INSPECTOR_SCALE : hats == CHEF_HATS ? CHEF_SCALE : hats == DEALER_HATS ? DEALER_SCALE : hats == WHISKERS_HATS ? WHISKERS_SCALE : CRITIC_SCALE;

        public static CPlayerCosmetics Outfit(string[] hats)
        {
            var cosmetics = new CPlayerCosmetics { Cosmetics = new KitchenData.DataObjectList() };
            int hat = TycoonBankSetupSystem.PickHat(hats);
            if (hat != 0) cosmetics.Set(KitchenData.CosmeticType.Hat, hat);
            return cosmetics;
        }

        public static void Mark(EntityManager em, Entity cat, string[] hats)
        {
            if (em.HasComponent<CCustomer>(cat))
            {
                CCustomer customer = em.GetComponentData<CCustomer>(cat);
                customer.Scale = ScaleFor(hats);
                em.SetComponentData(cat, customer);
            }
            if (em.HasComponent<CPlayerCosmetics>(cat)) em.SetComponentData(cat, Outfit(hats));
        }

        public static void Dress(EntityManager em, Entity group, string[] hats, Vector3? moveTo)
        {
            if (!em.HasComponent<CGroupMember>(group)) return;
            DynamicBuffer<CGroupMember> members = em.GetBuffer<CGroupMember>(group);
            var list = new List<Entity>();
            for (int i = 0; i < members.Length; i++) list.Add(members[i]);
            foreach (Entity member in list)
            {
                if (moveTo.HasValue && em.HasComponent<CPosition>(member)) em.SetComponentData(member, new CPosition(moveTo.Value));
                Mark(em, member, hats);
            }
        }
    }

    /// <summary>Special cats wear their own hat whatever the event.</summary>
    [HarmonyPatch(typeof(CustomerView), "UpdateData")]
    public static class SpecialCatHatPatch
    {
        private const string HAT_OBJECT = "TycoonSpecialHat";
        private const float FALLBACK_HAT_HEIGHT = 0.9f;
        private static readonly Dictionary<int, int> Wearing = new Dictionary<int, int>();
        private static readonly HashSet<int> LoggedHats = new HashSet<int>();

        [HarmonyPostfix]
        private static void Postfix(CustomerView __instance, CustomerView.ViewData view_data)
        {
            try
            {
                int view = __instance.GetInstanceID();
                string[] hats = SpecialCats.HatsForScale(view_data.Scale);
                if (hats == null)
                {
                    if (Wearing.Remove(view)) TakeOff(__instance);
                    return;
                }
                int hatId = TycoonBankSetupSystem.PickHat(hats);
                if (Wearing.TryGetValue(view, out int worn) && worn == hatId) return;
                TakeOff(__instance);
                Wearing[view] = hatId;
                PutOn(__instance, hatId, hats);
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Special cat hat: " + e.Message);
            }
        }

        private static void PutOn(CustomerView cat, int hatId, string[] hats)
        {
            SeasonalContentToggle[] toggles = cat.GetComponentsInChildren<SeasonalContentToggle>(true);
            foreach (SeasonalContentToggle toggle in toggles)
                if (toggle.Target != null) toggle.Target.SetActive(false);

            KitchenData.PlayerCosmetic hat = null;
            if (hatId == 0 || !KitchenData.GameData.Main.TryGet(hatId, out hat, false) || hat == null || hat.Visual == null)
            {
                Mod.LogWarning($"Special cat hat: no model for hat {hatId}.");
                return;
            }

            Transform head = FindHead(cat.transform);
            SkinnedMeshRenderer body = null;
            foreach (SkinnedMeshRenderer r in cat.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name != HAT_OBJECT) { body = r; break; }
            Bounds catBounds = body != null ? body.bounds : default;
            float catTop = catBounds.size.y > 0.1f ? catBounds.max.y : cat.transform.position.y + FALLBACK_HAT_HEIGHT + 0.15f;
            int layer = body != null ? body.gameObject.layer : cat.gameObject.layer;

            var container = new GameObject(HAT_OBJECT);
            container.layer = layer;
            GameObject source = Object.Instantiate(hat.Visual);
            source.SetActive(false);
            int pieces = 0;
            var names = new List<string>();
            PlayerOutfitComponent outfit = source.GetComponentInChildren<PlayerOutfitComponent>(true);
            var parts = new List<Renderer>();
            if (outfit != null)
            {
                if (outfit.AttachToHeadBone != null) parts.AddRange(outfit.AttachToHeadBone.GetComponentsInChildren<Renderer>(true));
                if (outfit.Renderers != null) foreach (SkinnedMeshRenderer r in outfit.Renderers) if (r != null) parts.Add(r);
                if (outfit.Hats != null) foreach (var h in outfit.Hats) if (h != null) parts.AddRange(h.gameObject.GetComponentsInChildren<Renderer>(true));
            }
            if (parts.Count == 0) parts.AddRange(source.GetComponentsInChildren<Renderer>(true));
            var done = new HashSet<Renderer>();
            foreach (Renderer part in parts)
            {
                if (part == null || !done.Add(part) || (outfit != null && part == outfit.Source)) continue;
                Mesh mesh = part is SkinnedMeshRenderer s ? s.sharedMesh : part.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                if (mesh.bounds.size.y * part.transform.lossyScale.y > BODY_HEIGHT * source.transform.lossyScale.y) { names.Add(part.name + " (body-sized, skipped)"); continue; }
                AddPiece(container, source.transform, part.transform, mesh, part.sharedMaterials, layer);
                names.Add(part.name);
                pieces++;
            }
            Object.Destroy(source);

            bool onFace = hats == SpecialCats.INSPECTOR_HATS;
            Bounds shape = default;
            bool first = true;
            foreach (Transform piece in container.transform)
            {
                Bounds b = piece.GetComponent<MeshFilter>().sharedMesh.bounds;
                foreach (Vector3 corner in Corners(b))
                {
                    Vector3 p = piece.localPosition + piece.localRotation * Vector3.Scale(corner, piece.localScale);
                    if (first) { shape = new Bounds(p, Vector3.zero); first = false; } else shape.Encapsulate(p);
                }
            }
            Vector3 pivot = onFace ? shape.center : new Vector3(shape.center.x, shape.min.y, shape.center.z);
            foreach (Transform piece in container.transform) piece.localPosition -= pivot;

            Transform anchor = head != null ? head : cat.transform;
            Vector3 headAt = anchor.position;
            Vector3 at = onFace
                ? new Vector3(headAt.x, Mathf.Lerp(headAt.y, catTop, FACE_HEIGHT), headAt.z) + cat.transform.forward * FACE_FORWARD
                : new Vector3(headAt.x, catTop - TOP_SINK, headAt.z) + cat.transform.forward * TOP_FORWARD;
            if (hats == SpecialCats.WHISKERS_HATS) at.y -= shape.size.y * cat.transform.lossyScale.y * HAT_SIZE * WRAP_SINK;
            container.transform.SetPositionAndRotation(at, cat.transform.rotation);
            container.transform.SetParent(anchor, true);
            Vector3 want = cat.transform.lossyScale * HAT_SIZE, has = anchor.lossyScale;
            container.transform.localScale = new Vector3(SafeDiv(want.x, has.x), SafeDiv(want.y, has.y), SafeDiv(want.z, has.z));

            if (LoggedHats.Add(hatId))
            {
                Vector3 headLocal = cat.transform.InverseTransformPoint(headAt);
                Mod.LogInfo($"Special cat hat: {hat.name}, {pieces} piece(s) [{string.Join(", ", names)}], shape {shape.size.ToString("F3")}; on {(head != null ? "bone '" + head.name + "'" : "the model")} "
                    + $"(head at {headLocal.ToString("F3")} from the cat, bone scale {anchor.lossyScale.ToString("F3")}), cat top {catTop - cat.transform.position.y:F3}, "
                    + $"cat bounds {catBounds.size.ToString("F3")}; season {Seasons.GetSeason()}, {toggles.Length} seasonal part(s) switched off.");
            }
        }

        private const float FACE_HEIGHT = 0.45f, FACE_FORWARD = 0.22f;
        private const float TOP_SINK = 0.12f, TOP_FORWARD = 0.05f;
        private const float WRAP_SINK = 0.6f;
        private const float HAT_SIZE = 1f;
        private const float BODY_HEIGHT = 0.7f;

        private static float SafeDiv(float a, float b) => Mathf.Abs(b) < 1e-5f ? a : a / b;

        private static Vector3[] Corners(Bounds b) => new[]
        {
            new Vector3(b.min.x, b.min.y, b.min.z), new Vector3(b.max.x, b.min.y, b.min.z), new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.min.x, b.min.y, b.max.z),
            new Vector3(b.max.x, b.max.y, b.min.z), new Vector3(b.max.x, b.min.y, b.max.z), new Vector3(b.min.x, b.max.y, b.max.z), new Vector3(b.max.x, b.max.y, b.max.z),
        };

        private static void AddPiece(GameObject container, Transform root, Transform from, Mesh mesh, Material[] materials, int layer)
        {
            var piece = new GameObject("piece");
            piece.layer = layer;
            piece.transform.SetParent(container.transform, false);
            piece.transform.localPosition = root.InverseTransformPoint(from.position);
            piece.transform.localRotation = Quaternion.Inverse(root.rotation) * from.rotation;
            Vector3 rs = root.lossyScale, fs = from.lossyScale;
            piece.transform.localScale = new Vector3(SafeDiv(fs.x, rs.x), SafeDiv(fs.y, rs.y), SafeDiv(fs.z, rs.z));
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        private static void TakeOff(CustomerView cat)
        {
            foreach (Transform t in cat.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == HAT_OBJECT) Object.Destroy(t.gameObject);
            KitchenData.Season season = Seasons.GetSeason();
            foreach (SeasonalContentToggle toggle in cat.GetComponentsInChildren<SeasonalContentToggle>(true))
                if (toggle.Target != null) toggle.Target.SetActive(toggle.SeasonActive == season);
        }

        private static Transform FindHead(Transform root)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                Transform t = queue.Dequeue();
                if (t != root && t.name != HAT_OBJECT && t.name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0) return t;
                foreach (Transform child in t) queue.Enqueue(child);
            }
            return null;
        }
    }

    /// <summary>"Reputation: X" on every player's screen, on its own line under the "N Stars" label.</summary>
    public static class TycoonReputationBar
    {
        private const int CODE_BASE = -1200000000;
        private const int CODE_SPAN = 100000000;
        private const int HUNDREDTHS_OFFSET = 5000000;

        internal static int Stars { get; private set; } = TycoonEconomy.MIN_STARS;
        internal static float Reputation { get; private set; }
        internal static bool Seen { get; private set; }

        private static DayDisplayView _view;
        private static Component _label;
        private static Component _neededLabel;
        private static Component _perCustomerLabel;
        private static string _perCustomerText;
        private static bool _failed;

        private const int NEEDED_BASE = -1300000000;
        private const int NEEDED_SPAN = 10000000;
        private const int NEEDED_PER_SLOT = 100000;
        private const int NEEDED_RECIPE_INDEX = NEEDED_PER_SLOT / 2 - 1;
        private static readonly int[] NeededIndex = new int[Systems.TycoonCardEffectsSystem.SLOTS];
        private static readonly bool[] NeededOnItsWay = new bool[Systems.TycoonCardEffectsSystem.SLOTS];
        private static bool _neededMore;
        private static List<int> _applianceIDs;

        private static List<int> ApplianceIDs
        {
            get
            {
                if (_applianceIDs != null) return _applianceIDs;
                _applianceIDs = new List<int>();
                foreach (KitchenData.Appliance a in KitchenData.GameData.Main.Get<KitchenData.Appliance>()) _applianceIDs.Add(a.ID);
                _applianceIDs.Sort();
                return _applianceIDs;
            }
        }

        public static int EncodeNeeded(int slot, int applianceID, bool more)
        {
            int index = applianceID == 0 ? 0
                : applianceID == Systems.TycoonCardEffectsSystem.NEEDED_RECIPE ? NEEDED_RECIPE_INDEX
                : ApplianceIDs.BinarySearch(applianceID) + 1;
            if (index < 0 || index >= NEEDED_PER_SLOT / 2) index = 0;
            if (more) index += NEEDED_PER_SLOT / 2;
            return NEEDED_BASE - (slot * NEEDED_PER_SLOT + index);
        }

        internal static bool TryApplyNeeded(int process, float progress)
        {
            if (process > NEEDED_BASE || process <= NEEDED_BASE - NEEDED_SPAN) return false;
            int value = NEEDED_BASE - process;
            int slot = value / NEEDED_PER_SLOT, index = value % NEEDED_PER_SLOT;
            if (slot < 0 || slot >= NeededIndex.Length) return true;
            if (slot == NeededIndex.Length - 1) _neededMore = index >= NEEDED_PER_SLOT / 2;
            NeededIndex[slot] = index % (NEEDED_PER_SLOT / 2);
            NeededOnItsWay[slot] = progress > 0.5f;
            Refresh();
            return true;
        }

        private static string NeededText()
        {
            var names = new List<string>();
            for (int slot = 0; slot < NeededIndex.Length; slot++)
            {
                int index = NeededIndex[slot];
                if (index == NEEDED_RECIPE_INDEX) { names.Add(Systems.TycoonCardEffectsSystem.NeededName(Systems.TycoonCardEffectsSystem.NEEDED_RECIPE)); continue; }
                if (index <= 0 || index > ApplianceIDs.Count) continue;
                int id = ApplianceIDs[index - 1];
                string name = KitchenData.GameData.Main.TryGet<KitchenData.Appliance>(id, out KitchenData.Appliance a) && !string.IsNullOrEmpty(a.Name) ? a.Name : id.ToString();
                names.Add(NeededOnItsWay[slot] ? name + " (on its way)" : name);
            }
            if (names.Count == 0) return "";
            return "Needed Items: " + string.Join(", ", names) + (_neededMore ? ", ..." : "");
        }

        public static int Encode(int stars, float reputation)
        {
            int hundredths = Mathf.Clamp(Mathf.RoundToInt(reputation * 100f), -HUNDREDTHS_OFFSET + 1, HUNDREDTHS_OFFSET - 1) + HUNDREDTHS_OFFSET;
            return CODE_BASE - (hundredths * 10 + TycoonEconomy.ClampStars(stars));
        }

        internal static bool TryApply(int process, float progress)
        {
            if (process > CODE_BASE || process <= CODE_BASE - CODE_SPAN) return false;
            int value = CODE_BASE - process;
            Stars = value % 10;
            float before = Reputation;
            bool first = !Seen;
            Reputation = (value / 10 - HUNDREDTHS_OFFSET) / 100f;
            Seen = true;
            if (first) _shown = Reputation;
            else if (Mathf.Abs(Reputation - before) >= 0.005f) SpawnChange(Reputation - before);
            Refresh();
            return true;
        }

        internal static void Remember(DayDisplayView view)
        {
            if (_view != view)
            {
                _view = view; _label = null; _neededLabel = null; _perCustomerLabel = null; _perCustomerText = null;
                foreach (Change c in Changes) if ((object)c.Text != null) Object.Destroy(c.Text.gameObject);
                Changes.Clear();
                _shown = Reputation;
            }
            Refresh();
        }

        public const float CHANGE_SECONDS = 1.2f;
        public const float COUNT_RATE = 5f, COUNT_MIN_SPEED = 4f;
        private static readonly Color GAIN = new Color(0.35f, 0.9f, 0.35f), LOSS = new Color(0.95f, 0.3f, 0.25f);

        private class Change
        {
            public Component Text;
            public float Age;
            public Vector3 From, To;
            public Color Colour;
        }

        private static readonly List<Change> Changes = new List<Change>();
        private static float _shown;
        private static float _lineHeight = 0.3f;
        private static string _shownText;

        private static void SpawnChange(float delta)
        {
            if (_label == null) return;
            try
            {
                GameObject copy = Object.Instantiate(_label.gameObject, _label.transform.parent, false);
                copy.name = "Tycoon reputation change";
                Component text = copy.GetComponent(_label.GetType());
                Color colour = delta >= 0f ? GAIN : LOSS;
                SetText(text, (delta >= 0f ? "+" : "") + delta.ToString("0.00"));
                SetColour(text, colour);
                Vector3 to = _label.transform.localPosition;
                var change = new Change { Text = text, From = to - new Vector3(0f, _lineHeight, 0f), To = to, Colour = colour };
                copy.transform.localPosition = change.From;
                Changes.Add(change);
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Reputation label: could not show the change: " + e.Message);
            }
        }

        internal static void Animate(float dt)
        {
            if (_label == null || _failed) return;
            try
            {
                UpdatePerCustomer();
                float gap = Reputation - _shown;
                if (Mathf.Abs(gap) > 0.001f)
                {
                    float step = Mathf.Max(Mathf.Abs(gap) * COUNT_RATE, COUNT_MIN_SPEED) * dt;
                    _shown = Mathf.Abs(gap) <= step ? Reputation : _shown + Mathf.Sign(gap) * step;
                    UpdateNumber();
                }
                for (int i = Changes.Count - 1; i >= 0; i--)
                {
                    Change c = Changes[i];
                    if ((object)c.Text == null) { Changes.RemoveAt(i); continue; }
                    c.Age += dt;
                    float t = Mathf.Clamp01(c.Age / CHANGE_SECONDS);
                    c.Text.transform.localPosition = Vector3.Lerp(c.From, c.To, t * t * (3f - 2f * t));
                    Color colour = c.Colour;
                    colour.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                    SetColour(c.Text, colour);
                    if (t >= 1f) { Object.Destroy(c.Text.gameObject); Changes.RemoveAt(i); }
                }
            }
            catch (System.Exception e)
            {
                _failed = true;
                Mod.LogWarning("Reputation label animation: " + e.Message);
            }
        }

        private static void UpdateNumber()
        {
            string text = $"Reputation: {_shown:0.00}";
            if (text == _shownText) return;
            _shownText = text;
            SetText(_label, text);
        }

        private static void UpdatePerCustomer()
        {
            if (_perCustomerLabel == null) return;
            string text = TycoonDishPricing.TryPerCustomer(out float v) ? $"{(v >= 0f ? "+" : "")}{v:0.0} Reputation per Customer" : "";
            if (text == _perCustomerText) return;
            _perCustomerText = text;
            SetText(_perCustomerLabel, text);
        }

        private static void SetText(Component c, string text) => c.GetType().GetProperty("text")?.SetValue(c, text);
        private static void SetColour(Component c, Color colour) => c.GetType().GetProperty("color")?.SetValue(c, colour);

        private static void Refresh()
        {
            if (_view == null || _failed || !Seen) return;
            try
            {
                if (!TycoonDays.Active && AccessTools.Field(typeof(DayDisplayView), "Tier")?.GetValue(_view) is Component tierText)
                {
                    string stars = TycoonReputation.StarsText(Stars);
                    if (tierText.GetType().GetProperty("text")?.GetValue(tierText) as string != stars) SetText(tierText, stars);
                }
                if (_label == null) { _label = MakeLabel(1, "reputation"); _shownText = null; }
                if (_label == null) return;
                UpdateNumber();
                if (_perCustomerLabel == null) { _perCustomerLabel = MakeLabel(2, "reputation per customer"); _perCustomerText = null; }
                UpdatePerCustomer();
                if (_neededLabel == null) _neededLabel = MakeLabel(3, "needed items");
                _neededLabel?.GetType().GetProperty("text")?.SetValue(_neededLabel, NeededText());
            }
            catch (System.Exception e)
            {
                _failed = true;
                Mod.LogWarning("Reputation label: " + e);
            }
        }

        private static Component MakeLabel(int line, string what)
        {
            if (!(AccessTools.Field(typeof(DayDisplayView), "Tier")?.GetValue(_view) is Component tier)) return null;
            GameObject copy = Object.Instantiate(tier.gameObject, tier.transform.parent, false);
            copy.name = "Tycoon " + what + " label";
            Component text = copy.GetComponent(tier.GetType());
            text.GetType().GetProperty("enableWordWrapping")?.SetValue(text, false);
            float height = tier.GetType().GetProperty("preferredHeight")?.GetValue(tier) is float h && h > 0f ? h * tier.transform.localScale.y : 0.3f;
            _lineHeight = height;
            copy.transform.localPosition = tier.transform.localPosition - new Vector3(0f, height * line, 0f);
            Mod.LogInfo($"{what} label: {line} line(s) under the stars label at {copy.transform.localPosition} (line height {height:0.###}).");
            return text;
        }
    }
}
