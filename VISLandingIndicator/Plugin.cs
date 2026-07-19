using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace VISLandingIndicator
{
    // VISLandingIndicator — a standalone BepInEx plugin ("VIS" = visibility).
    // Keeps the fielding "where the ball will land" indicator visible on night maps
    // (and configured dark-surface fields) by lightening it to match the game's own
    // night reticle color. Pure Harmony patch — no overlay, no game files touched.
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.flami.vislandingindicator";
        public const string Name = "VISLandingIndicator";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            ReticleConfig.Bind(Config);
            new Harmony(Guid).PatchAll();

            Log.LogInfo($"{Name} v{Version} loaded.");
        }
    }
}
