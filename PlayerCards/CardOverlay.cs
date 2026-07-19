using System;
using System.Collections.Generic;
using System.Globalization;
using BYB.AI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BackyardCardHud
{
    /// <summary>
    /// Our custom card HUD, drawn on a screen-space overlay we fully control.
    /// Round A: batter card in the left column (AT BAT: label above, tally + AVG pills below),
    /// pitcher card top-right (ON THE MOUND: label above, P/K/BB pill below). The original
    /// matchup banner is scaled to zero (kept active so its stat text keeps feeding us live
    /// numbers, which we mirror). Read-only against saves.
    /// </summary>
    internal class CardOverlay : MonoBehaviour
    {
        // ---- Tunable layout (1920x1080 reference units) ----
        private static readonly Vector2 BatterAnchor  = new Vector2(0f, 1f);   // top-left ref
        private static readonly Vector2 BatterPivot   = new Vector2(0.5f, 1f); // stack grows down from top-center
        private static readonly Vector2 BatterPos     = new Vector2(200f, -380f);
        private static readonly Vector2 PitcherAnchor = new Vector2(1f, 1f);   // top-right ref
        private static readonly Vector2 PitcherPivot  = new Vector2(1f, 1f);
        private static readonly Vector2 PitcherPos    = new Vector2(-78f, -18f);

        private const float BatterCardW = 210f, BatterCardH = 294f;
        private const float PitcherCardW = 150f, PitcherCardH = 210f;
        private const float JuiceScale = 0.85f;
        private static readonly Color PillRed   = new Color(0.78f, 0.16f, 0.13f, 0.92f);
        private static readonly Color PillBlue  = new Color(0.15f, 0.28f, 0.62f, 0.92f);
        private static readonly Color LabelDark = new Color(0f, 0f, 0f, 0.82f);

        private Sprite _pillSprite; // rounded 9-slice for pill/label backgrounds
        // ----------------------------------------------------

        internal static GameHUDUI Hud; // set by the GameHUDUI hooks

        private static readonly AccessTools.FieldRef<GameHUDUI, Image> BatterThumb =
            AccessTools.FieldRefAccess<GameHUDUI, Image>("batterThumbnail");
        private static readonly AccessTools.FieldRef<GameHUDUI, Image> PitcherThumb =
            AccessTools.FieldRefAccess<GameHUDUI, Image>("pitcherThumbnail");
        private static readonly AccessTools.FieldRef<GameHUDUI, TextMeshProUGUI> BatterStats =
            AccessTools.FieldRefAccess<GameHUDUI, TextMeshProUGUI>("batterStats");
        private static readonly AccessTools.FieldRef<GameHUDUI, TextMeshProUGUI> ScoreCount =
            AccessTools.FieldRefAccess<GameHUDUI, TextMeshProUGUI>("scoreCount");
        private static readonly AccessTools.FieldRef<GameHUDUI, GameObject> ChangePitchPrompt =
            AccessTools.FieldRefAccess<GameHUDUI, GameObject>("changePitchPrompt");
        private static readonly AccessTools.FieldRef<GameHUDUI, GameObject> ChangeBatPrompt =
            AccessTools.FieldRefAccess<GameHUDUI, GameObject>("changeBatPrompt");
        private static readonly AccessTools.FieldRef<GameHUDUI, TextMeshProUGUI> BatterName =
            AccessTools.FieldRefAccess<GameHUDUI, TextMeshProUGUI>("batterName");
        private static readonly AccessTools.FieldRef<GameHUDUI, TextMeshProUGUI> PitcherName =
            AccessTools.FieldRefAccess<GameHUDUI, TextMeshProUGUI>("pitcherName");

        private TMP_FontAsset _gameFont;
        private Sprite _labelSprite;    // dark prompt pill (from changePitch/BatPrompt)
        private Sprite _statPillSprite; // colored tintable pill (Collor_Fill from the banner)
        private bool _built;

        private GameObject _batterGroup, _pitcherGroup;
        private Image _batterCard, _pitcherCard;
        private TextMeshProUGUI _batterTally, _batterAvg, _pitcherStatsText;
        private GameObject _batterAvgPill;

        // Name strip drawn over the bottom of a placeholder card (custom kids only).
        private GameObject _batterNameStrip, _pitcherNameStrip;
        private TextMeshProUGUI _batterNameText, _pitcherNameText;
        private bool _batterIsCustom, _pitcherIsCustom;

        private string _lastBatterId, _lastPitcherId;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // LateUpdate so our hide/reposition wins over the game's own per-frame layout/animation.
        private void LateUpdate()
        {
            try
            {
                var batter = SafeGet(isBatter: true);
                var pitcher = SafeGet(isBatter: false);
                bool inGame = Hud != null && Hud.isActiveAndEnabled && batter != null && pitcher != null;

                if (!inGame)
                {
                    ShowGroups(false);
                    return;
                }

                CaptureAssets();
                if (!_built)
                {
                    if (_gameFont == null) { ShowGroups(false); return; }
                    BuildGroups();
                    _built = true;
                }

                HideOriginalBanner();
                GameplayHudTweaks.Apply(Hud);

                RefreshCard(_batterCard, batter, ref _lastBatterId, isBatter: true);
                RefreshCard(_pitcherCard, pitcher, ref _lastPitcherId, isBatter: false);

                _batterTally.text = ReadGameText(BatterStats);
                _pitcherStatsText.text = ReadGameText(ScoreCount);
                UpdateAvg(batter);

                // Custom kids use placeholder cards → overlay their name on the card bottom.
                UpdateNameStrip(_batterNameStrip, _batterNameText, _batterIsCustom, BatterName);
                UpdateNameStrip(_pitcherNameStrip, _pitcherNameText, _pitcherIsCustom, PitcherName);

                // Reveal in sync with the game HUD: it stays hidden until the batter finishes
                // walking up to the plate, so wait for the same signal (avoids popping in early).
                ShowGroups(BatterSettled(batter));
            }
            catch (Exception e) { Plugin.Log.LogError("CardOverlay.Update: " + e); }
        }

        // ---- building ----

        private void CaptureAssets()
        {
            if (Hud == null) return;

            if (_gameFont == null)
            {
                try { var bs = BatterStats(Hud); if (bs != null && bs.font != null) _gameFont = bs.font; }
                catch { }
            }

            if (_labelSprite == null)
            {
                try
                {
                    _labelSprite = GetLargestSprite(ChangePitchPrompt(Hud)) ?? GetLargestSprite(ChangeBatPrompt(Hud));
                    if (_labelSprite != null) Plugin.Log.LogInfo($"[Sprites] label pill = '{_labelSprite.name}'");
                }
                catch { }
            }

            if (_statPillSprite == null)
            {
                try
                {
                    var bt = BatterThumb(Hud);
                    var group = bt != null ? bt.transform.parent : null; // BatterStatCast
                    var img = group != null ? FindImageByName(group, "Collor_Fill") : null;
                    if (img != null && img.sprite != null)
                    {
                        _statPillSprite = img.sprite;
                        Plugin.Log.LogInfo($"[Sprites] stat pill = '{_statPillSprite.name}'");
                    }
                }
                catch { }
            }
        }

        private static Sprite GetLargestSprite(GameObject go)
        {
            if (go == null) return null;
            Sprite best = null;
            float bestArea = 0f;
            foreach (var img in go.GetComponentsInChildren<Image>(true))
            {
                if (img == null || img.sprite == null) continue;
                var r = img.rectTransform.rect;
                float a = Mathf.Abs(r.width * r.height);
                if (a > bestArea) { bestArea = a; best = img.sprite; }
            }
            return best;
        }

        private static Image FindImageByName(Transform root, string name)
        {
            foreach (var img in root.GetComponentsInChildren<Image>(true))
                if (img != null && img.gameObject.name == name) return img;
            return null;
        }

        private void BuildGroups()
        {
            // Fallback rounded 9-slice if a game sprite grab misses.
            _pillSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

            // Labels: prefer the dark prompt pill (tint white to show natively); if that grab
            // missed, reuse the game's gradient pill sprite tinted dark so labels still match.
            Sprite labelSprite = _labelSprite != null ? _labelSprite : _statPillSprite;
            Color labelColor = _labelSprite != null ? Color.white : LabelDark;

            _batterGroup = CreateGroup("BatterGroup", BatterAnchor, BatterPivot, BatterPos);
            var batterLabel = CreatePill(_batterGroup.transform, BatterCardW, 32f, 24f, labelColor, labelSprite, out var batterLabelPill);
            batterLabel.text = "AT BAT:";
            SizeToText(batterLabelPill, batterLabel, 40f);
            _batterCard = CreateCard(_batterGroup.transform, BatterCardW, BatterCardH);
            _batterNameText = CreateNameStrip(_batterCard, BatterCardH * 0.11f, 16f, labelSprite, out _batterNameStrip);
            _batterTally = CreatePill(_batterGroup.transform, BatterCardW, 34f, 24f, PillRed, _statPillSprite, out _);
            _batterAvg = CreatePill(_batterGroup.transform, BatterCardW, 34f, 24f, PillRed, _statPillSprite, out _batterAvgPill);

            _pitcherGroup = CreateGroup("PitcherGroup", PitcherAnchor, PitcherPivot, PitcherPos);
            var pitcherLabel = CreatePill(_pitcherGroup.transform, PitcherCardW, 28f, 20f, labelColor, labelSprite, out var pitcherLabelPill);
            pitcherLabel.text = "ON THE MOUND:";
            SizeToText(pitcherLabelPill, pitcherLabel, 36f);
            _pitcherCard = CreateCard(_pitcherGroup.transform, PitcherCardW, PitcherCardH);
            _pitcherNameText = CreateNameStrip(_pitcherCard, PitcherCardH * 0.12f, 12f, labelSprite, out _pitcherNameStrip);
            _pitcherStatsText = CreatePill(_pitcherGroup.transform, PitcherCardW, 30f, 20f, PillBlue, _statPillSprite, out _);

            ShowGroups(false);
        }

        private GameObject CreateGroup(string name, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 6f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        // Widen a label pill to fit its text (plus horizontal padding).
        private static void SizeToText(GameObject pill, TextMeshProUGUI tmp, float pad)
        {
            var le = pill.GetComponent<LayoutElement>();
            if (le != null) le.preferredWidth = tmp.GetPreferredValues(tmp.text).x + pad;
        }

        // A dark gradient strip with a name, overlaid on the bottom of a card. Child of the
        // card (not a layout element), so it floats over the art. Hidden unless it's a custom kid.
        private TextMeshProUGUI CreateNameStrip(Image card, float height, float fontSize, Sprite sprite, out GameObject strip)
        {
            strip = new GameObject("NameStrip", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(card.transform, false);
            var rt = (RectTransform)strip.transform;
            rt.anchorMin = new Vector2(0f, 0f);   // stretch across the bottom
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-8f, height); // slight inset from the card's border
            rt.anchoredPosition = new Vector2(0f, 3f); // sit right down at the bottom of the card

            var bg = strip.GetComponent<Image>();
            bg.color = Color.black; // fully opaque so nothing shows through
            bg.raycastTarget = false;
            // Solid rounded sprite (NOT the semi-transparent gradient) for full coverage.
            if (_pillSprite != null) { bg.sprite = _pillSprite; bg.type = Image.Type.Sliced; }

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(strip.transform, false);
            var trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(4f, 2f); trt.offsetMax = new Vector2(-4f, -2f);
            var t = txtGo.GetComponent<TextMeshProUGUI>();
            StyleText(t, fontSize, Color.white);  // NoWrap single line (short "First L." names)
            t.enableAutoSizing = true;            // shrink to fit width if long
            t.fontSizeMax = fontSize;
            t.fontSizeMin = 7f;

            strip.SetActive(false);
            return t;
        }

        private void UpdateNameStrip(GameObject strip, TextMeshProUGUI text, bool isCustom,
                                     AccessTools.FieldRef<GameHUDUI, TextMeshProUGUI> nameField)
        {
            if (strip == null) return;
            if (!isCustom)
            {
                if (strip.activeSelf) strip.SetActive(false);
                return;
            }
            text.text = ReadGameText(nameField);
            if (!strip.activeSelf) strip.SetActive(true);
        }

        private Image CreateCard(Transform parent, float w, float h)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.enabled = false; // stay hidden until a sprite loads (avoids a white box)
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = w; le.preferredHeight = h;
            return img;
        }

        private TextMeshProUGUI CreatePill(Transform parent, float w, float h, float fontSize, Color color, Sprite sprite, out GameObject pill)
        {
            pill = new GameObject("Pill", typeof(RectTransform), typeof(Image));
            pill.transform.SetParent(parent, false);
            var bg = pill.GetComponent<Image>();
            bg.color = color;
            bg.raycastTarget = false;
            var s = sprite != null ? sprite : _pillSprite;
            if (s != null)
            {
                bg.sprite = s;
                bg.type = Image.Type.Sliced;
            }
            var le = pill.AddComponent<LayoutElement>();
            le.preferredWidth = w; le.preferredHeight = h;

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(pill.transform, false);
            var rt = (RectTransform)txtGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = txtGo.GetComponent<TextMeshProUGUI>();
            StyleText(t, fontSize, Color.white);
            return t;
        }

        private void StyleText(TextMeshProUGUI t, float fontSize, Color color)
        {
            if (_gameFont != null) t.font = _gameFont;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // ---- per-frame updates ----

        private void ShowGroups(bool v)
        {
            if (_batterGroup != null && _batterGroup.activeSelf != v) _batterGroup.SetActive(v);
            if (_pitcherGroup != null && _pitcherGroup.activeSelf != v) _pitcherGroup.SetActive(v);
        }

        private static Character SafeGet(bool isBatter)
        {
            try { return isBatter ? GameStateManager.GetBatter() : GameStateManager.GetPitcher(); }
            catch { return null; }
        }

        // True only while the batter is settled at the plate (WalkState.None) — i.e. not
        // walking IN (load / new batter) and not walking OUT (struck out / end of inning).
        // Mirrors when the game shows vs transitions out its own HUD.
        private static bool BatterSettled(Character batter)
        {
            try
            {
                if (batter != null && batter.TryGetBehavior<AIBatterBehavior>(out var beh))
                    return beh.WalkingState == AIBatterBehavior.WalkState.None;
            }
            catch { }
            return true; // can't tell → don't block
        }

        private static string ReadGameText(AccessTools.FieldRef<GameHUDUI, TextMeshProUGUI> field)
        {
            try { var t = field(Hud); return t != null ? t.text : ""; }
            catch { return ""; }
        }

        private void UpdateAvg(Character batter)
        {
            try
            {
                bool league = GameModeManager.GameModeEnum == GameModeEnum.League;
                if (!league)
                {
                    if (_batterAvgPill != null && _batterAvgPill.activeSelf) _batterAvgPill.SetActive(false);
                    return;
                }

                var s = BaseballStatisticsManager.SeasonStatistics?.GetCharacterStatisticsByName(batter.Id);
                if (s == null) { if (_batterAvgPill.activeSelf) _batterAvgPill.SetActive(false); return; }

                float avg = s.BattingAverage / 10000f;
                string txt = avg.ToString("0.000", CultureInfo.InvariantCulture);
                if (txt.StartsWith("0")) txt = txt.Substring(1); // baseball-style .312

                _batterAvg.text = "AVG   " + txt;
                if (!_batterAvgPill.activeSelf) _batterAvgPill.SetActive(true);
            }
            catch (Exception e) { Plugin.Log.LogError("CardOverlay.UpdateAvg: " + e); }
        }

        private void HideOriginalBanner()
        {
            try
            {
                var bt = BatterThumb(Hud);
                var pt = PitcherThumb(Hud);
                if (bt != null) ZeroScale(bt.transform.parent);          // BatterStatCast
                if (pt != null) ZeroScale(pt.transform.parent);          // PitcherStatCast
                if (bt != null)
                {
                    var widget = bt.transform.parent.parent;             // MatchUpWidget
                    if (widget != null)
                    {
                        var vs = widget.Find("VSImage");
                        if (vs != null) ZeroScale(vs);

                        var juice = widget.Find("JuiceBoxMeter");        // the pitcher's tall straw/juice
                        if (juice != null)
                        {
                            var s = Vector3.one * JuiceScale;
                            if (juice.localScale != s) juice.localScale = s;
                        }
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogError("CardOverlay.HideOriginalBanner: " + e); }
        }

        private static void ZeroScale(Transform t)
        {
            if (t != null && t.localScale != Vector3.zero) t.localScale = Vector3.zero;
        }

        private void RefreshCard(Image img, Character character, ref string lastId, bool isBatter)
        {
            string id = character.Entry.CharacterId;
            if (string.IsNullOrEmpty(id) || id == lastId) return;
            lastId = id;
            LoadInto(img, id, isBatter);
        }

        private async void LoadInto(Image img, string id, bool isBatter)
        {
            try
            {
                // Prefer the kid's own 0-star card; if they have none (custom/League kids),
                // fall back to a skill card (LINE DRIVE for batters, HEAT for pitchers).
                // Resolving the data ourselves avoids the game's NRE on a null card.
                BaseballCardData charData = null;
                try { charData = CollectableCardDatabase.GetCardData(id, CardRarity.ZeroStar); } catch { }

                bool useFiller = charData == null;
                if (isBatter) _batterIsCustom = useFiller; else _pitcherIsCustom = useFiller;
                string cacheKey = useFiller ? (isBatter ? "__filler_batter" : "__filler_pitcher") : id;

                if (!Cache.TryGetValue(cacheKey, out var sprite) || sprite == null)
                {
                    BaseballCardData data = useFiller ? GetFillerCard(isBatter) : charData;
                    if (data != null) sprite = await CardLoader.GetCardFaceSpriteAsync(data);
                    if (sprite != null) Cache[cacheKey] = sprite;
                }

                if (img == null) return;
                if (sprite == null)
                {
                    Plugin.Log.LogInfo($"[Overlay] No card for '{id}' (filler={useFiller}, batter={isBatter}).");
                    img.enabled = false;
                    return;
                }
                img.sprite = sprite;
                img.enabled = true;
            }
            catch (Exception e) { Plugin.Log.LogError("CardOverlay.LoadInto: " + e); }
        }

        // The LINE DRIVE (batter) / HEAT (pitcher) skill card used for custom kids.
        private static BaseballCardData GetFillerCard(bool isBatter)
        {
            try
            {
                var type = isBatter ? FillerCardType.BattingPU : FillerCardType.PitchingPU;
                var list = CollectableCardDatabase.GetFillerCards(type);
                if (list == null) return null;

                string want = isBatter ? "linedrive" : "heat";

                // Exact (normalized) match first — avoids grabbing e.g. "Screaming Line Drive".
                foreach (var c in list)
                    if (c != null && Normalize(c.characterName) == want)
                        return c;
                // Loose fallback.
                foreach (var c in list)
                    if (c != null && Normalize(c.characterName).Contains(want))
                        return c;
            }
            catch (Exception e) { Plugin.Log.LogError("CardOverlay.GetFillerCard: " + e); }
            return null;
        }

        private static string Normalize(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.ToLowerInvariant().Replace(" ", "").Replace("-", "");
    }
}
