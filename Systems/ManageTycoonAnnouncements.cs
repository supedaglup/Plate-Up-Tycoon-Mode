using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Shows CTycoonAnnouncement messages to every player as a floating label.</summary>
    public class ManageTycoonAnnouncements : IndicatorManager, IModSystem
    {
        public const string MARKER = "#T";

        protected override ViewType ViewType => ViewType.GenericIndicator;

        public static FixedString64 Encode(string text)
        {
            string full = MARKER + text;
            if (full.Length > 60) full = full.Substring(0, 60);
            return new FixedString64(full);
        }

        public const float SECONDS = 5f;

        public static Entity Announce(EntityManager em, float now, UnityEngine.Vector3 position, string text, float seconds = SECONDS)
        {
            UnityEngine.Vector3 at = position + new UnityEngine.Vector3(0f, 1.5f, 0f);
            at.y += LINE_HEIGHT * Stacked(em, now, at);
            Entity e = em.CreateEntity(typeof(CTycoonAnnouncement), typeof(CPosition), typeof(CLocationChoice), typeof(CDoNotPersist));
            em.SetComponentData(e, new CTycoonAnnouncement { ExpiresAt = now + seconds });
            em.SetComponentData(e, new CPosition(at));
            FixedString64 message = Encode(text);
            em.SetComponentData(e, new CLocationChoice { RestaurantName = message, RestaurantSafeName = message });
            return e;
        }

        public static readonly UnityEngine.Vector3 ITEM_LABEL_OFFSET = new UnityEngine.Vector3(0f, 0.25f, -1.1f);

        public static UnityEngine.Vector3 ItemLabelAt(UnityEngine.Vector3 holder) => holder + ITEM_LABEL_OFFSET;

        public static Entity AnnounceItemLabel(EntityManager em, float now, UnityEngine.Vector3 holder, string text, float seconds)
        {
            Entity e = em.CreateEntity(typeof(CTycoonAnnouncement), typeof(CPosition), typeof(CLocationChoice), typeof(CDoNotPersist));
            em.SetComponentData(e, new CTycoonAnnouncement { ExpiresAt = now + seconds });
            em.SetComponentData(e, new CPosition(ItemLabelAt(holder)));
            FixedString64 message = Encode(text);
            em.SetComponentData(e, new CLocationChoice { RestaurantName = message, RestaurantSafeName = message });
            return e;
        }

        public const float LINE_HEIGHT = 0.8f;

        private static int Stacked(EntityManager em, float now, UnityEngine.Vector3 at)
        {
            int slot = 0;
            using EntityQuery q = em.CreateEntityQuery(typeof(CTycoonAnnouncement), typeof(CPosition));
            using NativeArray<CTycoonAnnouncement> anns = q.ToComponentDataArray<CTycoonAnnouncement>(Allocator.Temp);
            using NativeArray<CPosition> positions = q.ToComponentDataArray<CPosition>(Allocator.Temp);
            var taken = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < anns.Length; i++)
            {
                if (anns[i].ExpiresAt <= now) continue;
                UnityEngine.Vector3 p = positions[i].Position;
                if (p.y < -100f) continue;
                float dx = p.x - at.x, dz = p.z - at.z;
                if (dx * dx + dz * dz > 1f) continue;
                int s = UnityEngine.Mathf.RoundToInt((p.y - at.y) / LINE_HEIGHT);
                if (s >= 0) taken.Add(s);
            }
            while (taken.Contains(slot)) slot++;
            return slot;
        }

        protected override EntityQuery GetCandidateQuery()
        {
            return GetEntityQuery(new QueryHelper().All(typeof(CTycoonAnnouncement), typeof(CPosition)));
        }

        protected override bool ShouldHaveIndicator(Entity candidate)
        {
            return Require(candidate, out CTycoonAnnouncement a) && Time.RealTotalTime < a.ExpiresAt;
        }

        protected override Entity CreateIndicator(Entity source)
        {
            Entity indicator = base.CreateIndicator(source);
            if (Require(source, out CPosition position))
                EntityManager.AddComponentData(indicator, new CPosition(position.Position));
            EntityManager.AddComponentData(indicator, new CGenericInputIndicator
            {
                Message = InputIndicatorMessage.ReloadSave,
                CreateForPlayer = 0,
            });
            return indicator;
        }
    }

    /// <summary>Removes announcements a moment after they expire.</summary>
    public class CleanUpTycoonAnnouncements : GenericSystemBase, IModSystem
    {
        private EntityQuery _announcements;

        protected override void Initialise()
        {
            base.Initialise();
            _announcements = GetEntityQuery(new QueryHelper().All(typeof(CTycoonAnnouncement)));
            RequireForUpdate(_announcements);
        }

        protected override void OnUpdate()
        {
            using NativeArray<Entity> entities = _announcements.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                CTycoonAnnouncement a = EntityManager.GetComponentData<CTycoonAnnouncement>(entities[i]);
                if (Time.RealTotalTime > a.ExpiresAt + 1f && !Has<CHasIndicator>(entities[i]))
                    EntityManager.DestroyEntity(entities[i]);
            }
        }
    }
}
