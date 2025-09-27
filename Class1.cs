using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[BepInPlugin("com.recks.playerstatscanvas", "Player Stats Canvas GUI", "5.0.2")]
[BepInProcess("Erenshor.exe")]
public class PlayerStatsCanvasPlugin : BaseUnityPlugin
{
    // Core UI references
    private Canvas statsCanvas;
    private RectTransform panelRect;
    private GameObject windowPanel;
    private GameObject headerObject;
    private GameObject tabContainer;
    private GameObject contentObject;
    private GameObject dragHandle;

    // Tab buttons and images
    private Button statsTab;
    private Button repTab;
    private Image statsTabImage;
    private Image repTabImage;

    // CanvasGroup for fade
    private CanvasGroup panelGroup;

    // State
    private bool isLoaded;
    private bool dragging;
    private Vector2 dragOffset;

    // Config
    private ConfigEntry<KeyCode> toggleKey;
    private ConfigEntry<Color> activeTabColor;
    private ConfigEntry<Color> inactiveTabColor;

    private void Awake()
    {
        toggleKey = Config.Bind("Controls", "ToggleKey", KeyCode.P, "Key to open/close the stats window");
        activeTabColor = Config.Bind("Appearance", "ActiveTabColor", new Color(0.35f, 0.59f, 1f), "Color for active tab");
        inactiveTabColor = Config.Bind("Appearance", "InactiveTabColor", new Color(0.7f, 0.7f, 0.7f), "Color for inactive tab");
    }

    private void Update()
    {
        var scene = SceneManager.GetActiveScene().name;
        if (scene == "Menu" || scene == "LoadScene")
        {
            if (windowPanel != null && windowPanel.activeSelf)
            {
                panelGroup.alpha = 0f;
                panelGroup.interactable = false;
                panelGroup.blocksRaycasts = false;
                windowPanel.SetActive(false);
            }
            return;
        }

        if (!isLoaded && GameData.PlayerControl != null && GameData.PlayerStats != null)
        {
            isLoaded = true;
            InitializeUI();
        }
        if (!isLoaded) return;

        if (Input.GetKeyDown(toggleKey.Value))
            TogglePanel();
    }

    private void InitializeUI()
    {
        // Root Canvas
        var canvasGO = new GameObject("PlayerStatsCanvas");
        statsCanvas = canvasGO.AddComponent<Canvas>();
        statsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // Panel
        windowPanel = new GameObject("WindowPanel");
        windowPanel.transform.SetParent(canvasGO.transform, false);
        panelRect = windowPanel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(420, 620);
        panelRect.anchoredPosition = Vector2.zero;
        bg.color = new Color32(20, 30, 45, 230);
        windowPanel.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);
        panelGroup = windowPanel.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;

        // Header
        headerObject = new GameObject("Header");
        headerObject.transform.SetParent(windowPanel.transform, false);
        var hdrRect = headerObject.AddComponent<RectTransform>();
        hdrRect.anchorMin = new Vector2(0, 1);
        hdrRect.anchorMax = new Vector2(1, 1);
        hdrRect.pivot = new Vector2(0.5f, 1);
        hdrRect.sizeDelta = new Vector2(0, 40);
        var hdrImg = headerObject.AddComponent<Image>();
        hdrImg.color = new Color32(18, 30, 45, 255);
        var title = CreateText(headerObject.transform, "Player Stats & Reputation", 22, TextAnchor.MiddleCenter);
        title.rectTransform.anchorMin = Vector2.zero;
        title.rectTransform.anchorMax = Vector2.one;

        // Drag handle
        dragHandle = new GameObject("DragHandle");
        dragHandle.transform.SetParent(headerObject.transform, false);
        var dRect = dragHandle.AddComponent<RectTransform>();
        dRect.anchorMin = dRect.anchorMax = new Vector2(1, 1);
        dRect.pivot = new Vector2(1, 1);
        dRect.anchoredPosition = new Vector2(-20, -10);
        dRect.sizeDelta = new Vector2(16, 16);
        var dImg = dragHandle.AddComponent<Image>();
        dImg.color = new Color32(108, 194, 255, 255);
        dragHandle.transform.rotation = Quaternion.Euler(0, 0, 45);
        AddDragEvents(dragHandle.AddComponent<EventTrigger>());

        // Tabs
        tabContainer = new GameObject("Tabs");
        tabContainer.transform.SetParent(windowPanel.transform, false);
        var tRect = tabContainer.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0, 1);
        tRect.anchorMax = new Vector2(1, 1);
        tRect.pivot = new Vector2(0.5f, 1);
        tRect.anchoredPosition = new Vector2(0, -40);
        tRect.sizeDelta = new Vector2(0, 32);
        var tHLG = tabContainer.AddComponent<HorizontalLayoutGroup>();
        tHLG.childAlignment = TextAnchor.MiddleCenter;
        tHLG.spacing = 12;
        statsTab = CreateTabButton("Stats");
        statsTab.onClick.AddListener(() => ShowStats(true));
        statsTabImage = statsTab.GetComponent<Image>();
        repTab = CreateTabButton("Reputation");
        repTab.onClick.AddListener(() => ShowStats(false));
        repTabImage = repTab.GetComponent<Image>();

        // Content area
        contentObject = new GameObject("Content");
        contentObject.transform.SetParent(windowPanel.transform, false);
        var cRect = contentObject.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0, 0);
        cRect.anchorMax = new Vector2(1, 1);
        cRect.offsetMin = new Vector2(12, 12);
        cRect.offsetMax = new Vector2(-12, -84);
        var vLG = contentObject.AddComponent<VerticalLayoutGroup>();
        vLG.childAlignment = TextAnchor.UpperLeft;
        vLG.spacing = 6;
        vLG.padding = new RectOffset(8, 8, 8, 8);
        vLG.childForceExpandWidth = true;
        vLG.childForceExpandHeight = false;

        ShowStats(true);
        HighlightTab(true);
        windowPanel.SetActive(false);
    }

    private Button CreateTabButton(string label)
    {
        var go = new GameObject(label + "Tab");
        go.transform.SetParent(tabContainer.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120, 32);
        var img = go.AddComponent<Image>();
        img.color = inactiveTabColor.Value;
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        btn.colors = new ColorBlock
        {
            normalColor = inactiveTabColor.Value,
            highlightedColor = inactiveTabColor.Value * 1.2f,
            pressedColor = inactiveTabColor.Value * 0.9f,
            selectedColor = inactiveTabColor.Value,
            colorMultiplier = 1,
            fadeDuration = 0.1f
        };
        CreateText(go.transform, label, 18, TextAnchor.MiddleCenter);
        return btn;
    }

    private Text CreateText(Transform parent, string text, int size, TextAnchor align)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);
        return t;
    }

    private IEnumerator FadeIn()
    {
        windowPanel.SetActive(true);
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;
        for (float t = 0; t < 1; t += Time.deltaTime * 2)
        {
            panelGroup.alpha = t;
            yield return null;
        }
        panelGroup.alpha = 1;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;
    }

    private IEnumerator FadeOut()
    {
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;
        for (float t = 1; t > 0; t -= Time.deltaTime * 2)
        {
            panelGroup.alpha = t;
            yield return null;
        }
        panelGroup.alpha = 0;
        windowPanel.SetActive(false);
    }

    private void TogglePanel()
    {
        if (!windowPanel.activeSelf)
            StartCoroutine(FadeIn());
        else
            StartCoroutine(FadeOut());
    }

    private void ShowStats(bool showStats)
    {
        foreach (Transform c in contentObject.transform)
            Destroy(c.gameObject);
        if (showStats) RefreshStats(); else RefreshReputation();
        HighlightTab(showStats);
    }

    private void HighlightTab(bool statsActive)
    {
        statsTabImage.color = statsActive ? activeTabColor.Value : inactiveTabColor.Value;
        repTabImage.color = statsActive ? inactiveTabColor.Value : activeTabColor.Value;
    }

    private void AddDragEvents(EventTrigger et)
    {
        var eb = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        eb.callback.AddListener(data =>
        {
            dragging = true;
            var ped = (PointerEventData)data;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, ped.position, null, out dragOffset);
        });
        et.triggers.Add(eb);

        var ed = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        ed.callback.AddListener(data =>
        {
            if (dragging)
            {
                var ped = (PointerEventData)data;
                Vector2 pos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)statsCanvas.transform, ped.position, null, out pos);
                panelRect.anchoredPosition = pos - dragOffset;
            }
        });
        et.triggers.Add(ed);

        var ee = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        ee.callback.AddListener(data => { dragging = false; });
        et.triggers.Add(ee);
    }

    private void AddLine(string text)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(contentObject.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 24);

        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 16;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
        go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);
    }

    private void RefreshStats()
    {
        var s = GameData.PlayerStats;
        AddLine($"{"Level:",-20}{s.Level,8}");
        AddLine($"{"Ascension Level:",-20}{s.Myself.MySkills.GetPointsSpent(),8}");
        AddLine($"{"Exp:",-20}{s.CurrentExperience,8} / {s.ExperienceToLevelUp,-8}");
        AddLine($"{"Strength:",-20}{s.GetCurrentStr(),4}  (+{s.MyInv.ItemStr,3})");
        AddLine($"{"Dexterity:",-20}{s.GetCurrentDex(),4}  (+{s.MyInv.ItemDex,3})");
        AddLine($"{"Endurance:",-20}{s.GetCurrentEnd(),4}  (+{s.MyInv.ItemEnd,3})");
        AddLine($"{"Agility:",-20}{s.GetCurrentAgi(),4}  (+{s.MyInv.ItemAgi,3})");
        AddLine($"{"Intelligence:",-20}{s.GetCurrentInt(),4}  (+{s.MyInv.ItemInt,3})");
        AddLine($"{"Wisdom:",-20}{s.GetCurrentWis(),4}  (+{s.MyInv.ItemWis,3})");
        AddLine($"{"Charisma:",-20}{s.GetCurrentCha(),4}  (+{s.MyInv.ItemCha,3})");
        AddLine($"{"Crit Chance:",-20}{(s.GetCurrentDex() * 0.25f),8:F1}%");
        AddLine($"{"Dodge Chance:",-20}{(s.GetCurrentAgi() * 0.20f),8:F1}%");
        AddLine($"{"Physical Attack:",-20}{s.AttackAbility,8:F1}");
        AddLine($"{"Spell Dmg Bonus:",-20}{(s.GetCurrentInt() * (s.CharacterClass.IntBenefit / 100f)),8:F1}%");
        AddLine($"{"Healing Bonus:",-20}{(s.GetCurrentWis() * (s.CharacterClass.WisBenefit / 100f)),8:F1}%");
        AddLine($"{"Life Steal:",-20}{s.PercentLifesteal,8:F1}%");
        AddLine($"{"Damage Shield:",-20}{s.GetCurrentDS(),8}");
        AddLine($"{"Attack Speed Bonus:",-20}{(s.GetCurrentDex() * (s.CharacterClass.DexBenefit / 100f)),8:F1}%");
        AddLine($"{"Move Speed Bonus:",-20}{((s.actualRunSpeed - s.RunSpeed) / s.RunSpeed * 100f),8:F1}%");
        AddLine($"{"Resonance Proc Chance:",-20}{(s.GetCurrentDex() * (s.CharacterClass.DexBenefit / 100f)),8:F1}%");
    }


    private void RefreshReputation()
    {
        foreach (var f in GlobalFactionManager.AllFactions)
            AddLine($"{f.Desc}: {f.Value:F1}");
    }
}

