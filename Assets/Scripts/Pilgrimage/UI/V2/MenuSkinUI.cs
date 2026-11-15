using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Pilgrimage.UI
{
    // Red / white / black layered menu skin (main menu, faction selection, LAN lobby, settings, results).
    // It is built at runtime on top of the authored menu screens and drives the same controller actions.
    // The in-game card table is never touched here.
    public sealed partial class DemoController
    {
        private static readonly Color MenuRed = new Color(.84f, .04f, .07f), MenuDeepRed = new Color(.36f, .01f, .03f),
            MenuBlack = new Color(.035f, .03f, .03f), MenuWhite = new Color(.97f, .95f, .92f), MenuGrey = new Color(.56f, .54f, .53f);
        private enum PlateStyle { Dark, Light }

        private TMP_FontAsset menuFont;
        private bool ownsMenuFont;
        private readonly List<UnityEngine.Object> menuAssets = new List<UnityEngine.Object>();
        private Sprite circleSprite, dotSprite;
        private TMP_Text menuFeedbackText;
        private RectTransform settingsPanel;
        private TMP_Text bgmValue, sfxValue;
        private Button screenModeButton;

        // LAN
        private MenuHover lanHostCard, lanJoinCard;
        private bool lanChooseHost = true;
        private Button lanConfirmButton, lanReadyButton, lanStartButton, lanBackButton, lanFirewallButton;
        private TMP_Text lobbyCountText, lobbyRoomText, lobbyHintText;
        private readonly TMP_Text[] lobbyRole = new TMP_Text[4], lobbyName = new TMP_Text[4], lobbyState = new TMP_Text[4], lobbyFaction = new TMP_Text[4];
        private readonly MenuShape[] lobbyStatePlate = new MenuShape[4];
        private readonly Image[] lobbyIcon = new Image[4];
        private readonly Button[] lobbyKick = new Button[4];
        private readonly Button[] lobbyFactionChoice = new Button[4];
        private RectTransform lobbyFactionRow;

        // Result
        private RectTransform resultScreen, resultVictory, resultDefeat;
        private TMP_Text resultMessage, resultHint;
        private readonly TMP_Text[] resultValues = new TMP_Text[5];
        private readonly TMP_Text[] resultStandings = new TMP_Text[4];
        private Button resultRetry, resultMenu;
        public bool IsResultOpen => resultScreen != null && resultScreen.gameObject.activeSelf;
        public bool IsResultVictory { get; private set; }
        public bool IsSettingsOpen => settingsPanel != null && settingsPanel.gameObject.activeSelf;

        private void BuildMenuSkin()
        {
            InitializeMenuFont();
            CreateMenuSprites();
            BuildMainMenuSkin();
            BuildFactionSkin();
            BuildLanSkin();
            BuildResultScreen();
        }

        #region Primitives
        private void InitializeMenuFont()
        {
            menuFont = font;
            try
            {
                if (Font.GetOSInstalledFontNames().Contains("Arial"))
                {
                    TMP_FontAsset arial = TMP_FontAsset.CreateFontAsset("Arial", "Bold", 90);
                    if (arial != null)
                    {
                        arial.name = "Pilgrimage Menu Arial";
                        // Arial has no Hangul; Korean glyphs come from the game's Korean font.
                        if (font != null) arial.fallbackFontAssetTable = new List<TMP_FontAsset> { font };
                        menuFont = arial; ownsMenuFont = true;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("Pilgrimage: Arial menu font unavailable, using default font. " + e.Message); }
        }

        private void CreateMenuSprites()
        {
            circleSprite = MenuSprite("Menu Circle", 128, TextureWrapMode.Clamp, (x, y) => Mathf.Clamp01((60f - Mathf.Sqrt(x * x + y * y)) * .9f));
            dotSprite = MenuSprite("Menu Halftone", 32, TextureWrapMode.Repeat, (x, y) => Mathf.Clamp01((7.5f - Mathf.Sqrt(x * x + y * y)) * .9f));
        }

        private Sprite MenuSprite(string name, int size, TextureWrapMode wrap, Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = wrap, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float half = (size - 1) * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha(x - half, y - half) * 255));
            texture.SetPixels32(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            menuAssets.Add(texture); menuAssets.Add(sprite);
            return sprite;
        }

        private void DestroyMenuSkinAssets()
        {
            foreach (UnityEngine.Object asset in menuAssets) if (asset != null) Destroy(asset);
            menuAssets.Clear();
            if (ownsMenuFont && menuFont != null)
            {
                if (menuFont.material != null) Destroy(menuFont.material);
                foreach (Texture2D atlas in menuFont.atlasTextures) if (atlas != null) Destroy(atlas);
                Destroy(menuFont);
            }
            menuFont = null; ownsMenuFont = false;
        }

        private static RectTransform MenuRect(Transform parent, string name, Vector2 position, Vector2 size, float rotation = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localEulerAngles = new Vector3(0, 0, rotation);
            return rect;
        }

        private static RectTransform MenuLayer(Transform parent, string name)
        {
            RectTransform rect = MenuRect(parent, name, Vector2.zero, Vector2.zero);
            Stretch(rect);
            return rect;
        }

        private static MenuShape MenuPlate(Transform parent, string name, Vector2 position, Vector2 size, Color color, Vector2[] points = null, float rotation = 0)
        {
            RectTransform rect = MenuRect(parent, name, position, size, rotation);
            if (rect.GetComponent<CanvasRenderer>() == null) rect.gameObject.AddComponent<CanvasRenderer>();
            var shape = rect.gameObject.AddComponent<MenuShape>();
            shape.Points = points ?? MenuShape.Rectangle; shape.color = color; shape.raycastTarget = false;
            return shape;
        }

        private TMP_Text MenuLabel(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, FontStyles style = FontStyles.Bold, float rotation = 0)
        {
            RectTransform rect = MenuRect(parent, name, position, size, rotation);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (menuFont != null) text.font = menuFont;
            text.text = value; text.fontSize = fontSize; text.color = color; text.alignment = alignment; text.fontStyle = style;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static Image MenuImage(Transform parent, string name, Vector2 position, Vector2 size, Sprite sprite, Color color, float rotation = 0)
        {
            RectTransform rect = MenuRect(parent, name, position, size, rotation);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false; image.preserveAspect = true;
            return image;
        }

        // Slanted plate button: black (or white) body, contrasting border, red fill pours in on hover/selection.
        private Button MenuButton(Transform parent, string name, string label, Vector2 position, Vector2 size, UnityAction action,
            PlateStyle style = PlateStyle.Dark, float rotation = 0, float fontSize = 34, float skewPixels = 22,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            RectTransform rect = MenuRect(parent, name, position, size, rotation);
            const float border = 5;
            Vector2 edgeSize = size + new Vector2(border * 2, border * 2);
            Color bodyColor = style == PlateStyle.Dark ? MenuBlack : MenuWhite, inkColor = style == PlateStyle.Dark ? MenuWhite : MenuBlack;
            MenuShape edge = MenuPlate(rect, "Edge", Vector2.zero, edgeSize, inkColor, MenuShape.Slant(skewPixels / edgeSize.x));
            MenuShape body = MenuPlate(rect, "Body", Vector2.zero, size, bodyColor, MenuShape.Slant(skewPixels / size.x));
            body.raycastTarget = true;
            MenuShape fill = MenuPlate(rect, "Hover Fill", Vector2.zero, size, MenuRed, MenuShape.Slant(skewPixels / size.x));
            fill.Fill = 0;
            TMP_Text text = MenuLabel(rect, "Label", label, alignment == TextAlignmentOptions.Left ? new Vector2(skewPixels * .5f, 0) : Vector2.zero,
                size - new Vector2(skewPixels * 2 + 20, 4), fontSize, inkColor, alignment);
            text.enableAutoSizing = true; text.fontSizeMax = fontSize; text.fontSizeMin = Mathf.Max(12, fontSize * .55f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = body; button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (action != null) button.onClick.AddListener(action);
            var hover = rect.gameObject.AddComponent<MenuHover>();
            hover.fillShape = fill;
            hover.AddTint(text, inkColor, MenuWhite);
            hover.AddTint(edge, inkColor, MenuWhite);
            return button;
        }

        private static void SetMenuLabel(Button button, string label)
        {
            var text = button != null ? button.transform.Find("Label")?.GetComponent<TMP_Text>() : null;
            if (text != null) text.text = label;
        }

        private static void PrepareMenuScreen(RectTransform screen)
        {
            foreach (Transform child in screen) child.gameObject.SetActive(false);
            var image = screen.GetComponent<Image>();
            if (image != null) { image.sprite = null; image.color = MenuBlack; }
            var outline = screen.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }

        private Sprite FactionIcon(FactionId faction)
        {
            int index = (int)faction;
            Sprite sprite = factionPortraitSprites != null && index >= 0 && index < factionPortraitSprites.Length ? factionPortraitSprites[index] : null;
            return sprite != null ? sprite : VisualAssets != null ? VisualAssets.Portrait(faction) : null;
        }

        private static readonly Vector2[] Octagon = {
            new Vector2(.3f, 0), new Vector2(0, .3f), new Vector2(0, .7f), new Vector2(.3f, 1),
            new Vector2(.7f, 1), new Vector2(1, .7f), new Vector2(1, .3f), new Vector2(.7f, 0) };
        #endregion

        #region Decoration
        private RectTransform MenuBackdrop(RectTransform screen, int variant)
        {
            RectTransform bg = MenuLayer(screen, "Menu Backdrop");
            var block = bg.gameObject.AddComponent<Image>();
            block.color = MenuBlack; block.raycastTarget = true;
            MenuPlate(bg, "Deep Slab", new Vector2(-260, -545), new Vector2(2800, 170), MenuDeepRed, MenuShape.Slant(.04f), 7);
            MenuPlate(bg, "Red Slab", new Vector2(-220, -455), new Vector2(2800, 210), MenuRed, MenuShape.Slant(.04f), 7);
            MenuPlate(bg, "Red Slab High", new Vector2(820, 530), new Vector2(1250, 150), MenuRed, MenuShape.Slant(.12f), -5);
            Dots(bg, new Vector2(-830, 420), new Vector2(460, 280), -8, .7f);
            Dots(bg, new Vector2(880, -380), new Vector2(520, 300), 9, .55f);
            Dots(bg, new Vector2(80, -520), new Vector2(700, 140), 7, .35f);
            Shard(bg, new Vector2(-860, -330), new Vector2(640, 26), 19);
            Shard(bg, new Vector2(860, -280), new Vector2(760, 22), 167);
            Shard(bg, new Vector2(-640, 505), new Vector2(560, 18), -8);
            Shard(bg, new Vector2(300, -395), new Vector2(520, 16), 8);
            if (variant == 0) { Sun(bg, new Vector2(420, 230), 640); Cathedral(bg, new Vector2(430, -420), 1.05f); }
            else if (variant == 2) { Sun(bg, new Vector2(760, 395), 300); Cathedral(bg, new Vector2(800, 230), .42f); }
            else if (variant == 3) { Sun(bg, new Vector2(-700, 160), 820); }
            return bg;
        }

        private static void Shard(Transform parent, Vector2 position, Vector2 size, float rotation)
            => MenuPlate(parent, "White Shard", position, size, MenuWhite, new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, .5f) }, rotation);

        private void Dots(Transform parent, Vector2 position, Vector2 size, float rotation, float alpha)
        {
            Image dots = MenuImage(parent, "Halftone", position, size, dotSprite, new Color(MenuRed.r, MenuRed.g, MenuRed.b, alpha), rotation);
            dots.preserveAspect = false; dots.type = Image.Type.Tiled;
        }

        private void Sun(Transform parent, Vector2 center, float diameter)
        {
            RectTransform sun = MenuRect(parent, "Red Sun", center, new Vector2(diameter, diameter));
            for (int i = 0; i < 16; i++)
            {
                float length = diameter * (i % 2 == 0 ? .78f : .64f);
                MenuShape ray = MenuPlate(sun, "Ray", Vector2.zero, new Vector2(diameter * (i % 2 == 0 ? .07f : .045f), length), MenuWhite, MenuShape.Triangle, i * 22.5f + 6);
                ray.rectTransform.pivot = new Vector2(.5f, 0); ray.rectTransform.anchoredPosition = Vector2.zero;
            }
            MenuImage(sun, "Disc Rim", Vector2.zero, new Vector2(diameter * 1.04f, diameter * 1.04f), circleSprite, MenuBlack);
            MenuImage(sun, "Disc", Vector2.zero, new Vector2(diameter, diameter), circleSprite, MenuRed);
        }

        // Black cathedral silhouette with a white rim, drawn in passes so rims never cut through bodies.
        private void Cathedral(Transform parent, Vector2 basePosition, float scale)
        {
            RectTransform group = MenuRect(parent, "Cathedral", basePosition, Vector2.zero);
            var parts = new List<(Vector2 p, Vector2 s, Vector2[] shape)>
            {
                (new Vector2(0, 30), new Vector2(1040, 170), new[] { new Vector2(0, 0), new Vector2(.12f, .5f), new Vector2(.36f, 1), new Vector2(.64f, 1), new Vector2(.88f, .5f), new Vector2(1, 0) }),
                (new Vector2(0, 175), new Vector2(460, 190), null),
                (new Vector2(0, 305), new Vector2(490, 90), MenuShape.Triangle),
                (new Vector2(-185, 255), new Vector2(92, 350), null), (new Vector2(-185, 480), new Vector2(118, 130), MenuShape.Triangle),
                (new Vector2(185, 255), new Vector2(92, 350), null), (new Vector2(185, 480), new Vector2(118, 130), MenuShape.Triangle),
                (new Vector2(-330, 165), new Vector2(64, 220), null), (new Vector2(-330, 315), new Vector2(80, 90), MenuShape.Triangle),
                (new Vector2(330, 165), new Vector2(64, 220), null), (new Vector2(330, 315), new Vector2(80, 90), MenuShape.Triangle),
                (new Vector2(0, 370), new Vector2(136, 460), null), (new Vector2(0, 690), new Vector2(160, 200), MenuShape.Triangle),
            };
            foreach (var part in parts) MenuPlate(group, "Rim", part.p * scale, part.s * scale + new Vector2(12, 12), MenuWhite, part.shape);
            foreach (var part in parts) MenuPlate(group, "Mass", part.p * scale, part.s * scale, MenuBlack, part.shape);
            MenuPlate(group, "Cross Vertical", new Vector2(0, 830) * scale, new Vector2(14, 110) * scale, MenuWhite);
            MenuPlate(group, "Cross Arm", new Vector2(0, 850) * scale, new Vector2(64, 14) * scale, MenuWhite);
            MenuPlate(group, "Window", new Vector2(0, 430) * scale, new Vector2(36, 86) * scale, MenuRed, new[] { new Vector2(0, 0), new Vector2(0, .75f), new Vector2(.5f, 1), new Vector2(1, .75f), new Vector2(1, 0) });
            MenuPlate(group, "Window L", new Vector2(-185, 290) * scale, new Vector2(24, 60) * scale, MenuRed);
            MenuPlate(group, "Window R", new Vector2(185, 290) * scale, new Vector2(24, 60) * scale, MenuRed);
            MenuImage(group, "Rose Window", new Vector2(0, 205) * scale, new Vector2(78, 78) * scale, circleSprite, MenuRed);
        }

        private void MenuTitle(Transform parent, Vector2 position, float scale, float rotation)
        {
            RectTransform title = MenuRect(parent, "Title Plate", position, new Vector2(820, 150) * scale, rotation);
            MenuPlate(title, "Red Edge", new Vector2(16, -14) * scale, new Vector2(840, 162) * scale, MenuRed, MenuShape.Slant(.07f));
            MenuPlate(title, "White Edge", Vector2.zero, new Vector2(836, 166) * scale, MenuWhite, MenuShape.Slant(.07f));
            MenuPlate(title, "Black Plate", Vector2.zero, new Vector2(816, 146) * scale, MenuBlack, MenuShape.Slant(.07f));
            TMP_Text text = MenuLabel(title, "Title", "PILGRIMAGE", new Vector2(0, 4) * scale, new Vector2(780, 140) * scale, 112 * scale, MenuWhite,
                TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
            text.characterSpacing = -4; text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void MenuSubtitle(Transform parent, string value, Vector2 position, Vector2 size, float fontSize, float rotation)
        {
            RectTransform plate = MenuRect(parent, "Subtitle Plate", position, size, rotation);
            MenuPlate(plate, "Shadow", new Vector2(10, -8), size, MenuBlack, MenuShape.Slant(.06f));
            MenuPlate(plate, "Plate", Vector2.zero, size, MenuWhite, MenuShape.Slant(.06f));
            MenuLabel(plate, "Subtitle", value, Vector2.zero, size - new Vector2(40, 4), fontSize, MenuBlack);
        }

        private void Crown(Transform parent, Vector2 position, float scale, Color color, float rotation, bool broken)
        {
            RectTransform crown = MenuRect(parent, "Crown", position, Vector2.zero, rotation);
            MenuPlate(crown, "Band", new Vector2(0, -50) * scale, new Vector2(250, 56) * scale, color);
            MenuPlate(crown, "Peak L", new Vector2(-82, 30) * scale, new Vector2(86, 110) * scale, color, MenuShape.Triangle, broken ? 18 : 0);
            MenuPlate(crown, "Peak C", new Vector2(0, broken ? 70 : 45) * scale, new Vector2(96, 150) * scale, color, MenuShape.Triangle, broken ? -12 : 0);
            MenuPlate(crown, "Peak R", new Vector2(broken ? 120 : 82, broken ? -10 : 30) * scale, new Vector2(86, 110) * scale, color, MenuShape.Triangle, broken ? -55 : 0);
            MenuImage(crown, "Jewel", new Vector2(0, -50) * scale, new Vector2(34, 34) * scale, circleSprite, MenuRed);
        }

        private RectTransform MenuFramedPanel(Transform parent, string name, Vector2 position, Vector2 size, float rotation)
        {
            RectTransform panel = MenuRect(parent, name, position, size, rotation);
            MenuPlate(panel, "Red Shadow", new Vector2(18, -16), size + new Vector2(10, 10), MenuRed, MenuShape.Slant(.02f));
            MenuPlate(panel, "White Edge", Vector2.zero, size + new Vector2(12, 12), MenuWhite, MenuShape.Slant(.02f));
            MenuPlate(panel, "Body", Vector2.zero, size, MenuBlack, MenuShape.Slant(.02f));
            return panel;
        }

        private void MenuTag(Transform parent, string value, Vector2 position, Vector2 size, float fontSize)
        {
            MenuPlate(parent, value + " Tag", position, size, MenuRed, MenuShape.Slant(.12f));
            MenuLabel(parent, value + " Tag Label", value, position, size - new Vector2(20, 4), fontSize, MenuWhite);
        }
        #endregion

        #region Main menu & settings
        private void BuildMainMenuSkin()
        {
            PrepareMenuScreen(modeScreen);
            RectTransform skin = MenuLayer(modeScreen, "Menu Skin"); skin.gameObject.AddComponent<MenuSkinRoot>();
            MenuBackdrop(skin, 0);
            MenuTitle(skin, new Vector2(-470, 330), 1, 5);
            string[] labels = { "게임 시작", "LAN 플레이", "설정", "게임 종료" };
            UnityAction[] actions = { SelectSinglePlayer, SelectLanMode, OpenSettings, QuitGame };
            for (int i = 0; i < labels.Length; i++)
                MenuButton(skin, "메인 · " + labels[i], "▶   " + labels[i], new Vector2(-575 - i * 24, 120 - i * 112), new Vector2(560, 90), actions[i],
                    PlateStyle.Dark, 3, 42, 26, TextAlignmentOptions.Left);
            menuFeedbackText = MenuLabel(skin, "Menu Feedback Skin", "", new Vector2(-520, -300), new Vector2(900, 60), 24, MenuWhite,
                TextAlignmentOptions.Left, FontStyles.Normal);
            BuildSettingsPanel(skin);
        }

        private void SetMenuFeedback(string reason)
        {
            var authored = modeScreen.Find("Menu Feedback")?.GetComponent<TMP_Text>();
            if (authored != null) authored.text = reason ?? "";
            if (menuFeedbackText != null) menuFeedbackText.text = reason ?? "";
        }

        private void BuildSettingsPanel(Transform parent)
        {
            settingsPanel = MenuLayer(parent, "Settings Panel");
            var dim = settingsPanel.gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, .72f); dim.raycastTarget = true;
            RectTransform box = MenuFramedPanel(settingsPanel, "Settings Box", new Vector2(0, -10), new Vector2(900, 560), 2);
            MenuTag(box, "설정", new Vector2(-300, 225), new Vector2(240, 76), 44);
            string[] rows = { "배경음", "효과음", "화면 모드" };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 100 - i * 105;
                MenuLabel(box, rows[i] + " Label", rows[i], new Vector2(-250, y), new Vector2(280, 60), 34, MenuWhite, TextAlignmentOptions.Left);
                if (i == 2) { screenModeButton = MenuButton(box, "화면 모드 전환", "", new Vector2(170, y), new Vector2(330, 66), ToggleScreenMode, PlateStyle.Dark, 0, 30, 14); continue; }
                bool bgm = i == 0;
                MenuButton(box, rows[i] + " -", "◀", new Vector2(40, y), new Vector2(90, 66), () => ChangeVolume(bgm, -.1f), PlateStyle.Dark, 0, 30, 12);
                TMP_Text value = MenuLabel(box, rows[i] + " Value", "", new Vector2(170, y), new Vector2(150, 60), 34, MenuWhite);
                MenuButton(box, rows[i] + " +", "▶", new Vector2(300, y), new Vector2(90, 66), () => ChangeVolume(bgm, .1f), PlateStyle.Dark, 0, 30, 12);
                if (bgm) bgmValue = value; else sfxValue = value;
            }
            MenuButton(box, "설정 닫기", "닫기", new Vector2(250, -215), new Vector2(260, 72), CloseSettings, PlateStyle.Light, 0, 34, 16);
            settingsPanel.gameObject.SetActive(false);
        }

        public void OpenSettings()
        {
            if (!IsModeSelectionOpen) return;
            RefreshSettings();
            settingsPanel.gameObject.SetActive(true); settingsPanel.SetAsLastSibling();
        }
        public void CloseSettings() { if (settingsPanel != null) settingsPanel.gameObject.SetActive(false); }
        private void ChangeVolume(bool bgm, float delta)
        {
            var audio = PilgrimageAudio.Instance;
            if (audio == null) return;
            if (bgm) audio.BgmVolume = Mathf.Round((audio.BgmVolume + delta) * 10) / 10;
            else audio.SfxVolume = Mathf.Round((audio.SfxVolume + delta) * 10) / 10;
            RefreshSettings();
        }
        private void ToggleScreenMode()
        {
            bool full = !Screen.fullScreen;
            Screen.fullScreen = full;
            SetMenuLabel(screenModeButton, full ? "전체 화면" : "창 모드");
        }
        private void RefreshSettings()
        {
            var audio = PilgrimageAudio.Instance;
            bgmValue.text = audio != null ? Mathf.RoundToInt(audio.BgmVolume * 100) + "%" : "-";
            sfxValue.text = audio != null ? Mathf.RoundToInt(audio.SfxVolume * 100) + "%" : "-";
            SetMenuLabel(screenModeButton, Screen.fullScreen ? "전체 화면" : "창 모드");
        }
        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        #endregion

        #region Faction selection
        private void BuildFactionSkin()
        {
            PrepareMenuScreen(factionSelection);
            RectTransform skin = MenuLayer(factionSelection, "Faction Skin"); skin.gameObject.AddComponent<MenuSkinRoot>();
            MenuBackdrop(skin, 1);
            MenuTitle(skin, new Vector2(-650, 445), .62f, 5);
            MenuSubtitle(skin, "이끌어 갈 교단을 선택하세요", new Vector2(-120, 420), new Vector2(720, 82), 38, 3);
            for (int i = 0; i < GameSession.PlayerCount; i++)
            {
                FactionId faction = (FactionId)(i + 1);
                Vector2 position = new Vector2(i % 2 == 0 ? -425 : 425, i < 2 ? 140 : -190);
                Button card = MenuButton(skin, "선택 · " + FactionSetup.Name(faction), "", position, new Vector2(790, 290), () => ChooseFaction(faction),
                    PlateStyle.Dark, 1.5f, 30, 34);
                Transform r = card.transform;
                MenuHover hover = card.GetComponent<MenuHover>();
                MenuPlate(r, "Icon Frame", new Vector2(-255, 8), new Vector2(214, 214), MenuWhite, Octagon);
                MenuImage(r, "Icon", new Vector2(-255, 8), new Vector2(200, 200), FactionIcon(faction), Color.white);
                MenuPlate(r, "Name Plate", new Vector2(85, 88), new Vector2(420, 74), MenuWhite, MenuShape.Slant(.08f));
                TMP_Text name = MenuLabel(r, "Name", FactionSetup.Name(faction), new Vector2(85, 88), new Vector2(390, 70), 40, MenuRed);
                hover.AddTint(name, MenuRed, MenuBlack);
                MenuLabel(r, "Description", FactionSetup.Description(faction), new Vector2(110, -12), new Vector2(470, 110), 25, MenuWhite,
                    TextAlignmentOptions.Left, FontStyles.Normal);
                MenuShape tag = MenuPlate(r, "Select Tag", new Vector2(268, -100), new Vector2(180, 60), MenuRed, MenuShape.Slant(.15f));
                hover.AddTint(tag, MenuRed, MenuBlack);
                MenuLabel(r, "Select Label", "선택", new Vector2(268, -100), new Vector2(160, 56), 32, MenuWhite);
            }
            MenuLabel(skin, "Faction Note", "선택한 교단은 내가, 나머지 세 교단은 AI가 담당합니다.", new Vector2(-300, -420), new Vector2(1100, 50), 26,
                MenuWhite, TextAlignmentOptions.Left, FontStyles.Normal, 3);
            MenuButton(skin, "교단 · 뒤로", "←  뒤로", new Vector2(720, -430), new Vector2(270, 76), () => ShowMainMenu(""), PlateStyle.Light, 3, 34, 20);
        }
        #endregion

        #region LAN
        private void BuildLanSkin()
        {
            PrepareMenuScreen(lanScreen);
            RectTransform skin = MenuLayer(lanScreen, "LAN Skin"); skin.gameObject.AddComponent<MenuSkinRoot>();
            MenuBackdrop(skin, 2);
            MenuTitle(skin, new Vector2(-650, 455), .62f, 5);
            MenuSubtitle(skin, "LAN 플레이", new Vector2(-600, 360), new Vector2(380, 70), 38, 3);
            lanHostCard = LanModeCard(skin, "방 만들기", "새로운 방을 만들고 호스트가 됩니다.", "＋", new Vector2(-590, 225), true);
            lanJoinCard = LanModeCard(skin, "방 참가", "IP와 포트로 기존 방에 참가합니다.", "→", new Vector2(-600, 75), false);
            var fields = new[] { ("이름", lanName), ("방 이름", lanRoomName), ("IP 주소", lanAddress), ("포트", lanPort) };
            for (int i = 0; i < fields.Length; i++)
            {
                float y = -45 - i * 64;
                MenuPlate(skin, fields[i].Item1 + " Plate", new Vector2(-805, y), new Vector2(160, 52), MenuWhite, MenuShape.Slant(.1f));
                MenuLabel(skin, fields[i].Item1 + " Label", fields[i].Item1, new Vector2(-805, y), new Vector2(140, 50), 26, MenuBlack);
                StyleLanInput(fields[i].Item2, skin, new Vector2(-520, y), new Vector2(390, 52));
            }
            lanConfirmButton = MenuButton(skin, "LAN 확인", "", new Vector2(-600, -330), new Vector2(560, 84), ConfirmLanChoice, PlateStyle.Light, 2, 38, 24);
            var status = lanStatus.rectTransform;
            status.SetParent(skin, false); status.gameObject.SetActive(true);
            status.anchorMin = status.anchorMax = status.pivot = new Vector2(.5f, .5f);
            status.anchoredPosition = new Vector2(-590, -440); status.sizeDelta = new Vector2(640, 110); status.localEulerAngles = Vector3.zero;
            if (menuFont != null) lanStatus.font = menuFont;
            lanStatus.color = MenuWhite; lanStatus.fontSize = 21; lanStatus.fontStyle = FontStyles.Normal; lanStatus.alignment = TextAlignmentOptions.TopLeft;
            lanStatus.enableAutoSizing = true; lanStatus.fontSizeMin = 14; lanStatus.fontSizeMax = 21;

            RectTransform panel = MenuFramedPanel(skin, "Lobby Panel", new Vector2(340, 20), new Vector2(1110, 700), 0);
            MenuTag(panel, "로비", new Vector2(-440, 300), new Vector2(190, 66), 42);
            lobbyCountText = MenuLabel(panel, "Lobby Count", "", new Vector2(-130, 300), new Vector2(380, 60), 32, MenuWhite, TextAlignmentOptions.Left);
            MenuLabel(panel, "Lobby Rule", "같은 교단 선택 시 무작위 배정", new Vector2(330, 300), new Vector2(400, 60), 23, MenuWhite, TextAlignmentOptions.Right, FontStyles.Normal);
            lobbyRoomText = MenuLabel(panel, "Lobby Room", "", new Vector2(-150, 248), new Vector2(760, 40), 21, MenuGrey, TextAlignmentOptions.Left, FontStyles.Normal);
            lanFirewallButton = MenuButton(panel, "방화벽 다시 설정", "방화벽 다시 설정", new Vector2(395, 248), new Vector2(270, 44), () => StartCoroutine(RetryFirewall()),
                PlateStyle.Dark, 0, 20, 10);
            for (int i = 0; i < 4; i++)
            {
                RectTransform row = MenuRect(panel, "Lobby Row " + i, new Vector2(0, 165 - i * 118), new Vector2(1050, 104));
                MenuPlate(row, "Row Edge", Vector2.zero, new Vector2(1050, 104), MenuWhite, MenuShape.Slant(.015f));
                MenuPlate(row, "Row Body", Vector2.zero, new Vector2(1040, 94), MenuBlack, MenuShape.Slant(.015f));
                MenuPlate(row, "Role Plate", new Vector2(-440, 0), new Vector2(130, 50), MenuWhite, MenuShape.Slant(.12f));
                lobbyRole[i] = MenuLabel(row, "Role", "", new Vector2(-440, 0), new Vector2(120, 48), 24, MenuBlack);
                MenuPlate(row, "Icon Frame", new Vector2(-320, 0), new Vector2(88, 88), MenuWhite, Octagon);
                lobbyIcon[i] = MenuImage(row, "Faction Icon", new Vector2(-320, 0), new Vector2(82, 82), null, Color.white);
                lobbyName[i] = MenuLabel(row, "Name", "", new Vector2(-50, 20), new Vector2(400, 46), 32, MenuWhite, TextAlignmentOptions.Left);
                lobbyName[i].textWrappingMode = TextWrappingModes.NoWrap; lobbyName[i].overflowMode = TextOverflowModes.Truncate;
                lobbyName[i].enableAutoSizing = true; lobbyName[i].fontSizeMin = 18; lobbyName[i].fontSizeMax = 32;
                lobbyStatePlate[i] = MenuPlate(row, "State Plate", new Vector2(-142, -24), new Vector2(220, 36), MenuRed, MenuShape.Slant(.1f));
                lobbyState[i] = MenuLabel(row, "State", "", new Vector2(-142, -24), new Vector2(210, 34), 21, MenuWhite);
                lobbyKick[i] = MenuButton(row, "내보내기 " + i, "내보내기", new Vector2(110, -6), new Vector2(150, 46), null, PlateStyle.Dark, 0, 21, 10);
                MenuPlate(row, "Faction Plate", new Vector2(345, 0), new Vector2(300, 58), MenuWhite, MenuShape.Slant(.08f));
                lobbyFaction[i] = MenuLabel(row, "Faction", "", new Vector2(345, 0), new Vector2(280, 54), 26, MenuBlack);
            }
            lobbyFactionRow = MenuRect(panel, "Faction Choice", new Vector2(0, -300), new Vector2(1050, 70));
            MenuLabel(lobbyFactionRow, "Faction Choice Label", "희망 교단", new Vector2(-445, 0), new Vector2(150, 50), 26, MenuWhite, TextAlignmentOptions.Left);
            for (int k = 0; k < 4; k++)
            {
                FactionId faction = (FactionId)(k + 1);
                lobbyFactionChoice[k] = MenuButton(lobbyFactionRow, "희망 · " + FactionSetup.Name(faction), FactionSetup.Name(faction),
                    new Vector2(-250 + k * 236, 0), new Vector2(222, 56), () => Network?.Send(new LanCommand { kind = LanCommandKind.Faction, faction = faction }),
                    PlateStyle.Dark, 0, 22, 12);
            }
            lobbyHintText = MenuLabel(panel, "Lobby Hint", "", new Vector2(0, -300), new Vector2(1050, 60), 25, MenuWhite);
            lanReadyButton = MenuButton(skin, "LAN 준비", "", new Vector2(-40, -450), new Vector2(360, 90), null, PlateStyle.Light, 3, 40, 24);
            lanStartButton = MenuButton(skin, "LAN 게임 시작", "▶  게임 시작", new Vector2(370, -450), new Vector2(420, 90),
                () => Network?.Send(new LanCommand { kind = LanCommandKind.Prepare }), PlateStyle.Dark, 3, 40, 24);
            lanBackButton = MenuButton(skin, "LAN 뒤로", "←  뒤로", new Vector2(770, -450), new Vector2(260, 82), LeaveLan, PlateStyle.Dark, 3, 34, 20);
            ApplyLanChoice();
            RenderLanIdle();
        }

        private MenuHover LanModeCard(Transform parent, string title, string description, string glyph, Vector2 position, bool host)
        {
            Button card = MenuButton(parent, "LAN 모드 · " + title, "", position, new Vector2(600, 136), () => { lanChooseHost = host; ApplyLanChoice(); },
                PlateStyle.Dark, 2, 30, 26);
            Transform r = card.transform;
            MenuHover hover = card.GetComponent<MenuHover>();
            MenuShape iconPlate = MenuPlate(r, "Icon Plate", new Vector2(-205, 0), new Vector2(120, 96), MenuWhite, MenuShape.Slant(.1f));
            TMP_Text icon = MenuLabel(r, "Icon", glyph, new Vector2(-205, 2), new Vector2(110, 92), 64, MenuBlack);
            hover.AddTint(icon, MenuBlack, MenuRed);
            MenuLabel(r, "Title", title, new Vector2(70, 22), new Vector2(400, 60), 46, MenuWhite, TextAlignmentOptions.Left);
            MenuLabel(r, "Description", description, new Vector2(70, -34), new Vector2(400, 40), 21, MenuWhite, TextAlignmentOptions.Left, FontStyles.Normal);
            return hover;
        }

        private void StyleLanInput(TMP_InputField input, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)input.transform;
            rect.SetParent(parent, false); rect.gameObject.SetActive(true);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localEulerAngles = Vector3.zero;
            var image = input.GetComponent<Image>();
            if (image != null) { image.sprite = null; image.color = MenuWhite; }
            var outline = input.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            foreach (TMP_Text text in new[] { input.textComponent, input.placeholder as TMP_Text })
            {
                if (text == null) continue;
                if (menuFont != null) text.font = menuFont;
                text.rectTransform.sizeDelta = size - new Vector2(26, 6);
                text.fontSize = 26; text.fontStyle = text == input.textComponent ? FontStyles.Bold : FontStyles.Italic;
                text.color = text == input.textComponent ? MenuBlack : MenuGrey;
            }
            input.customCaretColor = true; input.caretColor = MenuBlack; input.selectionColor = new Color(MenuRed.r, MenuRed.g, MenuRed.b, .35f);
        }

        private void ConfirmLanChoice()
        {
            if (!IsLanMode || Network != null || firewallBusy || Game != null) return;
            StartLan(lanChooseHost);
        }

        private void ApplyLanChoice()
        {
            if (lanHostCard == null) return;
            lanHostCard.Selected = lanChooseHost; lanJoinCard.Selected = !lanChooseHost;
            SetMenuLabel(lanConfirmButton, lanChooseHost ? "√  확인 · 방 만들기" : "√  확인 · 방 참가");
            if (Network == null)
            {
                // Host listens on its own LAN addresses; only joining needs the host IPv4.
                lanAddress.interactable = !lanChooseHost;
                lanRoomName.interactable = lanChooseHost;
                lanName.interactable = lanPort.interactable = true;
            }
        }

        // Before a room exists: show four empty seats; the room is created/joined with the left 확인 button.
        private void RenderLanIdle()
        {
            lobbyCountText.text = "현재 인원  0 / 4";
            lobbyRoomText.text = "왼쪽에서 방 만들기 또는 방 참가를 고른 뒤 확인을 누르세요.";
            for (int i = 0; i < 4; i++)
            {
                lobbyRole[i].text = "—"; lobbyName[i].text = "빈자리"; lobbyFaction[i].text = "—";
                lobbyState[i].text = "□ 대기"; lobbyState[i].color = MenuBlack; lobbyStatePlate[i].color = MenuWhite;
                lobbyIcon[i].enabled = false; lobbyKick[i].gameObject.SetActive(false);
            }
            lobbyFactionRow.gameObject.SetActive(true); lobbyHintText.gameObject.SetActive(false);
            foreach (Button choice in lobbyFactionChoice) { choice.interactable = false; choice.GetComponent<MenuHover>().Selected = false; }
            SetMenuLabel(lanReadyButton, "√  준비"); Wire(lanReadyButton, () => { });
            lanReadyButton.interactable = false; lanStartButton.interactable = false; lanStartButton.gameObject.SetActive(true);
            lanFirewallButton.gameObject.SetActive(false);
            ApplyLanChoice();
        }

        private void RenderLobbySkin(LanLobbyView lobby, string error)
        {
            string endpoint = Network.Address + ":" + Network.Port;
            bool assignment = lobby.phase == LanPhase.Assignment;
            lobbyCountText.text = "현재 인원  " + lobby.members.Length + " / 4";
            lobbyRoomText.text = lobby.room + "  ·  " + endpoint;
            var mine = lobby.members.FirstOrDefault(m => m.connection == lobby.localConnection);
            for (int i = 0; i < 4; i++)
            {
                var member = i < lobby.members.Length ? lobby.members[i] : null;
                bool done = member != null && (assignment ? member.confirmed : member.ready);
                lobbyRole[i].text = member == null ? "AI" : i == 0 ? "호스트" : "참가자";
                lobbyName[i].text = member == null ? "빈자리 · AI 자동" : member.name + (member.connection == lobby.localConnection ? " (나)" : "");
                lobbyState[i].text = member == null ? "AI" : assignment ? (done ? "√ 확인 완료" : "□ 확인 대기") : (done ? "√ 준비 완료" : "□ 준비 대기");
                lobbyStatePlate[i].color = done ? MenuRed : MenuWhite;
                lobbyState[i].color = done ? MenuWhite : MenuBlack;
                FactionId shown = assignment ? lobby.assignment[i] : member != null ? member.desired : FactionId.None;
                lobbyFaction[i].text = assignment || member != null ? FactionSetup.Name(shown) : "시작 시 배정";
                lobbyIcon[i].sprite = FactionSetup.IsPlayable(shown) ? FactionIcon(shown) : null;
                lobbyIcon[i].enabled = lobbyIcon[i].sprite != null;
                bool kick = lobby.host && !assignment && member != null && member.connection != lobby.localConnection && ulong.TryParse(member.connection, out _);
                lobbyKick[i].gameObject.SetActive(kick);
                if (kick) { ulong id = ulong.Parse(member.connection); Wire(lobbyKick[i], () => Network?.Kick(id)); }
            }
            lobbyFactionRow.gameObject.SetActive(!assignment);
            lobbyHintText.gameObject.SetActive(assignment);
            lobbyHintText.text = assignment ? "최종 교단 배정 · 시드 " + lobby.seed + "  —  모두 확인하면 게임을 시작합니다." : "";
            for (int k = 0; k < 4; k++)
            {
                lobbyFactionChoice[k].interactable = !assignment && mine != null;
                lobbyFactionChoice[k].GetComponent<MenuHover>().Selected = mine != null && mine.desired == (FactionId)(k + 1);
            }
            if (assignment)
            {
                SetMenuLabel(lanReadyButton, mine != null && mine.confirmed ? "√  확인 완료" : "√  배정 확인");
                lanReadyButton.interactable = mine != null && !mine.confirmed;
                Wire(lanReadyButton, () => Network?.Send(new LanCommand { kind = LanCommandKind.ConfirmAssignment }));
            }
            else
            {
                bool ready = mine != null && mine.ready;
                SetMenuLabel(lanReadyButton, ready ? "√  준비 취소" : "√  준비");
                lanReadyButton.interactable = mine != null;
                Wire(lanReadyButton, () => Network?.Send(new LanCommand { kind = LanCommandKind.Ready, ready = !ready }));
            }
            lanStartButton.gameObject.SetActive(lobby.host && !assignment);
            lanStartButton.interactable = lobby.members.Length >= 2 && lobby.members.All(m => m.ready);
            lanFirewallButton.gameObject.SetActive(lobby.host && firewallState != LanFirewallState.Unsupported && firewallState != LanFirewallState.Ready);
        }

        // Per-frame enable state of the room create/join controls (connection attempts are asynchronous).
        private void UpdateLanSkin()
        {
            if (lanConfirmButton == null || !lanScreen.gameObject.activeInHierarchy) return;
            bool idle = Network == null && !firewallBusy;
            lanConfirmButton.interactable = idle;
            lanHostCard.GetComponent<Button>().interactable = idle;
            lanJoinCard.GetComponent<Button>().interactable = idle;
        }
        #endregion

        #region Result
        private void BuildResultScreen()
        {
            resultScreen = MenuLayer(root, "Result Screen"); resultScreen.gameObject.AddComponent<MenuSkinRoot>();
            MenuBackdrop(resultScreen, 3);
            resultVictory = MenuLayer(resultScreen, "Victory");
            MenuTitle(resultVictory, new Vector2(-650, 455), .62f, 5);
            Crown(resultVictory, new Vector2(-170, 330), 1, MenuWhite, -8, false);
            MenuPlate(resultVictory, "Victory Shadow", new Vector2(-400, 175), new Vector2(600, 200), MenuBlack, MenuShape.Slant(.1f), 4);
            MenuPlate(resultVictory, "Victory Edge", new Vector2(-420, 190), new Vector2(616, 216), MenuWhite, MenuShape.Slant(.1f), 4);
            MenuPlate(resultVictory, "Victory Plate", new Vector2(-420, 190), new Vector2(600, 200), MenuRed, MenuShape.Slant(.1f), 4);
            MenuLabel(resultVictory, "Victory Word", "승리", new Vector2(-420, 196), new Vector2(560, 190), 160, MenuWhite,
                TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic, 4);
            MenuLabel(resultVictory, "Victory Latin", "VICTORY", new Vector2(-300, 62), new Vector2(420, 56), 46, MenuWhite,
                TextAlignmentOptions.Right, FontStyles.Bold | FontStyles.Italic, 4);

            resultDefeat = MenuLayer(resultScreen, "Defeat");
            Crown(resultDefeat, new Vector2(-180, 360), .8f, MenuGrey, -28, true);
            MenuLabel(resultDefeat, "Defeat Shadow", "DEFEAT", new Vector2(-418, 222), new Vector2(980, 260), 230, MenuRed,
                TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic, 6).textWrappingMode = TextWrappingModes.NoWrap;
            MenuLabel(resultDefeat, "Defeat Word", "DEFEAT", new Vector2(-430, 236), new Vector2(980, 260), 230, MenuWhite,
                TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic, 6).textWrappingMode = TextWrappingModes.NoWrap;
            MenuPlate(resultDefeat, "Defeat Plate", new Vector2(-300, 70), new Vector2(240, 86), MenuRed, MenuShape.Slant(.12f), 6);
            MenuLabel(resultDefeat, "Defeat Label", "패배", new Vector2(-300, 72), new Vector2(220, 80), 54, MenuWhite, TextAlignmentOptions.Center, FontStyles.Bold, 6);

            MenuPlate(resultScreen, "Message Plate", new Vector2(-430, -70), new Vector2(800, 70), MenuWhite, MenuShape.Slant(.04f), 3);
            resultMessage = MenuLabel(resultScreen, "Result Message", "", new Vector2(-430, -68), new Vector2(740, 64), 28, MenuBlack,
                TextAlignmentOptions.Center, FontStyles.Bold, 3);

            RectTransform panel = MenuFramedPanel(resultScreen, "Summary Panel", new Vector2(480, 50), new Vector2(740, 660), -2);
            MenuTag(panel, "결과 요약", new Vector2(-200, 280), new Vector2(280, 66), 36);
            string[] labels = { "최종 라운드", "내 신도 수", "신앙심", "교리 정합성", "헌금" };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = 200 - i * 52;
                MenuLabel(panel, labels[i] + " Label", labels[i], new Vector2(-150, y), new Vector2(380, 46), 28, MenuWhite, TextAlignmentOptions.Left, FontStyles.Normal);
                resultValues[i] = MenuLabel(panel, labels[i] + " Value", "", new Vector2(200, y), new Vector2(240, 46), 32, MenuWhite, TextAlignmentOptions.Right);
            }
            MenuPlate(panel, "Divider", new Vector2(0, -70), new Vector2(660, 4), MenuWhite);
            MenuTag(panel, "최종 순위", new Vector2(-220, -120), new Vector2(240, 56), 30);
            for (int i = 0; i < 4; i++)
                resultStandings[i] = MenuLabel(panel, "Standing " + i, "", new Vector2(10, -175 - i * 44), new Vector2(640, 42), 26, MenuWhite,
                    TextAlignmentOptions.Left, FontStyles.Bold);

            resultRetry = MenuButton(resultScreen, "결과 · 다시 하기", "", new Vector2(-530, -230), new Vector2(480, 92), OnResultRetry, PlateStyle.Light, 3, 40, 24);
            resultMenu = MenuButton(resultScreen, "결과 · 메인 메뉴", "←  메인 메뉴로", new Vector2(-555, -345), new Vector2(480, 88), OnResultMenu, PlateStyle.Dark, 3, 36, 24);
            resultHint = MenuLabel(resultScreen, "Result Hint", "", new Vector2(-520, -440), new Vector2(860, 50), 23, MenuWhite, TextAlignmentOptions.Left, FontStyles.Normal, 3);
            resultScreen.gameObject.SetActive(false);
        }

        private void ShowResultScreen()
        {
            resultShownForMatch = true;
            int seat = IsLanMode ? LocalSeat : 0;
            string[] names; int[] followers; int round, faith, doctrine, offering; int[] winners;
            if (IsLanMode)
            {
                names = LanView.players.Select(p => p.name).ToArray(); followers = LanView.players.Select(p => p.followers).ToArray();
                var me = LanView.players[seat]; faith = me.faith; doctrine = me.doctrine; offering = me.offering; round = LanView.round;
                winners = LanView.winners ?? Array.Empty<int>();
            }
            else
            {
                names = Game.Players.Select(p => p.Name).ToArray(); followers = Game.Players.Select(p => p.FollowerCount).ToArray();
                var me = Game.Players[0]; faith = me.Faith; doctrine = me.Doctrine; offering = me.Offering; round = Game.Round;
                winners = Game.Winners.ToArray();
            }
            bool victory = winners.Contains(seat);
            IsResultVictory = victory;
            resultVictory.gameObject.SetActive(victory); resultDefeat.gameObject.SetActive(!victory);
            resultMessage.text = victory
                ? (winners.Length > 1 ? "공동 1위! 순례의 길을 끝까지 걸었습니다." : "당신의 교단이 순례의 길에서 승리했습니다.")
                : string.Join(" · ", winners.Select(i => names[i])) + " 교단이 승리했습니다.";
            string[] values = { round + " / " + GameSession.TotalRounds, followers[seat].ToString(), faith.ToString(), doctrine.ToString(), offering.ToString() };
            for (int i = 0; i < values.Length; i++) resultValues[i].text = values[i];
            int[] order = Enumerable.Range(0, followers.Length).OrderByDescending(i => followers[i]).ThenBy(i => i).ToArray();
            for (int k = 0; k < resultStandings.Length; k++)
            {
                int i = order[k], rank = 1 + followers.Count(f => f > followers[i]);
                resultStandings[k].text = rank + "위   " + names[i] + (i == seat ? "  (나)" : "") + "   ·   신도 " + followers[i];
                resultStandings[k].color = winners.Contains(i) ? MenuRed : MenuWhite;
            }
            if (IsLanMode)
            {
                bool host = Network != null && Network.IsHost;
                SetMenuLabel(resultRetry, host ? "▶  다시 하기 · 로비로" : "호스트 재경기 대기");
                resultRetry.interactable = host;
                resultHint.text = host ? "다시 하기를 누르면 모두 로비로 돌아갑니다. 전원 준비 후 새 게임을 시작하세요."
                    : "호스트가 다시 하기를 고르면 로비로 돌아갑니다.";
            }
            else
            {
                SetMenuLabel(resultRetry, "▶  다시 하기");
                resultRetry.interactable = true;
                resultHint.text = "같은 교단(" + FactionSetup.Name(Game.HumanFaction) + ")으로 새 게임을 시작합니다.";
            }
            CloseDetail(); CloseCardPicker();
            resultScreen.gameObject.SetActive(true);
            resultScreen.SetAsLastSibling();
        }

        private void HideResultScreen() { if (resultScreen != null) resultScreen.gameObject.SetActive(false); }
        #endregion
    }
}
