using HarmonyLib;

namespace BackyardCardHud
{
    // Capture the live GameHUDUI instance for the overlay. As of game v1.0.9.2 the HUD
    // refactored batter/pitcher into home/away and replaced Set{Batter,Pitcher}NameAndPortrait
    // with a single RefreshTeamNamesAndPortraits — hook that instead.
    [HarmonyPatch(typeof(GameHUDUI), "RefreshTeamNamesAndPortraits")]
    internal static class HudCaptureHook
    {
        private static void Postfix(GameHUDUI __instance) => CardOverlay.Hud = __instance;
    }
}
