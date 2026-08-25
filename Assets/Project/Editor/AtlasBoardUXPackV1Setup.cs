#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AtlasBoardUXPackV1Setup
{
    private const string CanvasName =
        "Canvas_UXOverlay";

    private const string HudRootName =
        "HUDRoot";

    private const string StatusBarName =
        "StatusBar";

    [MenuItem(
        "Atlas Board/UX/Build or Refresh UX Pack v1")]
    public static void BuildOrRefresh()
    {
        TurnManager turnManager =
            Object.FindAnyObjectByType<
                TurnManager>();

        TileResolutionManager
            tileResolutionManager =
                Object.FindAnyObjectByType<
                    TileResolutionManager>();

        TradeManager tradeManager =
            Object.FindAnyObjectByType<
                TradeManager>();

        AuctionManager auctionManager =
            Object.FindAnyObjectByType<
                AuctionManager>();

        EventCardManager eventCardManager =
            Object.FindAnyObjectByType<
                EventCardManager>();

        SpecialTileManager specialTileManager =
            Object.FindAnyObjectByType<
                SpecialTileManager>();

        TabletUIManager tabletUIManager =
            Object.FindAnyObjectByType<
                TabletUIManager>();

        BoardCameraController cameraController =
            Object.FindAnyObjectByType<
                BoardCameraController>();

        PlayerGameState[] players =
            Object.FindObjectsByType<
                    PlayerGameState>()
                .Where(
                    player =>
                        player != null)
                .OrderBy(
                    player =>
                        player.PlayerSlotIndex)
                .ToArray();

        if (turnManager == null ||
            players.Length < 2)
        {
            Debug.LogError(
                "UX Pack v1 requires TurnManager and at least " +
                "two PlayerGameState objects in the open scene.");

            return;
        }

        GameObject canvasObject =
            FindOrCreateCanvas();

        RectTransform canvasRect =
            canvasObject.GetComponent<
                RectTransform>();

        UXKeyboardShortcutController
            shortcutController =
                GetOrAddComponent<
                    UXKeyboardShortcutController>(
                        canvasObject);

        UXOverlayController overlayController =
            GetOrAddComponent<
                UXOverlayController>(
                    canvasObject);

        Transform hudRoot =
            FindOrCreateRectChild(
                canvasRect,
                HudRootName);

        StretchFull(
            hudRoot as RectTransform);

        PlayerHudPanel[] hudPanels =
            BuildHudPanels(
                hudRoot,
                4);

        Transform statusBar =
            BuildStatusBar(
                canvasRect,
                out TMP_Text statusText,
                out TMP_Text hintText);

        BuildCameraResetButton(
            statusBar as RectTransform,
            cameraController);

        TMP_Text legacyTurnStatus =
            GetSerializedReference<
                TMP_Text>(
                    turnManager,
                    "turnStatusText");

        TMP_Text legacyBalances =
            tileResolutionManager != null
                ? GetSerializedReference<
                    TMP_Text>(
                        tileResolutionManager,
                        "balancesText")
                : null;

        GameObject triplePenaltyPanel =
            GetSerializedReference<
                GameObject>(
                    turnManager,
                    "tripleDoublePenaltyPanel");

        GameObject tabletRoot =
            tabletUIManager != null
                ? GetSerializedReference<
                    GameObject>(
                        tabletUIManager,
                        "tabletRoot")
                : null;

        shortcutController
            .EditorConfigure(
                turnManager,
                tileResolutionManager,
                tradeManager,
                auctionManager,
                eventCardManager,
                specialTileManager,
                triplePenaltyPanel);

        overlayController
            .EditorConfigure(
                turnManager,
                players,
                hudPanels,
                statusBar as RectTransform,
                statusText,
                hintText,
                shortcutController,
                tabletRoot,
                legacyTurnStatus,
                legacyBalances);

        if (legacyTurnStatus != null)
        {
            legacyTurnStatus
                .gameObject
                .SetActive(false);

            EditorUtility.SetDirty(
                legacyTurnStatus.gameObject);
        }

        if (legacyBalances != null)
        {
            legacyBalances
                .gameObject
                .SetActive(false);

            EditorUtility.SetDirty(
                legacyBalances.gameObject);
        }

        if (tabletUIManager != null)
        {
            ApplyTabletPolish(
                tabletUIManager);
        }
        else
        {
            Debug.LogWarning(
                "TabletUIManager was not found. " +
                "HUD/shortcuts were built, tablet polish was skipped.");
        }

        EditorUtility.SetDirty(
            canvasObject);

        EditorUtility.SetDirty(
            shortcutController);

        EditorUtility.SetDirty(
            overlayController);

        if (canvasObject.scene.IsValid())
        {
            EditorSceneManager
                .MarkSceneDirty(
                    canvasObject.scene);
        }

        Selection.activeGameObject =
            canvasObject;

        Debug.Log(
            "UX Pack v1 built/refreshed. " +
            "Corner player HUD, board-center status cleanup, " +
            "keyboard shortcuts, status bar and tablet shell " +
            "are ready. Save the scene.");
    }

    private static GameObject
        FindOrCreateCanvas()
    {
        GameObject existing =
            GameObject.Find(
                CanvasName);

        if (existing != null)
        {
            EnsureCanvasComponents(
                existing);

            return existing;
        }

        GameObject canvasObject =
            new GameObject(
                CanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        Canvas canvas =
            canvasObject
                .GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 200;

        CanvasScaler scaler =
            canvasObject
                .GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler
                .ScaleMode
                .ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(
                1920f,
                1080f);

        scaler.screenMatchMode =
            CanvasScaler
                .ScreenMatchMode
                .MatchWidthOrHeight;

        scaler.matchWidthOrHeight =
            0.5f;

        Undo.RegisterCreatedObjectUndo(
            canvasObject,
            "Build Atlas Board UX Pack v1");

        return canvasObject;
    }

    private static void EnsureCanvasComponents(
        GameObject canvasObject)
    {
        Canvas canvas =
            GetOrAddComponent<Canvas>(
                canvasObject);

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 200;

        CanvasScaler scaler =
            GetOrAddComponent<
                CanvasScaler>(
                    canvasObject);

        scaler.uiScaleMode =
            CanvasScaler
                .ScaleMode
                .ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(
                1920f,
                1080f);

        scaler.screenMatchMode =
            CanvasScaler
                .ScreenMatchMode
                .MatchWidthOrHeight;

        scaler.matchWidthOrHeight =
            0.5f;

        GetOrAddComponent<
            GraphicRaycaster>(
                canvasObject);
    }

    private static PlayerHudPanel[]
        BuildHudPanels(
            Transform parent,
            int panelCount)
    {
        PlayerHudPanel[] panels =
            new PlayerHudPanel[
                panelCount];

        for (int index = 0;
             index < panelCount;
             index++)
        {
            string panelName =
                $"HUD_Player_{index + 1}";

            Transform existing =
                parent.Find(
                    panelName);

            if (existing != null)
            {
                Object.DestroyImmediate(
                    existing.gameObject);
            }

            GameObject panelObject =
                new GameObject(
                    panelName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Outline),
                    typeof(PlayerHudPanel));

            RectTransform panelRect =
                panelObject
                    .GetComponent<RectTransform>();

            panelRect.SetParent(
                parent,
                false);

            panelRect.sizeDelta =
                new Vector2(
                    330f,
                    104f);

            Image background =
                panelObject
                    .GetComponent<Image>();

            background.color =
                new Color(
                    0.055f,
                    0.065f,
                    0.085f,
                    0.90f);

            Outline outline =
                panelObject
                    .GetComponent<Outline>();

            outline.effectColor =
                new Color(
                    1f,
                    1f,
                    1f,
                    0.65f);

            outline.effectDistance =
                new Vector2(
                    2f,
                    -2f);

            outline.enabled = false;

            Image accent =
                CreateImage(
                    panelRect,
                    "Accent",
                    new Vector2(
                        0f,
                        0f),
                    new Vector2(
                        0f,
                        1f),
                    new Vector2(
                        0f,
                        0.5f),
                    new Vector2(
                        8f,
                        0f),
                    new Vector2(
                        4f,
                        0f));

            Image icon =
                CreateImage(
                    panelRect,
                    "Icon",
                    new Vector2(
                        0f,
                        0.5f),
                    new Vector2(
                        0f,
                        0.5f),
                    new Vector2(
                        0f,
                        0.5f),
                    new Vector2(
                        64f,
                        64f),
                    new Vector2(
                        20f,
                        0f));

            TMP_Text iconText =
                CreateText(
                    icon.rectTransform,
                    "IconText",
                    "P1",
                    22f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Center);

            iconText.color =
                Color.white;

            TMP_Text nameText =
                CreateAnchoredText(
                    panelRect,
                    "PlayerName",
                    "Oyuncu",
                    26f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Left,
                    new Vector2(
                        105f,
                        -14f),
                    new Vector2(
                        205f,
                        32f));

            TMP_Text moneyText =
                CreateAnchoredText(
                    panelRect,
                    "Money",
                    "1500 ₵",
                    25f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Left,
                    new Vector2(
                        105f,
                        -48f),
                    new Vector2(
                        205f,
                        30f));

            moneyText.color =
                new Color(
                    0.95f,
                    0.95f,
                    0.82f,
                    1f);

            TMP_Text controlText =
                CreateAnchoredText(
                    panelRect,
                    "ControlType",
                    "İNSAN",
                    14f,
                    FontStyles.Normal,
                    TextAlignmentOptions.Left,
                    new Vector2(
                        105f,
                        -78f),
                    new Vector2(
                        205f,
                        20f));

            controlText.color =
                new Color(
                    0.72f,
                    0.76f,
                    0.83f,
                    1f);

            GameObject badgeRoot =
                new GameObject(
                    "TurnBadge",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            RectTransform badgeRect =
                badgeRoot
                    .GetComponent<RectTransform>();

            badgeRect.SetParent(
                panelRect,
                false);

            badgeRect.anchorMin =
                new Vector2(
                    1f,
                    1f);

            badgeRect.anchorMax =
                new Vector2(
                    1f,
                    1f);

            badgeRect.pivot =
                new Vector2(
                    1f,
                    1f);

            badgeRect.sizeDelta =
                new Vector2(
                    58f,
                    24f);

            badgeRect.anchoredPosition =
                new Vector2(
                    -8f,
                    -8f);

            Image badgeImage =
                badgeRoot
                    .GetComponent<Image>();

            TMP_Text badgeText =
                CreateText(
                    badgeRect,
                    "Text",
                    "SIRA",
                    12f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Center);

            badgeText.color =
                Color.white;

            PlayerHudPanel panel =
                panelObject
                    .GetComponent<
                        PlayerHudPanel>();

            panel.EditorConfigure(
                panelRect,
                background,
                accent,
                icon,
                iconText,
                nameText,
                moneyText,
                controlText,
                badgeRoot,
                badgeImage,
                badgeText,
                outline);

            panels[index] = panel;
        }

        return panels;
    }

    private static Transform BuildStatusBar(
        RectTransform canvasRect,
        out TMP_Text statusText,
        out TMP_Text hintText)
    {
        Transform existing =
            canvasRect.Find(
                StatusBarName);

        if (existing != null)
        {
            Object.DestroyImmediate(
                existing.gameObject);
        }

        GameObject statusObject =
            new GameObject(
                StatusBarName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));

        RectTransform rect =
            statusObject
                .GetComponent<RectTransform>();

        rect.SetParent(
            canvasRect,
            false);

        rect.anchorMin =
            new Vector2(
                0.5f,
                1f);

        rect.anchorMax =
            new Vector2(
                0.5f,
                1f);

        rect.pivot =
            new Vector2(
                0.5f,
                1f);

        rect.sizeDelta =
            new Vector2(
                760f,
                78f);

        rect.anchoredPosition =
            new Vector2(
                0f,
                -28f);

        Image image =
            statusObject
                .GetComponent<Image>();

        image.color =
            new Color(
                0.045f,
                0.055f,
                0.075f,
                0.88f);

        image.raycastTarget =
            false;

        Outline outline =
            statusObject
                .GetComponent<Outline>();

        outline.effectColor =
            new Color(
                0.15f,
                0.22f,
                0.32f,
                0.8f);

        outline.effectDistance =
            new Vector2(
                2f,
                -2f);

        statusText =
            CreateAnchoredText(
                rect,
                "StatusText",
                "Oyun durumu",
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Vector2(
                    18f,
                    -10f),
                new Vector2(
                    724f,
                    30f));

        hintText =
            CreateAnchoredText(
                rect,
                "ShortcutHint",
                "",
                15f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Vector2(
                    18f,
                    -43f),
                new Vector2(
                    724f,
                    22f));

        hintText.color =
            new Color(
                0.68f,
                0.78f,
                0.93f,
                1f);

        return statusObject.transform;
    }

    private static void BuildCameraResetButton(
        RectTransform statusBarRect,
        BoardCameraController cameraController)
    {
        if (statusBarRect == null)
        {
            return;
        }

        Transform oldButton =
            statusBarRect.Find(
                "CameraResetButton");

        if (oldButton != null)
        {
            Object.DestroyImmediate(
                oldButton.gameObject);
        }

        GameObject buttonObject =
            new GameObject(
                "CameraResetButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(Outline),
                typeof(UXCameraResetButton));

        RectTransform rect =
            buttonObject
                .GetComponent<RectTransform>();

        rect.SetParent(
            statusBarRect,
            false);

        rect.anchorMin =
            new Vector2(
                1f,
                0.5f);

        rect.anchorMax =
            new Vector2(
                1f,
                0.5f);

        rect.pivot =
            new Vector2(
                0f,
                0.5f);

        rect.sizeDelta =
            new Vector2(
                48f,
                48f);

        rect.anchoredPosition =
            new Vector2(
                18f,
                0f);

        Image background =
            buttonObject
                .GetComponent<Image>();

        background.color =
            new Color(
                0.075f,
                0.09f,
                0.12f,
                0.96f);

        Button button =
            buttonObject
                .GetComponent<Button>();

        ColorBlock colors =
            button.colors;

        colors.normalColor =
            Color.white;

        colors.highlightedColor =
            new Color(
                0.88f,
                0.93f,
                1f,
                1f);

        colors.pressedColor =
            new Color(
                0.72f,
                0.82f,
                0.96f,
                1f);

        button.colors =
            colors;

        Outline outline =
            buttonObject
                .GetComponent<Outline>();

        outline.effectColor =
            new Color(
                0.15f,
                0.22f,
                0.32f,
                0.95f);

        outline.effectDistance =
            new Vector2(
                2f,
                -2f);

        GameObject iconObject =
            new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform iconRect =
            iconObject
                .GetComponent<RectTransform>();

        iconRect.SetParent(
            rect,
            false);

        iconRect.anchorMin =
            Vector2.zero;

        iconRect.anchorMax =
            Vector2.one;

        iconRect.offsetMin =
            new Vector2(
                5f,
                5f);

        iconRect.offsetMax =
            new Vector2(
                -5f,
                -5f);

        Image iconImage =
            iconObject
                .GetComponent<Image>();

        iconImage.raycastTarget =
            false;

        iconImage.preserveAspect =
            true;

        Sprite resetSprite =
            FindResetCameraSprite();

        if (resetSprite != null)
        {
            iconImage.sprite =
                resetSprite;

            iconImage.color =
                Color.white;
        }
        else
        {
            iconImage.color =
                new Color(
                    0.25f,
                    0.35f,
                    0.50f,
                    1f);

            Debug.LogWarning(
                "UX Pack could not find a Sprite named " +
                "'ic_reset_camera'. The camera reset button " +
                "was created, but its icon is using a placeholder. " +
                "Import the PNG as Sprite (2D and UI) and run " +
                "Build or Refresh UX Pack v1 again.");
        }

        UXCameraResetButton reset =
            buttonObject
                .GetComponent<
                    UXCameraResetButton>();

        reset.EditorConfigure(
            button,
            cameraController,
            iconImage);
    }

    private static Sprite FindResetCameraSprite()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "ic_reset_camera t:Sprite");

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase
                    .GUIDToAssetPath(guid);

            Sprite sprite =
                AssetDatabase
                    .LoadAssetAtPath<Sprite>(
                        path);

            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    private static void ApplyTabletPolish(
        TabletUIManager manager)
    {
        GameObject backdrop =
            GetSerializedReference<
                GameObject>(
                    manager,
                    "backdropDim");

        GameObject tabletRoot =
            GetSerializedReference<
                GameObject>(
                    manager,
                    "tabletRoot");

        TMP_Text titleText =
            GetSerializedReference<
                TMP_Text>(
                    manager,
                    "tabletTitleText");

        TabletShellStyler styler =
            GetOrAddComponent<
                TabletShellStyler>(
                    manager.gameObject);

        styler.EditorConfigure(
            backdrop,
            tabletRoot,
            titleText);

        styler.ApplyStyle();

        EditorUtility.SetDirty(
            styler);
    }

    private static Image CreateImage(
        RectTransform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta,
        Vector2 anchoredPosition)
    {
        GameObject child =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform rect =
            child.GetComponent<
                RectTransform>();

        rect.SetParent(
            parent,
            false);

        rect.anchorMin =
            anchorMin;

        rect.anchorMax =
            anchorMax;

        rect.pivot =
            pivot;

        rect.sizeDelta =
            sizeDelta;

        rect.anchoredPosition =
            anchoredPosition;

        Image image =
            child.GetComponent<Image>();

        image.raycastTarget =
            false;

        image.color =
            Color.white;

        return image;
    }

    private static TMP_Text CreateText(
        RectTransform parent,
        string name,
        string value,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject child =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        RectTransform rect =
            child.GetComponent<
                RectTransform>();

        rect.SetParent(
            parent,
            false);

        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        TextMeshProUGUI text =
            child.GetComponent<
                TextMeshProUGUI>();

        if (TMP_Settings
                .defaultFontAsset != null)
        {
            text.font =
                TMP_Settings
                    .defaultFontAsset;
        }

        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        return text;
    }

    private static TMP_Text
        CreateAnchoredText(
            RectTransform parent,
            string name,
            string value,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Vector2 position,
            Vector2 size)
    {
        GameObject child =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        RectTransform rect =
            child.GetComponent<
                RectTransform>();

        rect.SetParent(
            parent,
            false);

        rect.anchorMin =
            new Vector2(
                0f,
                1f);

        rect.anchorMax =
            new Vector2(
                0f,
                1f);

        rect.pivot =
            new Vector2(
                0f,
                1f);

        rect.anchoredPosition =
            position;

        rect.sizeDelta =
            size;

        TextMeshProUGUI text =
            child.GetComponent<
                TextMeshProUGUI>();

        if (TMP_Settings
                .defaultFontAsset != null)
        {
            text.font =
                TMP_Settings
                    .defaultFontAsset;
        }

        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        return text;
    }

    private static Transform
        FindOrCreateRectChild(
            RectTransform parent,
            string name)
    {
        Transform existing =
            parent.Find(name);

        if (existing != null)
        {
            return existing;
        }

        GameObject child =
            new GameObject(
                name,
                typeof(RectTransform));

        RectTransform rect =
            child.GetComponent<
                RectTransform>();

        rect.SetParent(
            parent,
            false);

        return rect;
    }

    private static void StretchFull(
        RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;
    }

    private static T GetOrAddComponent<T>(
        GameObject gameObject)
        where T : Component
    {
        T component =
            gameObject.GetComponent<T>();

        if (component == null)
        {
            component =
                gameObject.AddComponent<T>();
        }

        return component;
    }

    private static T
        GetSerializedReference<T>(
            Object owner,
            string propertyName)
        where T : Object
    {
        if (owner == null)
        {
            return null;
        }

        SerializedObject serialized =
            new SerializedObject(owner);

        SerializedProperty property =
            serialized.FindProperty(
                propertyName);

        if (property == null)
        {
            return null;
        }

        return property
                   .objectReferenceValue
               as T;
    }
}
#endif
