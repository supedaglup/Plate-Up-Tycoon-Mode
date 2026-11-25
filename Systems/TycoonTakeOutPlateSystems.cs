using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Components
{
    /// <summary>Tag on an item that is on a Take-Out Plate: an empty plate from the Take-Out Plates rack.</summary>
    public struct CTycoonTakeOutPlate : IComponentData, IModComponent
    {
    }

    /// <summary>Appliance property: the Take-Out Plates rack.</summary>
    public struct CTycoonTakeOutPlateRack : IApplianceProperty, IAttachableProperty, IComponentData, IModComponent
    {
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Take-Out Plates: tracks which items are on a disposable plate.</summary>
    internal static class TakeOutPlate
    {
        internal static readonly HashSet<Entity> Served = new HashSet<Entity>();

        internal static bool Marked(EntityManager em, Entity item) => item != Entity.Null && em.Exists(item) && em.HasComponent<CTycoonTakeOutPlate>(item);

        internal static bool FromRack(EntityManager em, Entity source) => source != Entity.Null && em.Exists(source) && em.HasComponent<CTycoonTakeOutPlateRack>(source);

        internal static void Mark(EntityManager em, Entity item)
        {
            if (item != Entity.Null && em.Exists(item) && !em.HasComponent<CTycoonTakeOutPlate>(item)) em.AddComponent<CTycoonTakeOutPlate>(item);
        }

        internal static bool Proposal(EntityManager em, Entity proposalEntity, out CItemTransferProposal proposal)
        {
            proposal = default;
            if (proposalEntity == Entity.Null || !em.Exists(proposalEntity) || !em.HasComponent<CItemTransferProposal>(proposalEntity)) return false;
            proposal = em.GetComponentData<CItemTransferProposal>(proposalEntity);
            return true;
        }

        internal static Entity HeldBy(EntityManager em, Entity holder) =>
            holder != Entity.Null && em.Exists(holder) && em.HasComponent<CItemHolder>(holder) ? em.GetComponentData<CItemHolder>(holder).HeldItem : Entity.Null;
    }

    [HarmonyPatch(typeof(AcceptIntoHolder), "AcceptTransfer")]
    public static class TakeOutPlateTakePatch
    {
        [HarmonyPrefix]
        private static void Prefix(AcceptIntoHolder __instance, Entity proposal_entity, out int __state)
        {
            __state = 0;
            if (!TycoonSession.IsActive) return;
            EntityManager em = __instance.EntityManager;
            if (TakeOutPlate.Proposal(em, proposal_entity, out CItemTransferProposal p))
                __state = TakeOutPlate.FromRack(em, p.Source) ? 2 : TakeOutPlate.Marked(em, p.Item) ? 1 : 0;
        }

        [HarmonyPostfix]
        private static void Postfix(AcceptIntoHolder __instance, Entity proposal_entity, int __state)
        {
            if (__state == 0) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                if (TakeOutPlate.Proposal(em, proposal_entity, out CItemTransferProposal p))
                {
                    if (__state == 2 && !TakeOutPlate.Marked(em, p.Item)) TycoonDayPhases.DisposablePlates++;
                    TakeOutPlate.Mark(em, p.Item);
                }
            }
            catch (System.Exception e) { Mod.LogWarning("Take-Out Plates (take): " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(AcceptMergeIntoHolder), "AcceptTransfer")]
    public static class TakeOutPlateMergePatch
    {
        [HarmonyPrefix]
        private static void Prefix(AcceptMergeIntoHolder __instance, Entity proposal_entity, out (bool marked, Entity held) __state)
        {
            __state = (false, Entity.Null);
            if (!TycoonSession.IsActive) return;
            EntityManager em = __instance.EntityManager;
            if (!TakeOutPlate.Proposal(em, proposal_entity, out CItemTransferProposal p)) return;
            Entity held = TakeOutPlate.HeldBy(em, p.Destination);
            bool marked = TakeOutPlate.FromRack(em, p.Source) || TakeOutPlate.Marked(em, p.Item) || TakeOutPlate.Marked(em, held);
            __state = (marked, held);
        }

        [HarmonyPostfix]
        private static void Postfix(AcceptMergeIntoHolder __instance, Entity proposal_entity, (bool marked, Entity held) __state)
        {
            if (!__state.marked) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                if (!TakeOutPlate.Proposal(em, proposal_entity, out CItemTransferProposal p)) return;
                Entity result = TakeOutPlate.HeldBy(em, p.Destination);
                if (result != __state.held)
                {
                    if (TakeOutPlate.FromRack(em, p.Source)) TycoonDayPhases.DisposablePlates++;
                    TakeOutPlate.Mark(em, result);
                }
            }
            catch (System.Exception e) { Mod.LogWarning("Take-Out Plates (merge): " + e.Message); }
        }
    }

    /// <summary>A side taken off a Take-Out Plate leaves no empty plate behind.</summary>
    [HarmonyPatch]
    public static class TakeOutPlateSplitPatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TakeFromComponentSplit), "SendTransfer");
            yield return AccessTools.Method(typeof(TakeFromLooseComponentSplit), "SendTransfer");
        }

        [HarmonyPrefix]
        private static void Prefix(ComponentSystemBase __instance, Entity transfer, out Entity __state)
        {
            __state = Entity.Null;
            if (!TycoonSession.IsActive) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                if (!TakeOutPlate.Proposal(em, transfer, out CItemTransferProposal p)) return;
                Entity held = TakeOutPlate.HeldBy(em, p.Source);
                if (!TakeOutPlate.Marked(em, held)) return;
                __state = held;
                TakeOutPlate.Mark(em, p.Item);
            }
            catch (System.Exception e) { Mod.LogWarning("Take-Out Plates (split): " + e.Message); }
        }

        [HarmonyPostfix]
        private static void Postfix(ComponentSystemBase __instance, Entity transfer, Entity __state)
        {
            if (__state == Entity.Null) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                if (!TakeOutPlate.Proposal(em, transfer, out CItemTransferProposal p)) return;
                Entity left = TakeOutPlate.HeldBy(em, p.Source);
                if (left == Entity.Null || !em.Exists(left) || !em.HasComponent<CItem>(left)) return;
                bool customer = p.Destination != Entity.Null && em.Exists(p.Destination) && em.HasComponent<COccupiedByGroup>(p.Destination);
                if (customer && em.GetComponentData<CItem>(left).ID == KitchenLib.References.ItemReferences.Plate)
                {
                    CItemHolder holder = em.GetComponentData<CItemHolder>(p.Source);
                    holder.HeldItem = Entity.Null;
                    em.SetComponentData(p.Source, holder);
                    em.DestroyEntity(left);
                    Mod.LogInfo("Take-Out Plates: a customer took the food off a Take-Out Plate; the empty plate is thrown away.");
                }
                else if (left != __state) TakeOutPlate.Mark(em, left);
            }
            catch (System.Exception e) { Mod.LogWarning("Take-Out Plates (split): " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(AcceptIntoOrderSatisfaction), "AcceptTransfer")]
    public static class TakeOutPlateServePatch
    {
        [HarmonyPrefix]
        private static void Prefix(AcceptIntoOrderSatisfaction __instance, Entity proposal_entity) => TakeOutPlateServe.Note(__instance.EntityManager, proposal_entity);
    }

    [HarmonyPatch(typeof(AcceptIntoPiecemealSatisfaction), "AcceptTransfer")]
    public static class TakeOutPlatePiecemealPatch
    {
        [HarmonyPrefix]
        private static void Prefix(AcceptIntoPiecemealSatisfaction __instance, Entity proposal_entity) => TakeOutPlateServe.Note(__instance.EntityManager, proposal_entity);
    }

    public static class TakeOutPlateServe
    {
        internal static bool Current;

        internal static void Note(EntityManager em, Entity proposalEntity)
        {
            if (!TycoonSession.IsActive) return;
            if (TakeOutPlate.Proposal(em, proposalEntity, out CItemTransferProposal p) && TakeOutPlate.Marked(em, p.Item))
            {
                TakeOutPlate.Served.Add(p.Item);
                if (TakeOutPlate.Served.Count > 500) TakeOutPlate.Served.Clear();
            }
        }
    }

    /// <summary>No dirty plate from a dish served on a Take-Out Plate.</summary>
    [HarmonyPatch(typeof(AddDirtItems), "HandleSatisfiedOrder")]
    public static class TakeOutPlateNoDirtPatch
    {
        private const int PICKY_STATUS = 0x800000;
        private const float PICKY_CHANCE = 0.5f;
        private static readonly System.Reflection.MethodInfo HasStatus = AccessTools.Method(typeof(GameSystemBase), "HasStatus");

        [HarmonyPrefix]
        private static bool Prefix(AddDirtItems __instance, CItemTransferProposal proposal, ref COrderAcceptance details)
        {
            if (!(TycoonSession.IsActive && TakeOutPlate.Served.Contains(proposal.Item))) return true;
            try
            {
                if (HasStatus == null || !(bool)HasStatus.Invoke(__instance, new object[] { (RestaurantStatus)PICKY_STATUS })) return false;
                if (UnityEngine.Random.value >= PICKY_CHANCE) return false;
                if (!GameData.Main.TryGet<Item>(details.DeliveredItem, out Item delivered, false) || delivered.DirtiesTo == null) return false;
                Item leftovers = delivered.DirtiesTo.ExtendedDirtItem;
                if (leftovers == null) return false;
                EntityManager em = __instance.EntityManager;
                if (details.TableSet == Entity.Null || !em.Exists(details.TableSet) || !em.HasComponent<CDirtItem>(details.TableSet)) return false;
                em.GetBuffer<CDirtItem>(details.TableSet).Add(new CDirtItem { ID = leftovers.ID });
                Mod.LogInfo("Take-Out Plates: Picky Eaters left food on a Disposable Plate.");
            }
            catch (System.Exception e) { Mod.LogWarning("Take-Out Plates (leftovers): " + e.Message); }
            return false;
        }
    }

    /// <summary>A Picky Eater's leftovers on a Take-Out Plate are left as one dirty disposable plate.</summary>
    [HarmonyPatch(typeof(SpawnTableDirt), "SpawnDirt")]
    public static class TakeOutPlateLeftoversPatch
    {
        private static HashSet<int> _leftoverIDs;

        private static bool IsLeftovers(int id)
        {
            if (_leftoverIDs == null)
            {
                _leftoverIDs = new HashSet<int>();
                foreach (Item item in GameData.Main.Get<Item>())
                    if (item?.ExtendedDirtItem != null) _leftoverIDs.Add(item.ExtendedDirtItem.ID);
            }
            return _leftoverIDs.Contains(id);
        }

        [HarmonyPrefix]
        private static void Prefix(DynamicBuffer<CDirtItem> dirt_items, out List<int> __state)
        {
            __state = null;
            if (!TycoonSession.IsActive || !dirt_items.IsCreated) return;
            try
            {
                for (int i = dirt_items.Length - 1; i >= 0; i--)
                {
                    if (!IsLeftovers(dirt_items[i].ID)) continue;
                    (__state ??= new List<int>()).Add(dirt_items[i].ID);
                    dirt_items.RemoveAt(i);
                }
            }
            catch (System.Exception e) { Mod.LogWarning("Take-Out Plates (leftovers): " + e.Message); }
        }

        [HarmonyPostfix]
        private static void Postfix(DynamicBuffer<CTableSetParts> parts, EntityCommandBuffer ecb, List<int> __state)
        {
            if (__state == null || !parts.IsCreated || parts.IsEmpty) return;
            try
            {
                Entity storage = parts[0];
                foreach (int id in __state)
                {
                    Entity item = ecb.CreateEntity();
                    ecb.AddComponent(item, new CCreateItem { ID = id });
                    ecb.AddComponent(item, new CStoredBy { Storage = storage });
                    ecb.AddComponent(item, new CTycoonTakeOutPlate());
                    ecb.AppendToBuffer(storage, new CItemStored { StoredItem = item });
                }
            }
            catch (System.Exception e) { Mod.LogWarning("Take-Out Plates (leftovers): " + e.Message); }
        }
    }

    /// <summary>Marks the serve being paid as a Take-Out Plate one.</summary>
    [HarmonyPatch(typeof(GrantMoneyForSatisfactions), "HandleSatisfiedOrder")]
    public static class TakeOutPlatePayPatch
    {
        [HarmonyPrefix]
        private static void Prefix(CItemTransferProposal proposal) => TakeOutPlateServe.Current = TycoonSession.IsActive && TakeOutPlate.Served.Contains(proposal.Item);

        [HarmonyPostfix]
        private static void Postfix() => TakeOutPlateServe.Current = false;
    }

    /// <summary>A dish on a Take-Out Plate goes in the bin plate and all.</summary>
    [HarmonyPatch(typeof(AcceptIntoBin), "AcceptTransfer")]
    public static class TakeOutPlateBinPatch
    {
        [HarmonyPrefix]
        private static void Prefix(AcceptIntoBin __instance, Entity proposal_entity, out bool __state)
        {
            __state = false;
            if (!TycoonSession.IsActive) return;
            EntityManager em = __instance.EntityManager;
            __state = TakeOutPlate.Proposal(em, proposal_entity, out CItemTransferProposal p) && TakeOutPlate.Marked(em, p.Item);
        }

        [HarmonyPostfix]
        private static void Postfix(AcceptIntoBin __instance, ref Entity return_item, bool __state)
        {
            if (!__state || return_item == Entity.Null) return;
            try
            {
                EntityManager em = __instance.EntityManager;
                if (em.Exists(return_item)) em.DestroyEntity(return_item);
                return_item = Entity.Null;
            }
            catch (System.Exception e) { Mod.LogWarning("Take-Out Plates (bin): " + e.Message); }
        }
    }

    /// <summary>The light-brown look on every player's screen.</summary>
    public class TycoonTakeOutPlateViewSystem : ViewSystemBase, IModSystem
    {
        public const int MARKER = -737373;
        private const float RESEND = 1f;
        private static readonly System.Type ViewDataType = AccessTools.Inner(typeof(ItemView), "ViewData");
        private static readonly System.Reflection.MethodInfo Send =
            ViewDataType == null ? null : AccessTools.Method(typeof(ViewSystemBase), "SendUpdate")?.MakeGenericMethod(ViewDataType);

        private EntityQuery _marked;
        private readonly Dictionary<Entity, float> _sentAt = new Dictionary<Entity, float>();
        private bool _failed;

        protected override void Initialise()
        {
            base.Initialise();
            _marked = GetEntityQuery(new QueryHelper().All(typeof(CTycoonTakeOutPlate), typeof(CItem), typeof(CLinkedView)).None(typeof(CItemUndergoingProcess)));
        }

        protected override void OnUpdate()
        {
            if (!TycoonSession.IsActive || _failed || Send == null) return;
            if (HasSingleton<SIsNightFirstUpdate>()) TakeOutPlate.Served.Clear();
            if (_marked.IsEmpty) return;
            float now = Time.RealTotalTime;
            try
            {
                using NativeArray<Entity> entities = _marked.ToEntityArray(Allocator.Temp);
                using NativeArray<CItem> items = _marked.ToComponentDataArray<CItem>(Allocator.Temp);
                using NativeArray<CLinkedView> views = _marked.ToComponentDataArray<CLinkedView>(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    if (_sentAt.TryGetValue(entities[i], out float at) && now - at < RESEND) continue;
                    _sentAt[entities[i]] = now;
                    var components = new List<int>();
                    if (items[i].Items.IsValid) components.AddRange(items[i].Items.AsArray());
                    components.Add(MARKER);
                    object data = System.Activator.CreateInstance(ViewDataType);
                    AccessTools.Field(ViewDataType, "ItemID").SetValue(data, items[i].ID);
                    AccessTools.Field(ViewDataType, "Components").SetValue(data, new ItemList(components));
                    AccessTools.Field(ViewDataType, "IsPartial").SetValue(data, items[i].IsPartial);
                    Send.Invoke(this, new object[] { (ViewIdentifier)views[i], data, MessageType.ViewUpdate, 0 });
                }
                if (_sentAt.Count > 300) _sentAt.Clear();
            }
            catch (System.Exception e)
            {
                _failed = true;
                Mod.LogWarning("Take-Out Plates: could not send the plate look: " + e);
            }
        }
    }
}

namespace PlateUpTycoon.Patches
{
    /// <summary>Every machine: an item view whose data carries TycoonTakeOutPlateViewSystem.MARKER is a Take-Out Plate item.</summary>
    [HarmonyPatch(typeof(ItemView), "UpdateData")]
    public static class TakeOutPlateViewPatch
    {
        private static readonly System.Type ViewDataType = AccessTools.Inner(typeof(ItemView), "ViewData");
        private static readonly HashSet<ItemView> Marked = new HashSet<ItemView>();
        private static int _logged;

        [HarmonyPrefix]
        private static void Prefix(ItemView __instance, object[] __args)
        {
            try
            {
                object data = __args[0];
                System.Reflection.FieldInfo field = AccessTools.Field(ViewDataType, "Components");
                ItemList components = (ItemList)field.GetValue(data);
                if (!components.IsValid) return;
                int[] all = components.AsArray();
                if (System.Array.IndexOf(all, Systems.TycoonTakeOutPlateViewSystem.MARKER) < 0) return;
                var kept = new List<int>();
                foreach (int id in all) if (id != Systems.TycoonTakeOutPlateViewSystem.MARKER) kept.Add(id);
                field.SetValue(data, new ItemList(kept));
                __args[0] = data;
                Marked.Add(__instance);
            }
            catch (System.Exception e) { if (_logged++ < 3) Mod.LogWarning("Take-Out Plates (view): " + e.Message); }
        }

        [HarmonyPostfix]
        private static void Postfix(ItemView __instance)
        {
            if (!Marked.Contains(__instance)) return;
            if (__instance == null) { Marked.Remove(__instance); return; }
            try
            {
                int changed = Customs.TakeOutPlateLook.Tint(__instance.gameObject, GameData.Main);
                if (_logged < 3 && changed > 0) { _logged++; Mod.LogInfo($"Take-Out Plates: tinted an item view ({changed} slot(s))."); }
            }
            catch (System.Exception e) { if (_logged++ < 3) Mod.LogWarning("Take-Out Plates (tint): " + e.Message); }
            if (Marked.Count > 300) Marked.RemoveWhere(v => v == null);
        }
    }
}
