using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace BackyardPitchLocator
{
    // Pitch Locator (Old School) — a standalone BepInEx plugin.
    // Brings back the classic "X marks where the pitch crossed the plate" read-aid.
    // Ships only this plugin's own code; modifies no game files and no game assets.
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.flami.pitchlocator";
        public const string Name = "Pitch Locator (Old School)";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            PitchTrackerConfig.Bind(Config);

            // Persistent host with its own screen-space canvas.
            var go = new GameObject("BackyardPitchLocator");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<PitchTracker>();

            Log.LogInfo($"{Name} v{Version} loaded.");
        }
    }
}
