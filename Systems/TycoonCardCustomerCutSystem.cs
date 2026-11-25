using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpTycoon.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace PlateUpTycoon.Components
{
    /// <summary>A customer-numbers modifier owned by one card.</summary>
    public struct CTycoonCardCustomerCut : IComponentData, IModComponent
    {
        public int UnlockID;
    }
}

namespace PlateUpTycoon.Systems
{
    /// <summary>Customer numbers follow the cards.</summary>
    public class TycoonCardCustomerCutSystem : RestaurantSystem, IModSystem
    {
        public const float SMALL = 0.15f, LARGE = 0.3f;

        private EntityQuery _cuts;
        private EntityQuery _cards;
        private string _last = "";

        protected override void Initialise()
        {
            base.Initialise();
            _cuts = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCardCustomerCut), typeof(CCustomerSpawnModifier)));
            _cards = GetEntityQuery(new QueryHelper().All(typeof(CTycoonCard), typeof(CTycoonCardBought), typeof(CTycoonCardApplied)));
            RequireSingletonForUpdate<STycoonMode>();
        }

        public static float ChangeFor(Unlock unlock)
        {
            if (unlock is Dish) return 0f;
            if (!(unlock is Dish dish)) return 0f;
            switch (dish.Type)
            {
                case DishType.Side:
                case DishType.Starter: return -SMALL;
                case DishType.Main:
                case DishType.Base: return -LARGE;
                default: return 0f;
            }
        }

        protected override void OnUpdate()
        {
            var wanted = new Dictionary<int, float>();
            if (HasSingleton<STycoonFastCasualOn>() && Mod.FastCasualCard != null) wanted[Mod.FastCasualCard.ID] = -SMALL;
            using (NativeArray<Entity> entities = _cards.ToEntityArray(Allocator.Temp))
                for (int i = 0; i < entities.Length; i++)
                {
                    CTycoonCard card = EntityManager.GetComponentData<CTycoonCard>(entities[i]);
                    if (Mod.TakeOutCard != null && card.UnlockID == Mod.TakeOutCard.ID) continue;
                    if (Mod.FastCasualCard != null && card.UnlockID == Mod.FastCasualCard.ID) continue;
                    if (!GameData.Main.TryGet<Unlock>(card.UnlockID, out Unlock unlock, false)) continue;
                    float change = ChangeFor(unlock);
                    if (UnityEngine.Mathf.Abs(change) > 0.0001f) wanted[card.UnlockID] = change;
                }

            var have = new HashSet<int>();
            using (NativeArray<Entity> entities = _cuts.ToEntityArray(Allocator.Temp))
                for (int i = 0; i < entities.Length; i++)
                {
                    int id = EntityManager.GetComponentData<CTycoonCardCustomerCut>(entities[i]).UnlockID;
                    if (!wanted.TryGetValue(id, out float value) || have.Contains(id)) { EntityManager.DestroyEntity(entities[i]); continue; }
                    have.Add(id);
                    CCustomerSpawnModifier m = EntityManager.GetComponentData<CCustomerSpawnModifier>(entities[i]);
                    if (UnityEngine.Mathf.Abs(m.BaseCustomerMultiplier.Value - value) > 0.001f)
                        EntityManager.SetComponentData(entities[i], new CCustomerSpawnModifier { BaseCustomerMultiplier = new Factor { Value = value }, PerDayCustomerMultiplier = new Factor { Value = 0f } });
                }
            foreach (KeyValuePair<int, float> w in wanted)
            {
                if (have.Contains(w.Key)) continue;
                Entity e = EntityManager.CreateEntity(typeof(CTycoonCardCustomerCut), typeof(CCustomerSpawnModifier));
                EntityManager.SetComponentData(e, new CTycoonCardCustomerCut { UnlockID = w.Key });
                EntityManager.SetComponentData(e, new CCustomerSpawnModifier { BaseCustomerMultiplier = new Factor { Value = w.Value }, PerDayCustomerMultiplier = new Factor { Value = 0f } });
            }

            var parts = new List<string>();
            float total = 0f;
            foreach (KeyValuePair<int, float> w in wanted)
            {
                total += w.Value;
                parts.Add($"{(GameData.Main.TryGet<Unlock>(w.Key, out Unlock u, false) ? u.Name : w.Key.ToString())} {w.Value:+0%;-0%}");
            }
            string report = parts.Count == 0 ? "Customer changes from cards: none."
                : $"Customer changes from cards (from the next day): {string.Join(", ", parts)}; together {total:+0%;-0%}.";
            if (report != _last) { _last = report; Mod.LogInfo(report); }
        }
    }
}
