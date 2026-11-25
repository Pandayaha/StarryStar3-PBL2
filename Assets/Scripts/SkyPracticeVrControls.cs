using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Valve.VR;

// SteamVR pointing and a world-space menu. Desktop clicks use the same pointer path.
[DefaultExecutionOrder(-100)]
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
    private SinglePatternPractice single;
    private Camera viewCamera;
    private SteamVR_Behaviour_Pose rightPose;
    private SteamVR_Action_Boolean confirmAction, menuAction;
    private GameObject interfaceRoot;
    private RectTransform panel, aboutViewport, aboutContent;
    private BoxCollider panelCollider;
    private RectTransform selectionCard;
    private BoxCollider selectionBackground;
    private MenuButton detailsButton, previousCandidate, nextCandidate, drawSelected, backToSky;
    private Text selectedTitle, selectedSummary, selectedDescription;
    private bool detailsOpen;
    private int cardSide = 1;
    private string displayedCardId, displayedTagId;
    private int displayedCandidateCount, displayedCandidateIndex;
    private Text titleLabel, statusLabel, inputLabel, aboutLabel, trackingNoticeLabel;
    private MenuButton openButton;
    private ScrollRect aboutScroll;
    private LineRenderer pointerLine, hoverRing;
    private Material pointerMaterial;
    private Font font;
    private bool aboutOpen, drawingChoicesOpen, panelOpen, legacySuspended, wasPracticing, wasComparisonActive;
    private int westernPatternIndex;
    private int suppressLegacyThroughFrame = -1;
    private bool consumedTriggerHeld;
    private float nextPoseSearchTime;
    private string trackingHint = string.Empty;
    private float rightPoseLostAt = -1f, trackingNoticeEndsAt;
    private bool trackingNoticeShown;

    public bool UsingHeadset => XRSettings.isDeviceActive || (viewCamera != null && viewCamera.stereoEnabled);
    public int HoveredStarHr { get; private set; }
    public string HoveredButtonName { get; private set; }
    public Vector3 PointerEndPoint { get; private set; }
    public bool IsMenuOpen => panelOpen;
    private bool DrawingIndividual => single != null && single.IsDrawing;

    public void Initialize(SkyCultureComparisonController controller)
    {
        if (comparison != null || controller == null) return;
        comparison = controller;
        field = controller.GetComponent<StarField>();
        chinese = controller.GetComponent<ChineseAsterismController>();
        single = controller.GetComponent<SinglePatternPractice>();
        RefreshCamera();
        RefreshInterface();
        SyncLegacyDrawing();
    }

    private void Update()
    {
        if (comparison == null || !comparison.IsReady) return;
        if (!RefreshCamera()) return;
        // Checking Unity XR first avoids initializing SteamVR on a desktop-only preview.
        if (UsingHeadset)
        {
            RefreshControllerPoses();
            if (menuAction == null) menuAction = SteamVR_Actions.default_GrabGrip;
            if (confirmAction == null) confirmAction = SteamVR_Actions.default_GrabPinch;
            if (!PoseReady(rightPose)) trackingHint = "Right controller not tracked. Check SteamVR. M: menu.";
            else if (confirmAction == null || !confirmAction.GetActive(SteamVR_Input_Sources.RightHand))
                trackingHint = "Right confirm inactive. Check SteamVR bindings.";
            else if (menuAction == null || !menuAction.GetActive(SteamVR_Input_Sources.LeftHand))
                trackingHint = "Left menu action inactive. Check binding; M also works.";
            else trackingHint = string.Empty;
        }
        else trackingHint = string.Empty;

        // Grip/M wins over confirmation, including on the frame the menu closes.
        if (Input.GetKeyDown(KeyCode.M) || (UsingHeadset && menuAction != null &&
            menuAction.GetActive(SteamVR_Input_Sources.LeftHand) && menuAction.GetStateDown(SteamVR_Input_Sources.LeftHand)))
        {
            ToggleMenu();
            return;
        }
        RefreshInterface();
        RefreshTrackingNotice(UsingHeadset, UsingHeadset && PoseReady(rightPose));
        SyncLegacyDrawing();
        if (UsingHeadset)
        {
            if (PoseReady(rightPose))
            {
                ProcessPointer(new Ray(rightPose.transform.position, rightPose.transform.forward),
                    confirmAction != null && confirmAction.GetStateDown(SteamVR_Input_Sources.RightHand));
            }
            else HidePointer();
        }
        else
        {
            ProcessPointer(viewCamera.ScreenPointToRay(Input.mousePosition), Input.GetMouseButtonDown(0));
        }
    }

    private bool RefreshCamera()
    {
        var current = Camera.main;
        if (current == null || !current.isActiveAndEnabled)
        {
            HidePointer();
            return false;
        }
        if (viewCamera != current)
        {
            viewCamera = current;
            rightPose = null;
            nextPoseSearchTime = 0f;
            if (interfaceRoot != null)
            {
                interfaceRoot.transform.SetParent(viewCamera.transform, false);
                foreach (var canvas in interfaceRoot.GetComponentsInChildren<Canvas>(true)) canvas.worldCamera = viewCamera;
            }
        }
        if (interfaceRoot == null)
        {
            buttons.Clear();
            if (pointerMaterial != null) Destroy(pointerMaterial);
            BuildInterface();
        }
        return true;
    }

    private static bool PoseReady(SteamVR_Behaviour_Pose pose)
    {
        return pose != null && pose.isActiveAndEnabled && pose.poseAction != null && pose.isActive && pose.isValid;
    }

    private void RefreshControllerPoses()
    {
        if (PoseReady(rightPose)) return;
        if (Time.unscaledTime < nextPoseSearchTime) return;
        nextPoseSearchTime = Time.unscaledTime + 0.5f;
        rightPose = null;
        foreach (var pose in FindObjectsOfType<SteamVR_Behaviour_Pose>(true))
        {
            if (pose.inputSource != SteamVR_Input_Sources.RightHand || !PoseReady(pose)) continue;
            rightPose = pose;
            break;
        }
    }

    private void RefreshTrackingNotice(bool headsetActive, bool rightTracked)
    {
        if (trackingNoticeLabel == null) return;
        float now = Time.unscaledTime;
        if (!headsetActive || rightTracked)
        {
            rightPoseLostAt = -1f;
            trackingNoticeEndsAt = 0f;
            trackingNoticeShown = false;
        }
        else
        {
            if (rightPoseLostAt < 0f) rightPoseLostAt = now;
            if (panelOpen) trackingNoticeEndsAt = 0f;
            else if (!trackingNoticeShown && now - rightPoseLostAt >= 0.75f)
            {
                trackingNoticeShown = true;
                trackingNoticeEndsAt = now + 4f;
            }
        }
        trackingNoticeLabel.transform.parent.gameObject.SetActive(
            headsetActive && !rightTracked && !panelOpen && now < trackingNoticeEndsAt);
    }

    public void ToggleMenu()
    {
        if (!isActiveAndEnabled || comparison == null || !comparison.IsReady || !RefreshCamera()) return;
        SyncComparisonMenuState();
        panelOpen = !panelOpen;
        if (panelOpen) detailsOpen = false;
        if (!panelOpen) aboutOpen = drawingChoicesOpen = false;
        HidePointer();
        ConsumePointerPress();
        RefreshInterface();
        RefreshTrackingNotice(UsingHeadset, UsingHeadset && PoseReady(rightPose));
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
        if (BackgroundBlocks(ray, panelCollider, nearest, out float backgroundDistance) ||
            BackgroundBlocks(ray, selectionBackground, nearest, out backgroundDistance))
        {
            hovered = null;
            endpoint = ray.GetPoint(backgroundDistance);
            blockedByPanel = true;
        }

        foreach (var button in buttons)
            button.Image.color = !button.Enabled ? new Color(0.08f, 0.1f, 0.14f, 1f) :
                button == hovered ? hoverColour : buttonColour;
        bool hoverIndividualStar = false;
        if (hovered != null)
        {
            HoveredButtonName = hovered.Name;
            if (confirm && hovered.Enabled)
            {
                hovered.Click();
                ConsumePointerPress();
                RefreshInterface();
                RefreshTrackingNotice(UsingHeadset, UsingHeadset && PoseReady(rightPose));
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
            else if (!comparison.IsActive && single != null)
            {
                Transform star;
                if (single.IsDrawing)
                {
                    if (single.TryPickDrawingStar(ray, out star))
                    {
                        hoverIndividualStar = true;
                        endpoint = star.position;
                        if (confirm) single.SelectDrawingStar(star);
                    }
                }
                else if (single.TryPickBrowseStar(ray, out star))
                {
                    hoverIndividualStar = true;
                    endpoint = star.position;
                    if (confirm && single.SelectBrowseStar(star))
                    {
                        cardSide = viewCamera.WorldToViewportPoint(single.SelectedCentre).x > 0.5f ? -1 : 1;
                        detailsOpen = false;
                        ConsumePointerPress();
                        RefreshInterface();
                    }
                }
                else if (confirm && !panelOpen && single.HasSelection)
                {
                    detailsOpen = false;
                    single.ClearSelection();
                    ConsumePointerPress();
                    RefreshInterface();
                }
            }
            else if (!comparison.IsActive && confirm && chinese != null)
                chinese.TryFocus(ray);
        }

        PointerEndPoint = endpoint;
        pointerLine.enabled = UsingHeadset;
        pointerLine.SetPosition(0, ray.origin);
        pointerLine.SetPosition(1, endpoint);
        hoverRing.enabled = HoveredStarHr > 0 || hoverIndividualStar;
        if (hoverRing.enabled) SetHoverRing(endpoint, ray.origin);
    }

    private static bool BackgroundBlocks(Ray ray, BoxCollider box, float buttonDistance, out float distance)
    {
        distance = 0f;
        return box != null && box.enabled && box.gameObject.activeInHierarchy &&
            TryHitRectangle(ray, box, out distance) && distance < buttonDistance - 0.0001f;
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
            new Action[] { () => { if (DrawingIndividual) single.CancelStart(); else comparison.CancelPracticeSelection(); },
                () => { if (DrawingIndividual) single.Undo(); else comparison.UndoPracticeLine(); },
                () => { if (DrawingIndividual) single.Restart(); else comparison.ResetPractice(); } });
        Row(174f, new[] { "Advance", "Answer" }, new[] { "Finish drawing", "Show answer" },
            new Action[] { () => { if (DrawingIndividual) single.Finish(); else comparison.AdvancePractice(); }, ToggleAnswer });
        Row(214f, new[] { "BackToObserve", "About", "HidePanel" }, new[] { "Back to observe", "About this case", "Hide panel" },
            new Action[] { BackToObserve, () => { aboutOpen = !aboutOpen; if (aboutOpen) drawingChoicesOpen = false; }, () => { panelOpen = false; } });
        MakeButton(panel, "StartDrawing", "Start drawing", new Rect(16f, 214f, (ContentWidth - 24f) / 3f, 36f),
            () => { drawingChoicesOpen = !drawingChoicesOpen; aboutOpen = false; });
        Row(134f, new[] { "BrowseCulture", "BrowseBoth" }, new[] { "Chinese / Western", "Show both cultures" },
            new Action[] { () => field.SwitchSkyCulture(), () => field.ToggleBothCultures() });
        Row(174f, new[] { "WesternPrevious", "WesternToggle", "WesternNext" }, new[] { "< Western", "Show", "Western >" },
            new Action[] { () => SelectWesternPattern(-1), () => field.ToggleConstellation(westernPatternIndex), () => SelectWesternPattern(1) });
        Row(214f, new[] { "RegionPrevious", "RegionToggle", "RegionNext" }, new[] { "< Region", "Show Chinese", "Region >" },
            new Action[] { () => chinese.SelectNextRegion(-1), () => chinese.ToggleSelectedRegion(), () => chinese.SelectNextRegion(1) });
        Row(254f, new[] { "FocusPrevious", "FocusNext" }, new[] { "< Asterism", "Asterism >" },
            new Action[] { () => chinese.SelectNextAsterism(-1), () => chinese.SelectNextAsterism(1) });
        Row(294f, new[] { "EnterComparison", "BrowseClose" }, new[] { "Compare / Draw", "Close menu" },
            new Action[] { () => field.EnterComparisonMode(), () => { panelOpen = false; } });

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
        BuildSelectionCard();
        var notice = MakeCanvas("SkyPracticeTrackingNotice", new Vector2(PanelWidth, 30f), new Vector3(-0.8f, -1.07f, 2.2f));
        trackingNoticeLabel = MakeText(notice, "ControllerTrackingNotice", 0f, 0f, PanelWidth, 30f, 17);
        trackingNoticeLabel.text = "Right controller not tracked. Check SteamVR.";
        trackingNoticeLabel.color = new Color(1f, 199f / 255f, 107f / 255f);
        trackingNoticeLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        notice.gameObject.SetActive(false);
        var shader = Resources.Load<Shader>("ChineseAsterismLine");
        pointerMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        pointerLine = MakePointerLine("PracticeControllerRay", 0.006f, new Color(0.45f, 1f, 0.85f, 1f), 2);
        hoverRing = MakePointerLine("PracticeHoveredStar", 0.22f, new Color(0.45f, 1f, 0.85f, 1f), 32);
        hoverRing.loop = true;
        HidePointer();
    }

    private void BuildSelectionCard()
    {
        displayedCardId = displayedTagId = null;
        var tag = MakeCanvas("SinglePatternTag", new Vector2(320f, 46f), Vector3.zero);
        detailsButton = MakeButton(tag, "SingleDetails", "Details", new Rect(0f, 0f, 320f, 46f),
            () => { detailsOpen = true; });
        const float width = 454f;
        selectionCard = MakeCanvas("SinglePatternCard", new Vector2(width, 220f), Vector3.zero);
        selectionCard.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.08f, 0.96f);
        selectionBackground = selectionCard.gameObject.AddComponent<BoxCollider>();
        selectionBackground.center = new Vector3(0f, 0f, 2f);
        selectedTitle = MakeText(selectionCard, "SingleTitle", 16f, 10f, width - 32f, 34f, 23);
        selectedSummary = MakeText(selectionCard, "SingleSummary", 16f, 48f, width - 32f, 30f, 17);
        selectedDescription = MakeText(selectionCard, "SingleDescription", 16f, 84f, width - 32f, 90f, 19);
        const float half = (width - 44f) / 2f;
        previousCandidate = MakeButton(selectionCard, "SinglePrevious", "< Pattern", new Rect(16f, 0f, half, 40f), () => single.CycleCandidate(-1));
        nextCandidate = MakeButton(selectionCard, "SingleNext", "Pattern >", new Rect(28f + half, 0f, half, 40f), () => single.CycleCandidate(1));
        drawSelected = MakeButton(selectionCard, "SingleStart", "Draw this pattern", new Rect(16f, 0f, half, 42f),
            () => { if (single.StartDrawing()) { detailsOpen = false; panelOpen = false; } });
        backToSky = MakeButton(selectionCard, "SingleBack", "Back to sky", new Rect(28f + half, 0f, half, 42f),
            () => { detailsOpen = false; });
        tag.gameObject.SetActive(false);
        selectionCard.gameObject.SetActive(false);
    }

    private void RefreshSelectionCard(bool browsing)
    {
        bool selected = browsing && single != null && single.HasSelection;
        if (!selected) detailsOpen = false;
        bool show = selected && !panelOpen;
        var tag = (RectTransform)detailsButton.Collider.transform.parent;
        tag.gameObject.SetActive(show && !detailsOpen);
        selectionCard.gameObject.SetActive(show && detailsOpen);
        if (!show) return;
        if (!detailsOpen)
        {
            if (displayedTagId != single.SelectedId)
            {
                displayedTagId = single.SelectedId;
                detailsButton.Label.text = single.SelectedName + "   ·   Details";
            }
            PositionBesideSky(tag);
            return;
        }
        if (displayedCardId == single.SelectedId && displayedCandidateCount == single.CandidateCount &&
            displayedCandidateIndex == single.CandidateIndex)
        {
            PositionBesideSky(selectionCard);
            return;
        }
        displayedCardId = single.SelectedId;
        displayedCandidateCount = single.CandidateCount;
        displayedCandidateIndex = single.CandidateIndex;
        selectedTitle.text = single.SelectedName;
        bool shared = single.CandidateCount > 1;
        selectedSummary.text = single.CultureLabel + (single.CanDraw
            ? $"  ·  {single.MemberCount} stars  ·  {single.LineCount} {(single.LineCount == 1 ? "line" : "lines")}"
            : "  ·  Single star  ·  Observation only");
        selectedSummary.color = single.SelectedIsChinese ? new Color(1f, 0.8f, 0.43f) : new Color(0.57f, 0.8f, 1f);
        selectedDescription.text = SinglePatternDescriptions.Get(single.SelectedId);
        float bodyHeight = Mathf.Max(48f, selectedDescription.preferredHeight + 5f);
        selectedDescription.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyHeight);
        float row = 84f + bodyHeight + 10f;
        previousCandidate.Collider.gameObject.SetActive(shared);
        nextCandidate.Collider.gameObject.SetActive(shared);
        if (shared)
        {
            SetButtonTop(previousCandidate, row);
            SetButtonTop(nextCandidate, row);
            nextCandidate.Label.text = $"Pattern {single.CandidateIndex + 1}/{single.CandidateCount}  >";
            row += 48f;
        }
        drawSelected.Collider.gameObject.SetActive(single.CanDraw);
        SetButtonTop(drawSelected, row);
        SetButtonTop(backToSky, row);
        selectionCard.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, row + 54f);
        selectionBackground.size = new Vector3(selectionCard.rect.width, selectionCard.rect.height, 0.1f);
        PositionBesideSky(selectionCard);
    }

    private static void SetButtonTop(MenuButton button, float top)
    {
        var rect = (RectTransform)button.Collider.transform;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top);
    }

    private void PositionBesideSky(RectTransform rect)
    {
        const float distance = 2.2f;
        var corner = viewCamera.transform.InverseTransformPoint(viewCamera.ViewportToWorldPoint(new Vector3(0.96f, 0.94f, distance)));
        float scale = Mathf.Min(0.002f, Mathf.Abs(corner.x) * 2f / rect.rect.width, Mathf.Abs(corner.y) * 2f / rect.rect.height);
        rect.localScale = Vector3.one * scale;
        float offsetX = Mathf.Clamp(Mathf.Abs(corner.x) - rect.rect.width * scale * 0.5f, 0f, 0.95f);
        float offsetY = Mathf.Clamp(Mathf.Abs(corner.y) - rect.rect.height * scale * 0.5f, 0f, 0.25f);
        rect.localPosition = new Vector3(cardSide * offsetX, -offsetY, distance);
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

    private void SyncComparisonMenuState()
    {
        if (comparison.IsActive == wasComparisonActive) return;
        wasComparisonActive = comparison.IsActive;
        panelOpen = comparison.IsActive && !comparison.IsPracticing;
        aboutOpen = drawingChoicesOpen = false;
    }

    private void RefreshInterface()
    {
        if (interfaceRoot == null || comparison == null) return;
        SyncComparisonMenuState();
        if (single != null) single.RefreshSelection();
        bool active = comparison.IsActive, practicing = comparison.IsPracticing;
        bool individual = DrawingIndividual;
        RefreshSelectionCard(!active && !individual);
        bool showInterface = active || individual || panelOpen;
        if (practicing && !wasPracticing) aboutOpen = false;
        wasPracticing = practicing;
        panel.gameObject.SetActive(showInterface && panelOpen);
        openButton.Collider.transform.parent.gameObject.SetActive((active || individual) && !panelOpen);
        if (!showInterface)
        {
            aboutOpen = drawingChoicesOpen = false;
            if (single == null || !single.HasSelection) HidePointer();
            return;
        }
        if (individual)
        {
            RefreshIndividualMenu();
            return;
        }
        titleLabel.text = active ? $"[{comparison.CaseIndex + 1}/{comparison.CaseCount}] {comparison.ChineseName} / {comparison.WesternName}" :
            "Sky browser / " + (field != null && field.BrowseChineseAvailable ? field.BrowseWesternAvailable ? "Both" : "Chinese" : "Western");
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
        if (!active && field != null)
        {
            string westernState = !field.BrowseWesternAvailable ? "not in this view" :
                field.IsWesternPatternVisible(westernPatternIndex) ? "shown" : "hidden";
            string regionName = chinese != null ? chinese.SelectedRegionNameEn?.Replace(" (Selection)", string.Empty) ?? "Unavailable" : "Unavailable";
            statusLabel.text = $"Western [{westernPatternIndex + 1}/{field.WesternPatternCount}]: {field.GetWesternPatternName(westernPatternIndex)} ({westernState})\n" +
                $"Chinese [{(chinese != null ? chinese.SelectedRegionIndex + 1 : 0)}/{(chinese != null ? chinese.RegionCount : 0)}]: {regionName}";
        }
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
            active ? "Choose Start drawing to draw Chinese, Western, or both." :
                chinese != null && chinese.IsRegionVisible ? "Focus: " + chinese.FocusedAsterismNameEn + " | Left grip / M: menu" :
                "Choose a pattern, then Show / Hide. Left grip / M: menu.";
        if (!string.IsNullOrEmpty(trackingHint)) inputLabel.text = trackingHint;
        openButton.Label.text = practicing ? (comparison.PracticePhase == SkyCultureComparisonController.DrawingPhase.Western ? "Western | Open menu" :
            comparison.PracticePhase == SkyCultureComparisonController.DrawingPhase.Chinese ? "Chinese | Open menu" : "Review | Open menu") : "Open menu";
        if (showFeedback) openButton.Label.text = comparison.PracticeFeedbackCorrect ? "Correct! | Open menu" : "Try again | Open menu";
        openButton.Label.color = showFeedback ? feedbackColour : Color.white;

        foreach (var button in buttons)
        {
            if (button.Collider.transform.parent != panel) continue;
            bool visible = active;
            bool enabled = true;
            switch (button.Name)
            {
                case "PreviousCase": case "NextCase": case "ExitComparison":
                case "SwitchCulture": case "BothViews": case "StartDrawing": visible = active && !practicing; break;
                case "BrowseCulture": case "BrowseBoth": case "EnterComparison": case "BrowseClose":
                    visible = !active; enabled = field != null; break;
                case "WesternPrevious": case "WesternToggle": case "WesternNext":
                    visible = !active; enabled = field != null && field.BrowseWesternAvailable; break;
                case "RegionPrevious": case "RegionToggle": case "RegionNext":
                    visible = !active; enabled = field != null && field.BrowseChineseAvailable && chinese != null; break;
                case "FocusPrevious": case "FocusNext":
                    visible = !active; enabled = field != null && field.BrowseChineseAvailable && chinese != null && chinese.IsRegionVisible; break;
                case "DrawChinese": case "DrawWestern": case "DrawBoth": visible = active && !practicing && drawingChoicesOpen; break;
                case "CancelSelection": visible = active && practicing; enabled = comparison.PracticeSelectedHr != 0; break;
                case "Undo": visible = active && practicing; enabled = comparison.PracticePhase != SkyCultureComparisonController.DrawingPhase.Review; break;
                case "Reset": case "Answer": case "BackToObserve": visible = active && practicing; break;
                case "Advance": visible = active && practicing; enabled = comparison.PracticePhase != SkyCultureComparisonController.DrawingPhase.Review; break;
                case "AboutUp": case "AboutDown": visible = active && aboutOpen; break;
            }
            SetMenuButton(button, visible, enabled);
            if (button.Name == "BackToObserve") button.Label.text = "Back to observe";
            if (button.Name == "Answer") button.Label.text = comparison.PracticeAnswerVisible ? "Your drawing" : "Show answer";
            if (button.Name == "About") button.Label.text = aboutOpen ? "Close introduction" : "About this case";
            if (button.Name == "StartDrawing") button.Label.text = drawingChoicesOpen ? "Close choices" : "Start drawing";
            if (button.Name == "BrowseBoth") button.Label.text = field != null && field.IsBothCulturesVisible ? "Return to one culture" : "Show both cultures";
            if (button.Name == "WesternToggle") button.Label.text = field != null && field.IsWesternPatternVisible(westernPatternIndex) ? "Hide" : "Show";
            if (button.Name == "RegionToggle") button.Label.text = chinese != null && chinese.IsRegionVisible ? "Hide Chinese" : "Show Chinese";
            if (button.Name == "Advance") button.Label.text = comparison.PracticeMode == SkyCultureComparisonController.DrawingMode.Both &&
                comparison.PracticePhase == SkyCultureComparisonController.DrawingPhase.Chinese ? "Check Chinese / Next" : "Finish / Check drawing";
        }
        bool showAbout = active && aboutOpen;
        aboutViewport.gameObject.SetActive(showAbout);
        float height = !active ? 338f : showAbout ? 526f : drawingChoicesOpen && !practicing ? 298f : 258f;
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

    private static void SetMenuButton(MenuButton button, bool visible, bool enabled)
    {
        button.Collider.gameObject.SetActive(visible);
        button.Enabled = enabled;
        button.Label.color = enabled ? Color.white : new Color(0.5f, 0.54f, 0.6f, 1f);
    }

    private void RefreshIndividualMenu()
    {
        titleLabel.text = single.SelectedName;
        string progress = $"{single.CultureLabel}  ·  Your lines: {single.DrawnLineCount}";
        bool feedback = !single.AnswerVisible && !string.IsNullOrEmpty(single.Feedback);
        if (feedback)
        {
            string colour = single.FeedbackCorrect ? "#6EFF9A" : "#FFC76B";
            statusLabel.text = $"<color={colour}>{single.Feedback}</color>\n{progress}";
        }
        else statusLabel.text = (single.AnswerVisible ? "Standard answer" : "Draw this pattern") + "\n" + progress;
        inputLabel.text = !string.IsNullOrEmpty(trackingHint) ? trackingHint :
            single.AnswerVisible ? "Choose Your drawing to continue." :
            UsingHeadset ? "Point at two outlined stars and press the right trigger on each." : "Click two outlined stars to connect. Right-drag to look.";
        openButton.Label.text = feedback ? (single.FeedbackCorrect ? "Correct! | Open menu" : "Try again | Open menu") : "Drawing | Open menu";
        openButton.Label.color = feedback ? (single.FeedbackCorrect ? new Color(0.43f, 1f, 0.6f) : new Color(1f, 0.78f, 0.42f)) : Color.white;
        aboutViewport.gameObject.SetActive(false);
        panel.sizeDelta = new Vector2(PanelWidth, 258f);
        panelCollider.size = new Vector3(PanelWidth, 258f, 0.1f);
        foreach (var button in buttons)
        {
            if (button.Collider.transform.parent != panel) continue;
            bool enabled = true;
            switch (button.Name)
            {
                case "CancelSelection": enabled = !single.AnswerVisible && single.HasPendingStart; break;
                case "Undo": enabled = !single.AnswerVisible && (single.DrawnLineCount > 0 || single.HasPendingStart); break;
                case "Advance": button.Label.text = "Finish / Check drawing"; enabled = !single.AnswerVisible; break;
                case "Answer": button.Label.text = single.AnswerVisible ? "Your drawing" : "Show answer"; break;
                case "BackToObserve": button.Label.text = "Back to sky"; break;
                case "Reset": case "HidePanel": break;
                default: SetMenuButton(button, false, false); continue;
            }
            SetMenuButton(button, true, enabled);
        }
    }

    private void SelectWesternPattern(int direction)
    {
        if (field == null || field.WesternPatternCount == 0) return;
        westernPatternIndex = (westernPatternIndex + direction + field.WesternPatternCount) % field.WesternPatternCount;
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
        panelOpen = false;
        aboutOpen = drawingChoicesOpen = false;
    }

    private void BackToObserve()
    {
        if (!DrawingIndividual) { comparison.StopPractice(); return; }
        single.ExitDrawing();
        panelOpen = detailsOpen = false;
    }

    private void ToggleAnswer()
    {
        if (DrawingIndividual) single.ToggleAnswer();
        else if (comparison.PracticeAnswerVisible) comparison.HidePracticeAnswer();
        else comparison.RevealPracticeAnswer();
    }

    private void ConsumePointerPress()
    {
        suppressLegacyThroughFrame = Time.frameCount;
        if (UsingHeadset && confirmAction != null && confirmAction.GetState(SteamVR_Input_Sources.RightHand))
            consumedTriggerHeld = true;
        SyncLegacyDrawing();
    }

    private void SyncLegacyDrawing()
    {
        if (consumedTriggerHeld && (!UsingHeadset || confirmAction == null || !confirmAction.GetState(SteamVR_Input_Sources.RightHand)))
        {
            consumedTriggerHeld = false;
            suppressLegacyThroughFrame = Time.frameCount;
        }
        bool shouldSuspend = comparison != null && comparison.IsActive || DrawingIndividual || detailsOpen || consumedTriggerHeld ||
            chinese != null && chinese.IsRegionVisible || panelOpen || Time.frameCount <= suppressLegacyThroughFrame;
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
