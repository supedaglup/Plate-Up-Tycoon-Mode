using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace PlateUpTycoon.Systems
{
    /// <summary>Extras are paid as a second payment after the order.</summary>
    public class TycoonExtrasPaySystem : RestaurantSystem, IModSystem
    {
        public const float DELAY = 0.7f;
        private const float GIVE_UP = 60f;

        private class Owed { public Entity Group; public int Amount; public float Since; public float PaidAt = -1f; }
        private static readonly List<Owed> Pending = new List<Owed>();

        internal static void Queue(Entity group, int amount)
        {
            foreach (Owed o in Pending) if (o.Group == group && o.PaidAt < 0f) { o.Amount += amount; return; }
            Pending.Add(new Owed { Group = group, Amount = amount, Since = -1f });
        }

        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<STycoonMode>();
        }

        protected override void OnUpdate()
        {
            if (Pending.Count == 0) return;
            float now = Time.TotalTime;
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                Owed o = Pending[i];
                if (o.Since < 0f) o.Since = now;
                bool exists = EntityManager.Exists(o.Group);
                if (o.PaidAt < 0f && (!exists || !Has<CGroupAwaitingOrder>(o.Group) || now - o.Since > GIVE_UP)) o.PaidAt = now;
                if (o.PaidAt < 0f || now - o.PaidAt < DELAY) continue;
                Pending.RemoveAt(i);
                Pay(o, exists);
            }
        }

        private void Pay(Owed o, bool exists)
        {
            if (HasSingleton<SMoney>())
            {
                SMoney money = GetSingleton<SMoney>();
                money.Amount += o.Amount;
                Set(money);
            }
            MoneyTracker.AddEvent(new EntityContext(EntityManager), 0, o.Amount);
            Vector3 at = Vector3.zero;
            bool found = false;
            if (exists && EntityManager.HasComponent<CGroupMember>(o.Group))
            {
                DynamicBuffer<CGroupMember> members = EntityManager.GetBuffer<CGroupMember>(o.Group);
                if (members.Length > 0 && Require(members[0].Customer, out CPosition p)) { at = p.Position; found = true; }
            }
            if (!found) return;
            Entity popup = EntityManager.CreateEntity(typeof(CMoneyPopup), typeof(CPosition), typeof(CLifetime), typeof(CRequiresView));
            EntityManager.SetComponentData(popup, new CMoneyPopup { Change = o.Amount });
            EntityManager.SetComponentData(popup, new CPosition(at));
            EntityManager.SetComponentData(popup, new CLifetime(1f));
            EntityManager.SetComponentData(popup, new CRequiresView { Type = ViewType.MoneyPopup });
        }
    }
}
