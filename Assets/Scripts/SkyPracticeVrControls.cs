using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Valve.VR;

// SteamVR pointing and a world-space menu. Desktop clicks use the same pointer path.
public class SkyPracticeVrControls : MonoBehaviour
{
    private sealed class MenuButton
    {
        public string Name;
        public BoxCollider Collider;
        public Image Image;
        public Text Label;
        public Action Click;
        public bool Enabled = true;
    }

    private const float PanelWidth = 560f;
    private const float ContentWidth = PanelWidth - 32f;
    private readonly List<MenuButton> buttons = new List<MenuButton>();
    private readonly Dictionary<DrawLineManager, bool> savedLegacyDrawing = new Dictionary<DrawLineManager, bool>();
    private readonly Color buttonColour = new Color(0.12f, 0.2f, 0.3f, 1f);
    private readonly Color hoverColour = new Color(0.25f, 0.39f, 0.53f, 1f);
    private SkyCultureComparisonController comparison;
    private StarField field;
    private ChineseAsterismController chinese;
    private Camera viewCamera;
    private SteamVR_Behaviour_Pose rightPose;
    private SteamVR_Action_Boolean confirmAction;
    private GameObject interfaceRoot;
    private RectTransform panel, aboutViewport, aboutContent;
    private BoxCollider panelCollider;
    private Text titleLabel, statusLabel, inputLabel, aboutLabel;
    private MenuButton openButton;
    private ScrollRect aboutScroll;
    private LineRenderer pointerLine, hoverRing;
    private Material pointerMaterial;
    private Font font;
    private bool aboutOpen, drawingChoicesOpen, panelOpen = true, legacySuspended, wasPracticing, wasInterfaceVisible;

    public bool UsingHeadset => XRSettings.isDeviceActive || (viewCamera != null && viewCamera.stereoEnabled);
    public int HoveredStarHr { get; private set; }
    public string HoveredButtonName { get; private set; }
    public Vector3 PointerEndPoint { get; private set; }

    public void Initialize(SkyCultureComparisonController controller)
    {
        if (comparison != null || controller == null) return;
        comparison = controller;
        field = controller.GetComponent<StarField>();
        chinese = controller.GetComponent<ChineseAsterismController>();
        viewCamera = Camera.main;
        if (viewCamera != null) BuildInterface();
        SyncLegacyDrawing();
        RefreshInterface();
    }

    private void Update()
    {
        if (comparison == null || !comparison.IsReady) return;
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
            if (viewCamera == null) return;
        }
        if (interfaceRoot == null) BuildInterface();
        SyncLegacyDrawing();
        RefreshInterface();

        // Checking Unity XR first avoids initializing SteamVR on a desktop-only preview.
        if (UsingHeadset)
        {
            if (rightPose == null || !rightPose.isActiveAndEnabled)
                foreach (var pose in FindObjectsOfType<SteamVR_Behaviour_Pose>(true))
                    if (pose.inputSource == SteamVR_Input_Sources.RightHand && pose.isActiveAndEnabled)
                    {
                        rightPose = pose;
                        break;
                    }
            if (rightPose != null && rightPose.isActiveAndEnabled && rightPose.poseAction != null && rightPose.isActive && rightPose.isValid)
            {
                if (confirmAction == null) confirmAction = SteamVR_Actions.default_GrabPinch;
                ProcessPointer(new Ray(rightPose.transform.position, rightPose.transform.forward),
                    confirmAction != null && confirmAction.GetStateDown(rightPose.inputSource));
            }
            else HidePointer();
        }
        else
        {
            ProcessPointer(viewCamera.ScreenPointToRay(Input.mousePosition), Input.GetMouseButtonDown(0));
        }
    }

    public void ProcessPointer(Ray ray, bool confirm)
    {
        if (!isActiveAndEnabled || comparison == null || !comparison.IsReady || interfaceRoot == null || ray.direction.sqrMagnitude < 0.001f)
            return;
        ray.direction = ray.direction.normalized;
        HoveredStarHr = 0;
        HoveredButtonName = null;
        MenuButton hovered = null;
        Vector3 endpoint = ray.GetPoint(6f);
        bool blockedByPanel = false;
        float nearest = float.PositiveInfinity;

        foreach (var button in buttons)
            if (button.Collider.enabled && button.Collider.gameObject.activeInHierarchy && TryHitRectangle(ray, button.Collider, out float distance) && distance < nearest)
            {
                nearest = distance;
                hovered = button;
                endpoint = ray.GetPoint(distance);
                blockedByPanel = true;
            }
        if (panelCollider.enabled && panelCollider.gameObject.activeInHierarchy && TryHitRectangle(ray, panelCollider, out float backgroundDistance) &&
            backgroundDistance < nearest - 0.0001f)
        {
            hovered = null;
            endpoint = ray.GetPoint(backgroundDistance);
            blockedByPanel = true;
        }

        foreach (var button in buttons)
            button.Image.color = !button.Enabled ? new Color(0.08f, 0.1f, 0.14f, 1f) :
                button == hovered ? hoverColour : buttonColour;
        if (hovered != null)
        {
            HoveredButtonName = hovered.Name;
            if (confirm && hovered.Enabled)
            {
                hovered.Click();
                SyncLegacyDrawing();
                RefreshInterface();
            }
        }
        else if (!blockedByPanel)
        {
            if (comparison.IsPracticing && comparison.TryPickPracticeStar(ray, out int hr, out Vector3 position))
            {
                HoveredStarHr = hr;
                endpoint = position;
                if (confirm) comparison.SelectPracticeStar(hr);
            }
            else if (!comparison.IsActive && confirm && chinese != null)
                chinese.TryFocus(ray);
        }

        PointerEndPoint = endpoint;
        pointerLine.enabled = UsingHeadset;
        pointerLine.SetPosition(0, ray.origin);
        pointerLine.SetPosition(1, endpoint);
        hoverRing.enabled = HoveredStarHr > 0;
        if (HoveredStarHr > 0) SetHoverRing(endpoint, ray.origin);
    }

    // The collider's local rectangle is also the hit target. No global physics sync is
    // needed when a UI action changes its transform or visibility in the same frame.
    private static bool TryHitRectangle(Ray ray, BoxCollider box, out float distance)
    {
        var plane = new Plane(box.transform.forward, box.transform.TransformPoint(box.center));
        if (!plane.Raycast(ray, out distance) || distance < 0f) return false;
        Vector3 point = box.transform.InverseTransformPoint(ray.GetPoint(distance)) - box.center;
        return Mathf.Abs(point.x) <= box.size.x * 0.5f && Mathf.Abs(point.y) <= box.size.y * 0.5f;
    }

    private void BuildInterface()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        interfaceRoot = new GameObject("SkyPracticeVrInterface");
        interfaceRoot.transform.SetParent(viewCamera.transform, false);
        panel = MakeCanvas("SkyPracticeWorldPanel", new Vector2(PanelWidth, 258f), new Vector3(-0.8f, -0.4f, 2.2f));
        var background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.025f, 0.045f, 0.08f, 0.96f);
        panelCollider = panel.gameObject.AddComponent<BoxCollider>();
        panelCollider.center = new Vector3(0f, 0f, 2f);
        panelCollider.size = new Vector3(PanelWidth, 258f, 0.1f);
        titleLabel = MakeText(panel, "CaseTitle", 16f, 10f, ContentWidth, 34f, 22);
        statusLabel = MakeText(panel, "PracticeStatus", 16f, 48f, ContentWidth, 44f, 18);
        statusLabel.supportRichText = true;
        inputLabel = MakeText(panel, "PointerHelp", 16f, 98f, ContentWidth, 30f, 17);
        Row(134f, new[] { "PreviousCase", "NextCase", "ExitComparison" }, new[] { "< Previous", "Next >", "Exit comparison" },
            new Action[] { () => comparison.SelectNextCase(-1), () => comparison.SelectNextCase(1), ExitComparison });
        Row(174f, new[] { "SwitchCulture", "BothViews" }, new[] { "Chinese / Western", "Show both cultures" },
            new Action[] { () => comparison.SwitchCulture(), () => comparison.ToggleBoth() });
        Row(254f, new[] { "DrawChinese", "DrawWestern", "DrawBoth" }, new[] { "Draw Chinese", "Draw Western", "Draw both" },
            new Action[] { () => StartDrawing(SkyCultureComparisonController.DrawingMode.Chinese),
                () => StartDrawing(SkyCultureComparisonController.DrawingMode.Western),
                () => StartDrawing(SkyCultureComparisonController.DrawingMode.Both) });
        Row(134f, new[] { "CancelSelection", "Undo", "Reset" }, new[] { "Cancel start", "Undo line", "Restart drawing" },
            new Action[] { () => comparison.CancelPracticeSelection(), () => comparison.UndoPracticeLine(), () => comparison.ResetPractice() });
        Row(174f, new[] { "Advance", "Answer" }, new[] { "Finish drawing", "Show answer" },
            new Action[] { () => comparison.AdvancePractice(), ToggleAnswer });
        Row(214f, new[] { "BackToObserve", "About", "HidePanel" }, new[] { "Back to observe", "About this case", "Hide panel" },
            new Action[] { () => comparison.StopPractice(), () => { aboutOpen = !aboutOpen; if (aboutOpen) drawingChoicesOpen = false; }, () => { panelOpen = false; } });
        MakeButton(panel, "StartDrawing", "Start drawing", new Rect(16f, 214f, (ContentWidth - 24f) / 3f, 36f),
            () => { drawingChoicesOpen = !drawingChoicesOpen; aboutOpen = false; });

        aboutViewport = MakeRect(panel, "AboutViewport", new Rect(16f, 264f, ContentWidth, 210f));
        aboutViewport.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.075f, 0.12f, 1f);
        aboutViewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        aboutScroll = aboutViewport.gameObject.AddComponent<ScrollRect>();
        aboutScroll.horizontal = false;
        aboutScroll.movementType = ScrollRect.MovementType.Clamped;
        aboutScroll.viewport = aboutViewport;
        aboutContent = MakeRect(aboutViewport, "AboutContent", new Rect(10f, 8f, ContentWidth - 20f, 400f));
        aboutLabel = MakeText(aboutContent, "AboutText", 0f, 0f, ContentWidth - 20f, 400f, 18);
        aboutLabel.supportRichText = false;
        aboutScroll.content = aboutContent;
        MakeButton(panel, "AboutUp", "Scroll up", new Rect(16f, 482f, (ContentWidth - 12f) / 2f, 36f),
            () => aboutScroll.verticalNormalizedPosition = Mathf.Min(1f, aboutScroll.verticalNormalizedPosition + 0.35f));
        MakeButton(panel, "AboutDown", "Scroll down", new Rect(16f + (ContentWidth + 12f) / 2f, 482f, (ContentWidth - 12f) / 2f, 36f),
            () => aboutScroll.verticalNormalizedPosition = Mathf.Max(0f, aboutScroll.verticalNormalizedPosition - 0.35f));

        var tab = MakeCanvas("SkyPracticeMenuTab", new Vector2(250f, 46f), new Vector3(-0.8f, -0.95f, 2.2f));
        openButton = MakeButton(tab, "OpenPanel", "Open menu", new Rect(0f, 0f, 250f, 46f), () => { panelOpen = true; });
        var shader = Resources.Load<Shader>("ChineseAsterismLine");
        pointerMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        pointerLine = MakePointerLine("PracticeControllerRay", 0.006f, new Color(0.45f, 1f, 0.85f, 1f), 2);
        hoverRing = MakePointerLine("PracticeHoveredStar", 0.22f, new Color(0.45f, 1f, 0.85f, 1f), 32);
        hoverRing.loop = true;
        HidePointer();
    }

    private RectTransform MakeCanvas(string name, Vector2 size, Vector3 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(interfaceRoot.transform, false);
        rect.sizeDelta = size;
        rect.localPosition = position;
        rect.localScale = Vector3.one * 0.002f;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = viewCamera;
        return rect;
    }

    private static RectTransform MakeRect(Transform parent, string name, Rect bounds)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(bounds.x, -bounds.y);
        rect.sizeDelta = bounds.size;
        return rect;
    }

    private Text MakeText(Transform parent, string name, float x, float y, float width, float height, int size)
    {
        var label = MakeRect(parent, name, new Rect(x, y, width, height)).gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        return label;
    }

    private MenuButton MakeButton(Transform parent, string name, string text, Rect bounds, Action action)
    {
        var rect = MakeRect(parent, "PracticeButton_" + name, bounds);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = buttonColour;
        var collider = rect.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(bounds.width * 0.5f, -bounds.height * 0.5f, -1f);
        collider.size = new Vector3(bounds.width, bounds.height, 0.1f);
        var label = MakeText(rect, "Caption", 5f, 0f, bounds.width - 10f, bounds.height, 18);
        label.alignment = TextAnchor.MiddleCenter;
        label.text = text;
        var button = new MenuButton { Name = name, Collider = collider, Image = image, Label = label, Click = action };
        buttons.Add(button);
        return button;
    }

    private void Row(float y, string[] names, string[] labels, Action[] actions)
    {
        float width = (ContentWidth - 12f * (names.Length - 1)) / names.Length;
        for (int i = 0; i < names.Length; i++)
            MakeButton(panel, names[i], labels[i], new Rect(16f + i * (width + 12f), y, width, 36f), actions[i]);
    }

    private void RefreshInterface()
    {
        if (interfaceRoot == null || comparison == null) return;
        bool active = comparison.IsActive, practicing = comparison.IsPracticing;
        bool showInterface = active;
        if (showInterface && !wasInterfaceVisible)
        {
            panelOpen = !practicing;
            aboutOpen = drawingChoicesOpen = false;
        }
        wasInterfaceVisible = showInterface;
        if (practicing && !wasPracticing) aboutOpen = false;
        wasPracticing = practicing;
        panel.gameObject.SetActive(showInterface && panelOpen);
        openButton.Collider.transform.parent.gameObject.SetActive(showInterface && !panelOpen);
        if (!showInterface)
        {
            aboutOpen = drawingChoicesOpen = false;
            HidePointer();
            return;
        }
        titleLabel.text = $"[{comparison.CaseIndex + 1}/{comparison.CaseCount}] {comparison.ChineseName} / {comparison.WesternName}";
        string practiceHeading;
        if (comparison.PracticePhase == SkyCultureComparisonController.DrawingPhase.Review)
            practiceHeading = comparison.PracticeAnswerVisible ? "Standard answer | Magenta: shared connections" :
                comparison.PracticeMode == SkyCultureComparisonController.DrawingMode.Both ?
                "Your drawing | Magenta: your two drawings overlap" : "Your completed drawing";
        else if (comparison.PracticeMode == SkyCultureComparisonController.DrawingMode.Both)
            practiceHeading = comparison.PracticePhase == SkyCultureComparisonController.DrawingPhase.Chinese ?
                "Stage 1/2: Chinese | No standard reference" : "Stage 2/2: Western | Magenta: your overlap";
        else practiceHeading = comparison.PracticeMode == SkyCultureComparisonController.DrawingMode.Chinese ?
                "Draw Chinese | Western reference" : "Draw Western | Chinese reference";
        statusLabel.text = practicing ? practiceHeading + $"\nYour lines: Chinese {comparison.PracticeChineseLineCount} | Western {comparison.PracticeWesternLineCount}" :
            $"Observe: {comparison.CurrentView} | Rings: shared stars\nGold: Chinese | Blue: Western | Magenta: shared lines";
        bool showFeedback = practicing && !comparison.PracticeAnswerVisible &&
            !string.IsNullOrEmpty(comparison.PracticeFeedbackText);
        Color feedbackColour = comparison.PracticeFeedbackCorrect ? new Color(110f / 255f, 1f, 154f / 255f) :
            new Color(1f, 199f / 255f, 107f / 255f);
        if (showFeedback)
        {
            string colour = comparison.PracticeFeedbackCorrect ? "#6EFF9A" : "#FFC76B";
            statusLabel.text = "<color=" + colour + ">" + comparison.PracticeFeedbackText + "</color>\n" + practiceHeading;
        }
        inputLabel.text = practicing ? (UsingHeadset ? "Right hand: point and confirm two outlined stars to connect." :
            "Click two outlined stars to connect. Right-drag to look.") :
            "Choose Start drawing to draw Chinese, Western, or both.";
        openButton.Label.text = practicing ? (comparison.PracticePhase == SkyCultureComparisonController.DrawingPhase.Western ? "Western | Open menu" :
            comparison.PracticePhase == SkyCultureComparisonController.DrawingPhase.Chinese ? "Chinese | Open menu" : "Review | Open menu") : "Open menu";
        if (showFeedback) openButton.Label.text = comparison.PracticeFeedbackCorrect ? "Correct! | Open menu" : "Try again | Open menu";
        openButton.Label.color = showFeedback ? feedbackColour : Color.white;

        foreach (var button in buttons)
        {
            if (button == openButton) continue;
            bool visible = active;
            bool enabled = true;
            switch (button.Name)
            {
                case "PreviousCase": case "NextCase": case "ExitComparison":
                case "SwitchCulture": case "BothViews": case "StartDrawing": visible = active && !practicing; break;
                case "DrawChinese": case "DrawWestern": case "DrawBoth": visible = active && !practicing && drawingChoicesOpen; break;
                case "CancelSelection": visible = active && practicing; enabled = comparison.PracticeSelectedHr != 0; break;
                case "Undo": visible = active && practicing; enabled = comparison.PracticePhase != SkyCultureComparisonController.DrawingPhase.Review; break;
                case "Reset": case "Answer": case "BackToObserve": visible = active && practicing; break;
                case "Advance": visible = active && practicing; enabled = comparison.PracticePhase != SkyCultureComparisonController.DrawingPhase.Review; break;
                case "AboutUp": case "AboutDown": visible = active && aboutOpen; break;
            }
            button.Collider.gameObject.SetActive(visible);
            button.Enabled = enabled;
            button.Label.color = enabled ? Color.white : new Color(0.5f, 0.54f, 0.6f, 1f);
            if (button.Name == "Answer") button.Label.text = comparison.PracticeAnswerVisible ? "Your drawing" : "Show answer";
            if (button.Name == "About") button.Label.text = aboutOpen ? "Close introduction" : "About this case";
            if (button.Name == "StartDrawing") button.Label.text = drawingChoicesOpen ? "Close choices" : "Start drawing";
            if (button.Name == "Advance") button.Label.text = comparison.PracticeMode == SkyCultureComparisonController.DrawingMode.Both &&
                comparison.PracticePhase == SkyCultureComparisonController.DrawingPhase.Chinese ? "Check Chinese / Next" : "Finish / Check drawing";
        }
        bool showAbout = active && aboutOpen;
        aboutViewport.gameObject.SetActive(showAbout);
        float height = showAbout ? 526f : drawingChoicesOpen && !practicing ? 298f : 258f;
        panel.sizeDelta = new Vector2(PanelWidth, height);
        panelCollider.size = new Vector3(PanelWidth, height, 0.1f);
        if (showAbout && aboutLabel.text != comparison.CaseAboutText)
        {
            aboutLabel.text = comparison.CaseAboutText;
            float contentHeight = Mathf.Max(aboutViewport.rect.height - 16f, aboutLabel.preferredHeight + 8f);
            aboutLabel.rectTransform.sizeDelta = new Vector2(ContentWidth - 20f, contentHeight);
            aboutContent.sizeDelta = new Vector2(ContentWidth - 20f, contentHeight);
            aboutScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void StartDrawing(SkyCultureComparisonController.DrawingMode mode)
    {
        aboutOpen = false;
        drawingChoicesOpen = false;
        comparison.StartPractice(mode);
        panelOpen = false;
    }

    private void ExitComparison()
    {
        if (field != null) field.ExitComparisonMode();
        aboutOpen = drawingChoicesOpen = false;
    }

    private void ToggleAnswer()
    {
        if (comparison.PracticeAnswerVisible) comparison.HidePracticeAnswer();
        else comparison.RevealPracticeAnswer();
    }

    private void SyncLegacyDrawing()
    {
        bool shouldSuspend = comparison != null && comparison.IsActive ||
            chinese != null && chinese.IsRegionVisible;
        if (shouldSuspend && !legacySuspended)
        {
            foreach (var legacy in FindObjectsOfType<DrawLineManager>(true))
            {
                savedLegacyDrawing[legacy] = legacy.enabled;
                legacy.enabled = false;
            }
            legacySuspended = true;
        }
        else if (!shouldSuspend && legacySuspended) RestoreLegacyDrawing();
    }

    private void RestoreLegacyDrawing()
    {
        foreach (var item in savedLegacyDrawing)
            if (item.Key != null) item.Key.enabled = item.Value;
        savedLegacyDrawing.Clear();
        legacySuspended = false;
    }

    private LineRenderer MakePointerLine(string name, float width, Color colour, int points)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(interfaceRoot.transform, false);
        line.sharedMaterial = pointerMaterial;
        line.useWorldSpace = true;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = colour;
        line.positionCount = points;
        return line;
    }

    private void SetHoverRing(Vector3 position, Vector3 origin)
    {
        Vector3 normal = (position - origin).normalized;
        Vector3 right = Vector3.Cross(normal, Vector3.up);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(normal, Vector3.right);
        right.Normalize();
        Vector3 up = Vector3.Cross(normal, right);
        float radius = Mathf.Max(0.025f, Vector3.Distance(position, origin) * 0.006f);
        for (int i = 0; i < 32; i++)
        {
            float angle = i * Mathf.PI * 2f / 32f;
            hoverRing.SetPosition(i, position - normal * radius * 0.2f + radius * (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)));
        }
    }

    private void HidePointer()
    {
        HoveredStarHr = 0;
        HoveredButtonName = null;
        if (pointerLine != null) pointerLine.enabled = false;
        if (hoverRing != null) hoverRing.enabled = false;
    }

    private void OnEnable()
    {
        if (interfaceRoot != null) interfaceRoot.SetActive(true);
    }

    private void OnDisable()
    {
        RestoreLegacyDrawing();
        HidePointer();
        if (interfaceRoot != null) interfaceRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        RestoreLegacyDrawing();
        if (interfaceRoot != null) Destroy(interfaceRoot);
        if (pointerMaterial != null) Destroy(pointerMaterial);
    }
}
