using HarmonyLib;

namespace BackyardCardHud
{
    // Lightweight hooks: just capture the live GameHUDUI instance for the overlay.
    // (Card art + stats are now drawn on our own overlay in CardOverlay.)

    [HarmonyPatch(typeof(GameHUDUI), "SetBatterNameAndPortrait")]
    internal static class BatterHudHook
    {
        private static void Postfix(GameHUDUI __instance) => CardOverlay.Hud = __instance;
    }

    [HarmonyPatch(typeof(GameHUDUI), "SetPitcherNameAndPortrait")]
    internal static class PitcherHudHook
    {
        private static void Postfix(GameHUDUI __instance) => CardOverlay.Hud = __instance;
    }
}
