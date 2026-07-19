using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BackyardCardHud
{
    /// <summary>
    /// Shrinks the grouped swing-type / pitch-type selector panels a touch and nudges them
    /// down a few px to clear the pitcher card. Uniform localScale + a fixed position offset
    /// from each panel's captured original pos. Re-asserted each frame (from LateUpdate).
    /// </summary>
    internal static class GameplayHudTweaks
    {
        // Tunable — 1.0 = vanilla size.
        private const float SelectorScale = 0.85f;
        private const float PanelDropPx = 25f;
        private const float PromptDropPx = 15f;

        private static readonly Dictionary<GameObject, Vector2> OrigPos = new Dictionary<GameObject, Vector2>();

        private static readonly AccessTools.FieldRef<GameHUDUI, GameObject> BatterTypes =
            AccessTools.FieldRefAccess<GameHUDUI, GameObject>("batterPanel");
        private static readonly AccessTools.FieldRef<GameHUDUI, GameObject> PitcherTypes =
            AccessTools.FieldRefAccess<GameHUDUI, GameObject>("pitcherPanel");
        private static readonly AccessTools.FieldRef<GameHUDUI, GameObject> SecBatterTypes =
            AccessTools.FieldRefAccess<GameHUDUI, GameObject>("secBatterPanel");
        private static readonly AccessTools.FieldRef<GameHUDUI, GameObject> SecPitcherTypes =
            AccessTools.FieldRefAccess<GameHUDUI, GameObject>("secPitcherPanel");
        private static readonly AccessTools.FieldRef<GameHUDUI, GameObject> ChangePitchPrompt =
            AccessTools.FieldRefAccess<GameHUDUI, GameObject>("changePitchPrompt");
        private static readonly AccessTools.FieldRef<GameHUDUI, GameObject> ChangeBatPrompt =
            AccessTools.FieldRefAccess<GameHUDUI, GameObject>("changeBatPrompt");

        public static void Apply(GameHUDUI hud)
        {
            try
            {
                Scale(BatterTypes(hud));
                Scale(PitcherTypes(hud));
                Scale(SecBatterTypes(hud));
                Scale(SecPitcherTypes(hud));

                Drop(ChangePitchPrompt(hud), PromptDropPx);
                Drop(ChangeBatPrompt(hud), PromptDropPx);
            }
            catch (Exception e) { Plugin.Log.LogError("GameplayHudTweaks: " + e); }
        }

        private static void Scale(GameObject go)
        {
            if (go == null) return;
            var s = Vector3.one * SelectorScale;
            if (go.transform.localScale != s)
                go.transform.localScale = s;
            Drop(go, PanelDropPx);
        }

        // Moves an element straight down by px, relative to its captured original position.
        private static void Drop(GameObject go, float px)
        {
            if (go == null || !(go.transform is RectTransform rt)) return;
            if (!OrigPos.TryGetValue(go, out var orig))
            {
                orig = rt.anchoredPosition;
                OrigPos[go] = orig;
            }
            var target = orig + new Vector2(0f, -px);
            if (rt.anchoredPosition != target)
                rt.anchoredPosition = target;
        }
    }
}
