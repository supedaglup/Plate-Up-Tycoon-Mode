using Kitchen;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Entities.Serialization;

namespace PlateUpTycoon
{
    /// <summary>Gives Tycoon Mode its own save files.</summary>
    public static class TycoonSaveData
    {
        public const string PROGRESS_FOLDER = "TycoonProgress";
        public const string FULL_FOLDER = "TycoonFull";
        public const int COMPANY_SLOTS = 3;
        public static int Slot { get; private set; } = 1;
        private static int _activeSlot;

        public static string ProgressFolder(int slot) => slot <= 1 ? PROGRESS_FOLDER : PROGRESS_FOLDER + slot;
        public static string FullFolder(int slot) => slot <= 1 ? FULL_FOLDER : FULL_FOLDER + slot;

        private const BindingFlags ANY = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static PackProgressionSaveSystem _vanillaProgress;
        private static FullWorldSaveSystem _vanillaFull;
        private static readonly System.Collections.Generic.Dictionary<int, PackProgressionSaveSystem> _progressBySlot = new System.Collections.Generic.Dictionary<int, PackProgressionSaveSystem>();
        private static readonly System.Collections.Generic.Dictionary<int, FullWorldSaveSystem> _fullBySlot = new System.Collections.Generic.Dictionary<int, FullWorldSaveSystem>();
        private static PackProgressionSaveSystem _tycoonProgress => _progressBySlot.TryGetValue(Slot, out var p) ? p : null;
        private static FullWorldSaveSystem _tycoonFull => _fullBySlot.TryGetValue(Slot, out var f) ? f : null;

        public static bool IsActive { get; private set; }

        public static void SelectCompany(int slot)
        {
            slot = Math.Max(1, Math.Min(COMPANY_SLOTS, slot));
            if (slot == Slot && (!IsActive || _activeSlot == slot)) return;
            if (TycoonSession.IsActive) TycoonSession.End();
            Deactivate();
            Slot = slot;
            TycoonCompany.SwitchTo(slot);
            Mod.LogInfo($"Company slot {slot} chosen ({ProgressFolder(slot)}, {FullFolder(slot)}, {TycoonCompany.FileName}).");
        }

        public static async Task Activate()
        {
            if (IsActive && _activeSlot == Slot) return;
            if (IsActive) Deactivate();
            await EnsureCreated();
            await RepairUnreadableSlots();

            Persistence.Progress = _tycoonProgress;
            Persistence.FullWorld = _tycoonFull;
            IsActive = true;
            _activeSlot = Slot;
        }

        private static async Task EnsureCreated()
        {
            if (_tycoonProgress == null)
            {
                if (_vanillaProgress == null)
                {
                    _vanillaProgress = Persistence.Progress;
                    _vanillaFull = Persistence.FullWorld;
                }
                if (_vanillaProgress == null || _vanillaFull == null)
                    throw new InvalidOperationException("Game persistence is not initialised yet.");

                FieldInfo progressStorageField = typeof(PackProgressionSaveSystem).GetField("SaveStorage", ANY);
                FieldInfo fullStorageField = typeof(FullWorldSaveSystem).GetField("SaveData", ANY);
                object vanillaProgressStorage = progressStorageField.GetValue(_vanillaProgress);
                Array vanillaFullStorages = (Array)fullStorageField.GetValue(_vanillaFull);

                Type progressStorageType = vanillaProgressStorage.GetType();
                Type fullStorageType = vanillaFullStorages.GetType().GetElementType();
                object platform = GetCurrentPlatform(progressStorageType);

                object progressStorage = NewStorage(progressStorageType, platform, ProgressFolder(Slot));

                Array fullStorages = Array.CreateInstance(fullStorageType, vanillaFullStorages.Length);
                for (int i = 0; i < fullStorages.Length; i++)
                {
                    string name = Path.Combine(FullFolder(Slot), (i + 1).ToString());
                    fullStorages.SetValue(NewStorage(fullStorageType, platform, name), i);
                }

                var progress = (PackProgressionSaveSystem)Activator.CreateInstance(typeof(PackProgressionSaveSystem), progressStorage);
                var full = (FullWorldSaveSystem)Activator.CreateInstance(typeof(FullWorldSaveSystem), fullStorages, progress);

                await progress.Initialise();
                await full.Initialise();
                _progressBySlot[Slot] = progress;
                _fullBySlot[Slot] = full;
                Mod.LogInfo($"Tycoon save data ready for company {Slot} ({ProgressFolder(Slot)}, {FullFolder(Slot)}/1-{fullStorages.Length}).");
            }
        }

        private static async Task RepairUnreadableSlots()
        {
            var pending = new System.Collections.Generic.List<Task>();
            try
            {
                Array storages = (Array)typeof(FullWorldSaveSystem).GetField("SaveData", ANY).GetValue(_tycoonFull);
                for (int i = 0; i < storages.Length; i++)
                {
                    object storage = storages.GetValue(i);
                    int copies = 0, readable = 0;
                    foreach (byte[] data in SavedCopies(storage))
                    {
                        copies++;
                        if (CanDeserialise(data)) readable++;
                    }
                    if (copies == 0 || readable > 0) continue;

                    Mod.LogWarning($"Tycoon save slot {i + 1} was written by an older build and cannot be loaded; deleting it so the game does not get stuck.");
                    pending.Add(Clear(storage));
                }
            }
            catch (Exception e)
            {
                Mod.LogWarning("Could not check the Tycoon save slots: " + e.Message);
            }
            await Task.WhenAll(pending);
        }

        private static IEnumerable SavedCopies(object storage)
        {
            MethodInfo get = storage.GetType().GetMethod("Get", ANY, null, Type.EmptyTypes, null);
            if (get == null) yield break;
            if (!(get.Invoke(storage, null) is IEnumerable entries)) yield break;
            foreach (object entry in entries)
            {
                if (entry == null) continue;
                object data = entry.GetType().GetField("Data", ANY)?.GetValue(entry)
                           ?? entry.GetType().GetProperty("Data", ANY)?.GetValue(entry);
                if (data is byte[] bytes && bytes.Length > 0) yield return bytes;
            }
        }

        private static bool CanDeserialise(byte[] data)
        {
            World world = null;
            try
            {
                world = new World("TycoonSaveCheck", WorldFlags.Staging);
                EntityManager em = world.EntityManager;
                ExclusiveEntityTransaction transaction = em.BeginExclusiveEntityTransaction();
                try
                {
                    using (var reader = new ArrayBinaryReader(data))
                        SerializeUtility.DeserializeWorld(transaction, reader, null);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
                finally
                {
                    em.EndExclusiveEntityTransaction();
                }
            }
            catch (Exception e)
            {
                Mod.LogWarning("Save check failed to run: " + e.Message);
                return true;
            }
            finally
            {
                world?.Dispose();
            }
        }

        private static Task Clear(object storage)
        {
            MethodInfo clear = storage.GetType().GetMethod("Clear", ANY, null, Type.EmptyTypes, null);
            if (clear == null) throw new MissingMethodException(storage.GetType().FullName, "Clear");
            return clear.Invoke(storage, null) as Task ?? Task.CompletedTask;
        }

        public static async Task<string> DeleteAll()
        {
            Trace("DeleteAll: start");
            if (TycoonSession.IsActive) TycoonSession.End();
            Deactivate();
            await EnsureCreated();
            Trace("DeleteAll: save systems ready");
            var pending = new System.Collections.Generic.List<Task>();
            Array storages = (Array)typeof(FullWorldSaveSystem).GetField("SaveData", ANY).GetValue(_tycoonFull);
            for (int i = 0; i < storages.Length; i++)
            {
                Trace($"DeleteAll: clearing slot {i + 1}");
                pending.Add(Clear(storages.GetValue(i)));
            }
            object progressStorage = typeof(PackProgressionSaveSystem).GetField("SaveStorage", ANY).GetValue(_tycoonProgress);
            Trace("DeleteAll: clearing HQ progress");
            pending.Add(Clear(progressStorage));
            await Task.WhenAll(pending);
            _progressBySlot.Remove(Slot);
            _fullBySlot.Remove(Slot);
            TycoonCompany.WipeCurrent();
            Trace("DeleteAll: finished");
            string result = $"Company {Slot} was wiped: it starts again as a brand-new company.";
            Mod.LogWarning("DEBUG: " + result);
            return result;
        }

        internal static void Trace(string step)
        {
            if (!PlateUpTycoon.TycoonPreferences.DeveloperMode) return;
            try
            {
                string path = Path.Combine(UnityEngine.Application.persistentDataPath, "TycoonDeleteTrace.txt");
                File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {Mod.BUILD} | {step}{Environment.NewLine}");
            }
            catch (Exception e)
            {
                Mod.LogWarning("Could not write the delete trace: " + e.Message);
            }
        }

        public static void Deactivate()
        {
            if (!IsActive) return;
            Persistence.Progress = _vanillaProgress;
            Persistence.FullWorld = _vanillaFull;
            IsActive = false;
            _activeSlot = 0;
        }

        private static object NewStorage(Type storageType, object platform, string folderName)
        {
            ConstructorInfo ctor = FindStorageCtor(storageType);
            return ctor.Invoke(new object[] { platform, folderName });
        }

        private static ConstructorInfo FindStorageCtor(Type storageType)
        {
            foreach (ConstructorInfo c in storageType.GetConstructors(ANY))
            {
                ParameterInfo[] p = c.GetParameters();
                if (p.Length == 2 && p[1].ParameterType == typeof(string))
                    return c;
            }
            throw new MissingMethodException(storageType.FullName, "(Platform, string)");
        }

        private static object GetCurrentPlatform(Type storageType)
        {
            Type paramType = FindStorageCtor(storageType).GetParameters()[0].ParameterType;
            Type platformType = paramType.Assembly.GetType("Platforms.Platform");
            if (platformType == null)
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    platformType = asm.GetType("Platforms.Platform");
                    if (platformType != null) break;
                }
            }
            if (platformType == null)
                throw new TypeLoadException("Platforms.Platform not found.");

            PropertyInfo current = platformType.GetProperty("Current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (current == null)
                throw new MissingMemberException(platformType.FullName, "Current");
            object platform = current.GetValue(null);
            if (platform == null || !paramType.IsInstanceOfType(platform))
                throw new InvalidOperationException($"Platform.Current is not a {paramType.FullName}.");
            return platform;
        }
    }
}
