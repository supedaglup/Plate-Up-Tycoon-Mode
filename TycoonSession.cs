using System;
using System.Threading.Tasks;

namespace PlateUpTycoon
{
    /// <summary>Host-side switch for Tycoon Mode.</summary>
    public static class TycoonSession
    {
        public static bool IsActive { get; private set; }

        public static bool OpenedFromTycoonMenu { get; set; }

        public static bool DebugAutoStartUsed { get; set; }

        public static async Task<bool> Begin()
        {
            DebugAutoStartUsed = false;
            if (IsActive)
            {
                OpenedFromTycoonMenu = true;
                return true;
            }

            try
            {
                await TycoonSaveData.Activate();
            }
            catch (Exception e)
            {
                Mod.LogError("Could not switch to the Tycoon save data; Tycoon Mode was NOT started. " + e);
                TycoonSaveData.Deactivate();
                return false;
            }
            IsActive = true;
            OpenedFromTycoonMenu = true;
            Mod.LogInfo($"Tycoon session started (Tycoon saves active: {TycoonSaveData.IsActive}).");
            return true;
        }

        public static void End()
        {
            OpenedFromTycoonMenu = false;
            if (!IsActive) return;
            TycoonSaveData.Deactivate();
            IsActive = false;
            Mod.LogInfo("Tycoon session ended; vanilla saves restored.");
        }
    }
}
