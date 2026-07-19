using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BackyardCardHud
{
    // Player Cards — a standalone BepInEx plugin.
    // Replaces the flat VS matchup banner with the game's collectible trading cards:
    // batter card bottom-left, pitcher card top-right, with their stat lines. Custom/League
    // kids (no card of their own) fall back to the LINE DRIVE / HEAT skill cards. In League
    // mode it also adds a live season batting-average line under the batter card.
    // Draws its own overlay on top of the HUD; modifies no game files and no game assets.
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.flami.cardhud";
        public const string Name = "Player Cards";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            // Activates the GameHUDUI hooks in CardSwapPatch (they feed the live HUD to the overlay).
            new Harmony(Guid).PatchAll();

            var overlayGo = new GameObject("PlayerCardsOverlay");
            UnityEngine.Object.DontDestroyOnLoad(overlayGo);
            overlayGo.AddComponent<CardOverlay>();

            Log.LogInfo($"{Name} v{Version} loaded.");
        }
    }
}
