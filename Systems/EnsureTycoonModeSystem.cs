using Kitchen;
using KitchenMods;
using PlateUpTycoon.Components;
using Unity.Entities;

namespace PlateUpTycoon.Systems
{
    /// <summary>Keeps the STycoonMode singleton in step with TycoonSession.IsActive.</summary>
    public class EnsureTycoonModeSystem : GenericSystemBase, IModSystem
    {
        protected override void Initialise()
        {
            base.Initialise();
        }

        protected override void OnUpdate()
        {
            bool hasSingleton = HasSingleton<STycoonMode>();

            if (TycoonSession.IsActive && !hasSingleton)
            {
                Set(new STycoonMode());
                Mod.LogInfo("Tycoon Mode active in this scene.");
            }
            else if (!TycoonSession.IsActive && hasSingleton)
            {
                EntityManager.DestroyEntity(GetSingletonEntity<STycoonMode>());
                Mod.LogWarning("Removed Tycoon Mode marker from a normal-mode game.");
            }
        }
    }
}
