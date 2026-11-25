using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PlateUpTycoon
{
    /// <summary>TycoonLog.txt, the mod's own short log next to Player.log.</summary>
    public static class TycoonLog
    {
        public const string FILE = "TycoonLog.txt", PREVIOUS = "TycoonLog-previous.txt";
        private const long MAX_BYTES = 4L * 1024 * 1024;
        private const int STACK_LINES = 8;

        private static readonly object Gate = new object();
        private static StreamWriter _writer;
        private static long _written;
        private static string _lastLine;
        private static int _repeats;
        private static bool _started, _full;

        public static void Start()
        {
            if (_started) return;
            _started = true;
            try
            {
                string dir = Application.persistentDataPath;
                string path = Path.Combine(dir, FILE), previous = Path.Combine(dir, PREVIOUS);
                if (File.Exists(path))
                {
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(path, previous);
                }
                _writer = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
                Raw($"Tycoon Mode {Mod.MOD_VERSION} ({Mod.BUILD}) | game {Application.version} | {DateTime.Now:yyyy-MM-dd HH:mm} | {SystemInfo.operatingSystem}");
                Raw("Send this file (and TycoonLog-previous.txt if the game was restarted since the problem) with your bug report.");
                Application.logMessageReceivedThreaded += OnUnityLog;
            }
            catch (Exception e)
            {
                _writer = null;
                Debug.LogWarning($"[{Mod.MOD_NAME}] Could not open {FILE}: {e.Message}");
            }
        }

        public static void Write(char level, string msg)
        {
            if (_writer == null) return;
            if (_full && level == 'I') return;
            Line($"{level} {msg}");
        }

        private static void OnUnityLog(string condition, string stack, LogType type)
        {
            if (_writer == null || condition == null) return;
            if (condition.Contains("[" + Mod.MOD_NAME + "]")) return;
            if (condition.Contains("[DEBUG]") || condition.Contains("Found mod:")) return;
            bool ours = Mentions(condition) || Mentions(stack);
            bool network = condition.Contains(" NETW]") && (condition.Contains("Transport") || condition.Contains("Disconnect") || condition.Contains("Lobby"));
            if (!ours && !network) return;
            char level = type == LogType.Exception || type == LogType.Error || type == LogType.Assert ? 'E' : type == LogType.Warning ? 'W' : 'I';
            if (_full && level == 'I') return;
            var sb = new StringBuilder($"{level} [game] {condition.Trim()}");
            if (ours && !string.IsNullOrEmpty(stack) && level == 'E')
            {
                string[] lines = stack.Split('\n');
                for (int i = 0, n = 0; i < lines.Length && n < STACK_LINES; i++)
                {
                    string l = lines[i].Trim();
                    if (l.Length == 0) continue;
                    sb.Append("\n    ").Append(l);
                    n++;
                }
            }
            Line(sb.ToString());
        }

        private static bool Mentions(string s) =>
            !string.IsNullOrEmpty(s) && (s.IndexOf("PlateUpTycoon", StringComparison.OrdinalIgnoreCase) >= 0);

        private static void Line(string text)
        {
            lock (Gate)
            {
                if (text == _lastLine) { _repeats++; return; }
                FlushRepeats();
                _lastLine = text;
                Raw($"{DateTime.Now:HH:mm:ss} {text}");
            }
        }

        private static void FlushRepeats()
        {
            if (_repeats > 0) Raw($"         (the line above x{_repeats + 1})");
            _repeats = 0;
        }

        private static void Raw(string text)
        {
            try
            {
                _writer.WriteLine(text);
                _written += text.Length + 2;
                if (!_full && _written > MAX_BYTES)
                {
                    _full = true;
                    _writer.WriteLine("--- the log is large: from here on only warnings and errors ---");
                }
            }
            catch (Exception) { }
        }

        private static Kitchen.SceneType _scene = Kitchen.SceneType.Null;
        private static float _sceneAt = -1f;
        private static bool _roleLogged;

        public static void Tick()
        {
            if (_writer == null) return;
            Kitchen.SceneType scene = Kitchen.GameInfo.CurrentScene;
            if (scene != _scene)
            {
                _scene = scene;
                _sceneAt = Time.realtimeSinceStartup;
                _roleLogged = false;
                Write('I', $"--- scene: {scene} ---");
            }
            if (!_roleLogged && TycoonSession.IsActive && _sceneAt >= 0f && Time.realtimeSinceStartup - _sceneAt > 3f)
            {
                _roleLogged = true;
                Write('I', TycoonNetData.IsHost ? "This player is the host." : "This player is a guest.");
            }
        }
    }
}
