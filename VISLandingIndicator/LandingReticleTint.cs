using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace VISLandingIndicator
{
    // Lightens the fielding "where the ball lands" indicator so it stays visible
    // on night maps (and configured dark-surface maps like Tin Can Alley).
    //
    // The BaseballLandingIndicator prefab carries no color logic in code -- its look
    // comes entirely from the renderer's material. The game DOES tint its sister
    // element (the batting prediction reticle) light-gray at night; we do the same
    // here. First run also logs the material/shader/texture + the live FieldName so
    // we can confirm the tint took and capture exact field names for the config.

    internal static class ReticleConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ApplyOnNightMaps;
        internal static ConfigEntry<float> Brightness;
        internal static ConfigEntry<string> DarkFields;

        internal static void Bind(BepInEx.Configuration.ConfigFile cfg)
        {
            Enabled = cfg.Bind("Landing Indicator", "Enabled", true,
                "Master switch for the landing-indicator tint.");
            ApplyOnNightMaps = cfg.Bind("Landing Indicator", "ApplyOnNightMaps", true,
                "Lighten the landing indicator on any night map.");
            Brightness = cfg.Bind("Landing Indicator", "Brightness", 0.75f,
                new ConfigDescription("Gray level of the lightened indicator (0=black, 1=white). 0.75 matches the game's own night prediction-reticle color.",
                    new AcceptableValueRange<float>(0f, 1f)));
            DarkFields = cfg.Bind("Landing Indicator", "DarkFields", "Tin Can Alley, Cement Gardens",
                "Comma-separated field names that always use the light indicator, even in daytime (dark asphalt surfaces). Matching is space/case-insensitive and partial, so 'tincan' matches 'TinCanAlley'. Check LogOutput.log for the exact FieldName the game reports.");
        }
    }

    // Fires when the indicator activates for a hit (phase -> Fielding).
    [HarmonyPatch(typeof(BaseballLandingIndicator), "OnPhaseChanged")]
    internal static class LandingReticleTintPatch
    {
        private static bool _loggedMaterial;
        private static string _lastDecisionField;

        private static void Postfix(BaseballLandingIndicator __instance)
        {
            try
            {
                if (ReticleConfig.Enabled == null || !ReticleConfig.Enabled.Value)
                    return;
                // Only touch it when it's actually showing.
                if (!__instance.IsIndicatorActive)
                    return;

                string fieldName = SafeCurrentFieldName();
                bool isDay = GameplayLevelSettings.IsDayTime;
                bool lighten = ShouldLighten(fieldName, isDay);

                // One-time deep diagnostic of the renderer/material.
                var renderer = __instance.GetComponentInChildren<Renderer>(true);
                if (!_loggedMaterial)
                {
                    _loggedMaterial = true;
                    LogMaterialDiagnostic(renderer, fieldName, isDay, lighten);
                }

                // Log the per-field decision once per field so the config is easy to tune,
                // without spamming a line on every fly ball.
                if (_lastDecisionField != fieldName)
                {
                    _lastDecisionField = fieldName;
                    Plugin.Log.LogInfo($"[Reticle] Field='{fieldName}' IsDayTime={isDay} -> lighten={lighten} (brightness={ReticleConfig.Brightness.Value:0.00})");
                }

                if (!lighten || renderer == null)
                    return;

                float b = ReticleConfig.Brightness.Value;
                var tint = new Color(b, b, b, 1f);
                ApplyTint(renderer.material, tint);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Reticle] tint failed: {e.Message}");
            }
        }

        private static bool ShouldLighten(string fieldName, bool isDay)
        {
            if (ReticleConfig.ApplyOnNightMaps.Value && !isDay)
                return true;

            string fn = Normalize(fieldName);
            if (fn.Length > 0)
            {
                foreach (var raw in ReticleConfig.DarkFields.Value.Split(','))
                {
                    string dark = Normalize(raw);
                    if (dark.Length > 0 && fn.Contains(dark))
                        return true;
                }
            }
            return false;
        }

        private static void ApplyTint(Material mat, Color tint)
        {
            if (mat == null) return;
            // Cover the common Unity color property names; only set ones the shader has
            // so we don't spit "material doesn't have property" warnings.
            string[] props = { "_Color", "_BaseColor", "_TintColor", "_MainColor" };
            bool setAny = false;
            foreach (var p in props)
            {
                if (mat.HasProperty(p))
                {
                    var existing = mat.GetColor(p);
                    // Preserve the shader's own alpha handling; just swap the RGB tint.
                    mat.SetColor(p, new Color(tint.r, tint.g, tint.b, existing.a <= 0f ? 1f : existing.a));
                    setAny = true;
                }
            }
            if (!setAny)
                mat.color = tint; // last resort
        }

        private static void LogMaterialDiagnostic(Renderer renderer, string fieldName, bool isDay, bool lighten)
        {
            if (renderer == null)
            {
                Plugin.Log.LogWarning("[Reticle][DIAG] No Renderer found under BaseballLandingIndicator (could be a Projector/decal). Field='" + fieldName + "'");
                return;
            }
            var mat = renderer.sharedMaterial;
            string shader = mat != null && mat.shader != null ? mat.shader.name : "<null>";
            string matName = mat != null ? mat.name : "<null>";
            string tex = mat != null && mat.mainTexture != null ? mat.mainTexture.name : "<none>";
            string hasProps = "";
            if (mat != null)
            {
                foreach (var p in new[] { "_Color", "_BaseColor", "_TintColor", "_MainColor" })
                    if (mat.HasProperty(p)) hasProps += p + "=" + mat.GetColor(p) + " ";
            }
            Plugin.Log.LogInfo(
                $"[Reticle][DIAG] renderer={renderer.GetType().Name} material='{matName}' shader='{shader}' mainTexture='{tex}' colorProps=[{hasProps.Trim()}] | Field='{fieldName}' IsDayTime={isDay} lighten={lighten}");
        }

        private static string SafeCurrentFieldName()
        {
            try
            {
                var cur = BaseballFieldManager.Current;
                return cur != null ? cur.FieldName : "";
            }
            catch { return ""; }
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
