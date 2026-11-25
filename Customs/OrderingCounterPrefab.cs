using HarmonyLib;
using Kitchen;
using KitchenData;
using System.Reflection;
using UnityEngine;

namespace PlateUpTycoon.Customs
{
    /// <summary>The Ordering Counter's model: the bar table with a ghost customer on the customer side.</summary>
    public static class OrderingCounterPrefab
    {
        private static GameObject _container;
        private static GameObject _prefab;

        public static GameObject Build(GameData gameData, GameObject barPrefab, Vector3 customerOffset)
        {
            if (_prefab != null) return _prefab;
            if (barPrefab == null) return null;
            try
            {
                if (!gameData.TryGet<Appliance>(KitchenLib.References.ApplianceReferences.HostStand, out Appliance hostStand) || hostStand.Prefab == null)
                {
                    Mod.LogWarning("Ordering Counter: Host Stand model not found; no customer ghost.");
                    return null;
                }
                DrawPathableGhostView source = hostStand.Prefab.GetComponentInChildren<DrawPathableGhostView>(true);
                Transform sourceCustomer = source != null ? Get<Transform>(source, "Customer") : null;
                Material pathable = source != null ? Get<Material>(source, "PathableMaterial") : null;
                if (sourceCustomer == null)
                {
                    Mod.LogWarning("Ordering Counter: the Host Stand has no ghost customer; no customer ghost.");
                    return null;
                }

                if (_container == null)
                {
                    _container = new GameObject("PlateUpTycoon Prefabs");
                    _container.SetActive(false);
                    Object.DontDestroyOnLoad(_container);
                }
                GameObject prefab = Object.Instantiate(barPrefab, _container.transform);
                prefab.name = "Tycoon Ordering Counter";

                foreach (DrawPathableGhostView old in prefab.GetComponentsInChildren<DrawPathableGhostView>(true))
                    Object.DestroyImmediate(old);

                var holder = new GameObject("Customer Spot Ghost");
                holder.transform.SetParent(prefab.transform, false);
                var spot = new GameObject("Spot").transform;
                spot.SetParent(holder.transform, false);
                spot.localPosition = customerOffset;

                GameObject customer = Object.Instantiate(sourceCustomer.gameObject, holder.transform);
                customer.name = "Ghost Customer";
                customer.transform.localPosition = customerOffset;
                customer.transform.localRotation = Quaternion.LookRotation(-customerOffset, Vector3.up);
                customer.transform.localScale = sourceCustomer.localScale;

                DrawPathableGhostView view = holder.AddComponent<DrawPathableGhostView>();
                Set(view, "Customer", customer.transform);
                Set(view, "NearPosition", spot);
                Set(view, "FarPosition", spot);
                Set(view, "PathableMaterial", pathable);
                Set(view, "NonPathableMaterial", pathable);

                _prefab = prefab;
                Mod.LogInfo($"Ordering Counter: model with a customer ghost at local {customerOffset}.");
                return _prefab;
            }
            catch (System.Exception e)
            {
                Mod.LogWarning("Ordering Counter: could not add the customer ghost, using the plain bar table model: " + e);
                return null;
            }
        }

        private static T Get<T>(object target, string field) where T : class
        {
            FieldInfo f = AccessTools.Field(target.GetType(), field);
            return f?.GetValue(target) as T;
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo f = AccessTools.Field(target.GetType(), field);
            if (f == null) throw new MissingFieldException(target.GetType().Name, field);
            f.SetValue(target, value);
        }

        private class MissingFieldException : System.Exception
        {
            public MissingFieldException(string type, string field) : base($"{type}.{field} not found") { }
        }
    }
}
