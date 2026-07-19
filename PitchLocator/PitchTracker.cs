using System;
using System.Linq;
using BepInEx.Configuration;
using BYB.AI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BackyardPitchLocator
{
    // Classic "X marks where the pitch crossed the plate" — a batting read-aid, useful with the
    // aim assist on or off. (BYB2026 has NO coded pitch-assist setting; its only built-in
    // prediction reticle is hard-locked to tutorial mode — so this is our own overlay.)
    //
    // Behavior we replicate from the old games:
    //   * A deep-red X appears at the pitch's plate location on any pitch that reaches the plate
    //     untouched (called ball, called strike, or a swing-and-miss).
    //   * It stays through the result, then disappears once the ball is back in the
    //     pitcher's hand, ready for the next pitch.
    //
    // How it works:
    //   * Ball = SingletonMB<Baseball>.Instance. DistanceToStrikezone = Position.z - plate.z,
    //     so the ball crosses the plate the frame that flips from >0 to <=0 — that TIMES the marker.
    //   * Position: by default we snap the X to the aim-assist circle (the pitcher's aim target,
    //     StrikeZone.PitchingTargetPositionVector3) that players read as the sweet spot, since a
    //     curveball physically curves away from it. AnchorToAimAssist=false instead marks the
    //     ball's true curved crossing (sub-frame lerp between last/this frame).
    //   * "Not hit" = Baseball.LastHit is null or older than the current pitch (LastPitch.time).
    //   * "Back in the pitcher's hand" = the pitcher's fielder behavior HasTheBall again
    //     (AIPitcherBehavior : AIFielderBehavior). At release this is false, so we only start
    //     checking after the ball has crossed — no premature hide.

    internal static class PitchTrackerConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> Color;
        internal static ConfigEntry<float> FontSize;
        internal static ConfigEntry<bool> Outline;
        internal static ConfigEntry<bool> AnchorToAimAssist;
        internal static ConfigEntry<float> OffsetX;
        internal static ConfigEntry<float> OffsetY;

        internal static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("Pitch Locator", "Enabled", true,
                "Show a red X where each pitch crossed the plate (called ball/strike or swing-and-miss). "
                + "The game has no 'Pitch Assist' setting to key off, so this is simply on/off.");
            Color = cfg.Bind("Pitch Locator", "Color", "#CC0000",
                "X color as an HTML hex string (e.g. #CC0000 = deep red).");
            FontSize = cfg.Bind("Pitch Locator", "FontSize", 45f,
                new ConfigDescription("On-screen size of the X (1920x1080 reference units).",
                    new AcceptableValueRange<float>(20f, 240f)));
            Outline = cfg.Bind("Pitch Locator", "Outline", true,
                "Draw a dark outline behind the X so it stays legible over any field.");
            AnchorToAimAssist = cfg.Bind("Pitch Locator", "AnchorToAimAssist", true,
                "Place the X on the aim-assist circle (the pitcher's target 'sweet spot') rather than the ball's true curved crossing point. A curveball physically lands offset from the circle, but players read the circle as the pitch location, so ON matches expectations. Turn OFF to mark the exact spot the ball passed through.");
            OffsetX = cfg.Bind("Pitch Locator", "OffsetX", 0f,
                new ConfigDescription("Fine-tune nudge of the X in world units, left(-)/right(+). Leave 0 unless the X sits consistently off to one side.",
                    new AcceptableValueRange<float>(-2f, 2f)));
            OffsetY = cfg.Bind("Pitch Locator", "OffsetY", 0f,
                new ConfigDescription("Fine-tune nudge of the X in world units, down(-)/up(+). Leave 0 unless the X sits consistently high or low.",
                    new AcceptableValueRange<float>(-2f, 2f)));
        }
    }

    internal class PitchTracker : MonoBehaviour
    {
        private RectTransform _canvasRect;
        private TextMeshProUGUI _x;
        private RectTransform _xRect;
        private TMP_FontAsset _font;
        private bool _built;

        // Per-pitch tracking.
        private float? _trackedPitch;   // LastPitch.time.Value we're following
        private bool _crossed;          // has this pitch crossed the plate yet
        private Vector3 _crossWorld;    // world point to keep projecting to
        private float? _prevDist;       // previous frame's DistanceToStrikezone
        private Vector3 _prevPos;       // previous frame's ball position

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400; // above the game HUD

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRect = GetComponent<RectTransform>();
        }

        private void LateUpdate()
        {
            try
            {
                if (PitchTrackerConfig.Enabled == null || !PitchTrackerConfig.Enabled.Value)
                {
                    Show(false);
                    return;
                }

                var ball = SafeBall();
                if (ball == null) { Reset(); return; }

                // Identify the current pitch; a new one resets tracking (and clears the X:
                // the ball was released, i.e. back in hand a moment ago).
                float? pitch = SafePitchTime(ball);
                if (pitch != _trackedPitch)
                {
                    _trackedPitch = pitch;
                    _crossed = false;
                    _prevDist = null;
                    Show(false);
                }

                if (!_trackedPitch.HasValue) { Show(false); return; }

                // --- Show: detect the plate crossing on an untouched pitch ---
                if (!_crossed && !WasHit(ball, _trackedPitch.Value))
                {
                    float dist = SafeDist(ball);
                    Vector3 pos = ball.Position;
                    if (_prevDist.HasValue && _prevDist.Value > 0f && dist <= 0f)
                    {
                        // Sub-frame interpolation to the plate plane = the ball's TRUE crossing point.
                        float span = _prevDist.Value - dist;
                        float t = span > 1e-5f ? _prevDist.Value / span : 1f;
                        Vector3 anchor = Vector3.Lerp(_prevPos, pos, Mathf.Clamp01(t));

                        // Default: snap to the aim-assist circle (the pitcher's aim target). Players
                        // read that circle as the sweet spot; a curveball's true crossing lands offset
                        // from it. Fall back to the true crossing if aim data is missing or opt-out.
                        if (PitchTrackerConfig.AnchorToAimAssist.Value)
                        {
                            Vector3? aim = SafeAimTarget();
                            if (aim.HasValue) anchor = aim.Value;
                        }

                        // Optional user fine-tune (world units).
                        _crossWorld = anchor + new Vector3(PitchTrackerConfig.OffsetX.Value, PitchTrackerConfig.OffsetY.Value, 0f);
                        _crossed = true;
                        EnsureBuilt();
                    }
                    _prevDist = dist;
                    _prevPos = pos;
                }

                // --- Hide: ball back in the pitcher's hand, hit, or play moved on ---
                if (_crossed && ShouldHide(ball, _trackedPitch.Value))
                {
                    _crossed = false;
                    Show(false);
                    return;
                }

                // --- Keep the X glued to its world point while shown ---
                if (_crossed && _built)
                    ProjectAndShow();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("PitchTracker.LateUpdate: " + e);
            }
        }

        private bool ShouldHide(Baseball ball, float pitchTime)
        {
            if (WasHit(ball, pitchTime)) return true;

            switch (SafePhase())
            {
                case GamePhase.Fielding:
                case GamePhase.ReturningToStartPositions:
                case GamePhase.Finish:
                case GamePhase.Highlight:
                case GamePhase.Setup:
                    return true;
            }

            // The faithful trigger: the pitcher has received the ball again.
            return PitcherHasBall();
        }

        private void ProjectAndShow()
        {
            var cam = Camera.main;
            if (cam == null) cam = Camera.allCameras.FirstOrDefault(c => c != null && c.isActiveAndEnabled);
            if (cam == null) { Show(false); return; }

            Vector3 sp = cam.WorldToScreenPoint(_crossWorld);
            if (sp.z <= 0f) { Show(false); return; } // behind the camera

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, new Vector2(sp.x, sp.y), null, out var local))
            {
                _xRect.anchoredPosition = local;
                ApplyStyle();
                Show(true);
            }
        }

        // ---- build / style ----

        private void EnsureBuilt()
        {
            if (_built) return;
            if (_font == null) _font = ResolveFont();
            if (_font == null) return; // try again next frame

            var go = new GameObject("PitchX", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            _xRect = (RectTransform)go.transform;
            _xRect.anchorMin = _xRect.anchorMax = new Vector2(0.5f, 0.5f);
            _xRect.pivot = new Vector2(0.5f, 0.5f);
            _xRect.sizeDelta = new Vector2(160f, 160f);

            _x = go.GetComponent<TextMeshProUGUI>();
            _x.font = _font;
            _x.text = "X";
            _x.alignment = TextAlignmentOptions.Center;
            _x.textWrappingMode = TextWrappingModes.NoWrap;
            _x.raycastTarget = false;
            _x.fontStyle = FontStyles.Bold;
            ApplyStyle();

            go.SetActive(false);
            _built = true;
        }

        private void ApplyStyle()
        {
            if (_x == null) return;
            _x.fontSize = PitchTrackerConfig.FontSize.Value;
            _x.color = ParseColor(PitchTrackerConfig.Color.Value, new Color(0.8f, 0f, 0f, 1f));

            if (PitchTrackerConfig.Outline.Value)
            {
                _x.outlineWidth = 0.22f;
                _x.outlineColor = new Color32(0, 0, 0, 220);
            }
            else
            {
                _x.outlineWidth = 0f;
            }
        }

        private void Show(bool v)
        {
            if (_x != null && _x.gameObject.activeSelf != v)
                _x.gameObject.SetActive(v);
        }

        private void Reset()
        {
            _trackedPitch = null;
            _crossed = false;
            _prevDist = null;
            Show(false);
        }

        // ---- safe game-state reads ----

        private static Baseball SafeBall()
        {
            try { return SingletonMB<Baseball>.Instance; }
            catch { return null; }
        }

        // Ball's signed distance to the plate plane; only used to detect the crossing that
        // TIMES the marker (the X's position comes from the aim target / true-crossing lerp).
        private static float SafeDist(Baseball ball)
        {
            try { return ball.DistanceToStrikezone; }
            catch { return float.NaN; }
        }

        private static float? SafePitchTime(Baseball ball)
        {
            try { var lp = ball.LastPitch; return lp.HasValue ? lp.Value.time.Value : (float?)null; }
            catch { return null; }
        }

        private static bool WasHit(Baseball ball, float pitchTime)
        {
            try
            {
                var lh = ball.LastHit;
                return lh.HasValue && lh.Value.time.Value >= pitchTime;
            }
            catch { return false; }
        }

        private static GamePhase SafePhase()
        {
            try { return GameStateManager.Phase; }
            catch { return GamePhase.Invalid; }
        }

        // The aim-assist circle's world position = where the pitcher aimed this pitch.
        // (BattingTargetPositionVector is the batter's own swing cursor, not the pitch, so we
        // deliberately use the Pitching target here.)
        private static Vector3? SafeAimTarget()
        {
            try
            {
                if (SingletonMB<StrikeZone>.Instance != null)
                    return StrikeZone.PitchingTargetPositionVector3;
            }
            catch { }
            return null;
        }

        private static bool PitcherHasBall()
        {
            try
            {
                var pitcher = GameStateManager.GetPitcher();
                // AIPitcherBehavior : AIFielderBehavior, so the base type reads HasTheBall
                // whichever behavior the pitcher currently holds.
                if (pitcher != null && pitcher.TryGetBehavior<AIFielderBehavior>(out var beh))
                    return beh.HasTheBall;
            }
            catch { }
            return false;
        }

        // ---- helpers ----

        private static TMP_FontAsset ResolveFont()
        {
            try
            {
                if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
                return Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
            }
            catch { return null; }
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex.Trim(), out var c))
                return c;
            return fallback;
        }
    }
}
