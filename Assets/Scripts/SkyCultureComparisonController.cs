using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

// Comparison and drawing; StarField pauses and restores the ordinary sky views.
public class SkyCultureComparisonController : MonoBehaviour
{
    public enum ComparisonView { Western, Chinese, Both }
    public enum DrawingMode { None, Chinese, Western, Both }
    public enum DrawingPhase { None, Chinese, Western, Review }
    private static readonly Color ChineseColour = new Color(1f, 0.78f, 0.3f, 0.95f);
    private static readonly Color WesternColour = new Color(0.3f, 0.65f, 1f, 0.95f);
    private static readonly Color SharedColour = new Color(1f, 79f / 255f, 216f / 255f, 0.95f);
    private static readonly HashSet<string> OriginalLabelNames = new HashSet<string>
    {
        "Draco_label", "Pegasus_label", "Cetus_label", "Aries_label", "Perseus_label",
        "Auriga_label", "Orion_label", "CanisMajor_label", "Monoceros_label", "Lynx_label",
        "Bootes_label", "Libra_label", "Ophiuchus_label", "Capricornus_label"
    };

    private sealed class ComparisonCase
    {
        public string ChineseName, WesternName, Note, About;
        public HashSet<int> Members, SharedStars;
        public HashSet<(int, int)> ChineseEdges, WesternEdges;
        public int SharedEdges;
    }

    private readonly Dictionary<int, GameObject> starsByHr = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, Color> savedColours = new Dictionary<int, Color>();
    private readonly Dictionary<GameObject, bool> savedVisibility = new Dictionary<GameObject, bool>();
    private ComparisonCase[] cases;
    private GameObject drawing;
    private Material lineMaterial;
    private Font labelFont;
    private GUIStyle infoStyle;
    private bool chineseSingle, both;
    private Camera savedCamera;
    private Quaternion savedCameraRotation;
    private bool restoreCamera;
    private readonly List<(int, int)> chineseDrawing = new List<(int, int)>();
    private readonly List<(int, int)> westernDrawing = new List<(int, int)>();

    public bool IsReady { get; private set; }
    public bool IsActive { get; private set; }
    public int CaseIndex { get; private set; }
    public int CaseCount => cases == null ? 0 : cases.Length;
    public ComparisonView CurrentView => both ? ComparisonView.Both :
        chineseSingle ? ComparisonView.Chinese : ComparisonView.Western;
    public string ChineseName => IsReady ? cases[CaseIndex].ChineseName : null;
    public string WesternName => IsReady ? cases[CaseIndex].WesternName : null;
    public int SharedStarCount => IsReady ? cases[CaseIndex].SharedStars.Count : 0;
    public int SharedEdgeCount => IsReady ? cases[CaseIndex].SharedEdges : 0;
    public int ChineseOnlyEdgeCount => IsReady ? cases[CaseIndex].ChineseEdges.Count - SharedEdgeCount : 0;
    public int WesternOnlyEdgeCount => IsReady ? cases[CaseIndex].WesternEdges.Count - SharedEdgeCount : 0;
    public int VisibleLineCount { get; private set; }
    public int VisibleRingCount { get; private set; }
    public DrawingMode PracticeMode { get; private set; }
    public DrawingPhase PracticePhase { get; private set; }
    public bool IsPracticing => PracticeMode != DrawingMode.None;
    public bool PracticeAnswerVisible { get; private set; }
    public string PracticeFeedbackText { get; private set; } = string.Empty;
    public bool PracticeFeedbackCorrect { get; private set; }
    public int PracticeChineseLineCount => chineseDrawing.Count;
    public int PracticeWesternLineCount => westernDrawing.Count;
    public int PracticeSelectedHr { get; private set; }
    public string CaseAboutText => IsReady ? cases[CaseIndex].About : string.Empty;
    public Vector3 CaseCentre
    {
        get
        {
            if (!IsReady) return Vector3.zero;
            Vector3 centre = Vector3.zero;
            foreach (int hr in cases[CaseIndex].Members) centre += starsByHr[hr].transform.position;
            return centre / cases[CaseIndex].Members.Count;
        }
    }
    public string PracticeStatusText
    {
        get
        {
            if (!IsPracticing) return "Observe the two sky traditions, then choose a drawing mode.";
            if (PracticeAnswerVisible) return "Standard answer. Choose Your Drawing to compare your own lines.";
            if (!string.IsNullOrEmpty(PracticeFeedbackText)) return PracticeFeedbackText;
            if (PracticePhase == DrawingPhase.Review)
                return "Your drawing. Choose Show Answer to compare, or Reset to try again.";
            string target = PracticePhase == DrawingPhase.Chinese ? "Chinese asterism" : "Western constellation";
            string reference = PracticeMode == DrawingMode.Chinese ? " Western lines are a reference." :
                PracticeMode == DrawingMode.Western ? " Chinese lines are a reference." : " No standard lines are shown.";
            return "Draw the " + target + ". Select two stars for each line." + reference;
        }
    }

    public void Initialize(List<StarDataLoader.Star> stars, List<GameObject> starObjects,
        ChineseAsterismController chinese, IReadOnlyList<(int[], int[])> constellations)
    {
        if (IsReady || chinese == null || !chinese.IsReady) return;
        try
        {
            starsByHr.Clear();
            for (int i = 0; i < stars.Count; i++)
                starsByHr.Add(Mathf.RoundToInt(stars[i].catalog_number), starObjects[i]);
            // 西方数组直接来自原 StarField；中文定义取同一份已加载目录。
            cases = new[]
            {
                MakeCase(chinese, "chinese_266", "Auriga", constellations[5],
                    "Five shared stars, with different connections."),
                MakeCase(chinese, "chinese_003", "Orion", constellations[6],
                    "Three Stars forms the seven-star core of this Orion figure."),
                MakeCase(chinese, "chinese_004", "Libra", constellations[11],
                    "Three shared stars; Root includes one star this Libra figure omits."),
                MakeCase(chinese, "chinese_208", "Draco", constellations[0],
                    "Four shared stars; the Flail includes one star this Draco figure omits."),
                MakeCase(chinese, "chinese_014", "Aries", constellations[3],
                    "All three Bond stars are shared with this Aries figure."),
                MakeCase(chinese, "chinese_210", "Cetus", constellations[2],
                    "Five shared stars; the Granary includes one star this Cetus figure omits.")
            };
            var shader = Resources.Load<Shader>("ChineseAsterismLine");
            if (shader == null || !shader.isSupported)
                throw new ArgumentException("Comparison line shader is unavailable.");
            lineMaterial = new Material(shader) { name = "Sky culture comparison lines (runtime)" };
            labelFont = chinese.LabelFont;
            IsReady = true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Sky culture comparison: " + exception.Message, this);
        }
    }

    private ComparisonCase MakeCase(ChineseAsterismController chinese, string id,
        string westernName, (int[], int[]) western, string note)
    {
        if (!chinese.TryGetAsterism(id, out var definition))
            throw new ArgumentException("Missing comparison asterism: " + id);
        var chineseStars = new HashSet<int>();
        foreach (string member in definition.memberIds) chineseStars.Add(chinese.GetObjectHr(member));
        var result = new ComparisonCase
        {
            ChineseName = ChineseAsterismController.GetDisplayName(definition),
            WesternName = westernName,
            Note = note,
            About = AboutText(id),
            Members = new HashSet<int>(western.Item1),
            SharedStars = new HashSet<int>(chineseStars),
            ChineseEdges = new HashSet<(int, int)>(),
            WesternEdges = new HashSet<(int, int)>()
        };
        result.SharedStars.IntersectWith(result.Members);
        result.Members.UnionWith(chineseStars);
        foreach (int hr in result.Members)
            if (!starsByHr.ContainsKey(hr)) throw new ArgumentException("Missing comparison HR " + hr);
        foreach (var edge in definition.segments)
            result.ChineseEdges.Add(Edge(chinese.GetObjectHr(edge.fromId), chinese.GetObjectHr(edge.toId)));
        for (int i = 0; i < western.Item2.Length; i += 2)
            result.WesternEdges.Add(Edge(western.Item2[i], western.Item2[i + 1]));
        foreach (var edge in result.ChineseEdges)
            if (result.WesternEdges.Contains(edge)) result.SharedEdges++;
        return result;
    }

    private static string AboutText(string id)
    {
        switch (id)
        {
            case "chinese_266":
                return @"CHINESE VIEW
Five Chariots is a five-star asterism in the Net Mansion region. Its name places chariots in the sky, reflecting the objects and activities used in traditional Chinese sky imagery.

WESTERN VIEW
Auriga is the Charioteer. This project draws a larger eight-star figure around the same sky.

COMPARE
All five Chinese members appear in this Auriga figure. Three connections are shared; the other connections differ.

LOOK FOR
Notice how changing the connections changes the figure without moving the stars. These are selected line figures, not complete constellation boundaries.";
            case "chinese_003":
                return @"CHINESE VIEW
Three Stars is the principal asterism of the Three Stars Mansion. Its name draws attention to the central row of three stars; this catalogue's complete figure uses seven members.

WESTERN VIEW
Orion is the Hunter. The same central row is often called Orion's Belt. This project draws an eighteen-star Orion figure.

COMPARE
All seven Chinese members and all six Chinese connections also occur in this Orion figure. This case shows a shared core within a larger grouping.

LOOK FOR
Find the central row, then compare how far each figure extends around it.";
            case "chinese_004":
                return @"CHINESE VIEW
Root is the principal asterism of the Root Mansion, one of the seven eastern mansions associated with the Azure Dragon. This catalogue draws it with four stars and three connections.

WESTERN VIEW
Libra represents the Scales. This project uses five stars to draw its selected Libra figure.

COMPARE
Three stars are shared. Root includes another member that this Western line figure omits; this does not mean that star lies outside the official constellation boundary.

LOOK FOR
Look for the additional Chinese member before you try to rebuild Root from memory.";
            case "chinese_208":
                return @"CHINESE VIEW
Celestial Flail belongs to the Purple Forbidden Enclosure. Its name evokes a flail, one of the many everyday or court-related objects represented in the traditional Chinese sky. This figure has five members.

WESTERN VIEW
Draco is the Dragon. The project's thirteen-star figure follows a much longer chain across the northern sky.

COMPARE
Four stars and three connections are shared. The Chinese figure also uses one member omitted from this Western line figure.

LOOK FOR
Compare the compact Flail with the Dragon's extended chain; neither figure requires moving the stars.";
            case "chinese_014":
                return @"CHINESE VIEW
Bond is the principal asterism of the Bond Mansion, one of the seven western mansions associated with the White Tiger. This compact figure uses three stars joined by two connections.

WESTERN VIEW
Aries is the Ram. The project's four-star Aries figure extends the same small grouping by one star.

COMPARE
All three Chinese members and both Chinese connections occur in this Aries line figure. This is a simple example of two cultures choosing different extents for a figure.

LOOK FOR
Notice which endpoint continues farther in the Western figure.";
            case "chinese_210":
                return @"CHINESE VIEW
Square Celestial Granary belongs to the Bond Mansion region. Its name represents a store for grain, showing how practical activities on Earth could become images in the sky. This figure uses six members.

WESTERN VIEW
Cetus is the Sea Monster. The project's selected Western figure uses fifteen stars across a much larger area.

COMPARE
Five Chinese members appear in the Western figure, with four shared connections. One Chinese member is absent from that selected figure.

LOOK FOR
Follow the compact Granary, then see how its shared stars contribute to the larger Sea Monster.";
            default: return string.Empty;
        }
    }

    private static (int, int) Edge(int a, int b) { return a < b ? (a, b) : (b, a); }

    // 由 StarField 调用，确保普通模式状态先暂停。
    public void EnterComparison()
    {
        if (!IsReady || IsActive) return;
        IsActive = true;
        CaseIndex = 0;
        ClearPracticeState();
        chineseSingle = both = false;
        savedCamera = Camera.main;
        restoreCamera = savedCamera != null && !IsVrActive(savedCamera);
        if (restoreCamera) savedCameraRotation = savedCamera.transform.rotation;
        HideOriginalLabels();
        HighlightCaseStars();
        RebuildDrawing();
        AimAtCase();
    }

    public void ExitComparison()
    {
        if (!IsActive) return;
        IsActive = false;
        ClearPracticeState();
        ClearDrawing();
        RestoreStarColours();
        foreach (var entry in savedVisibility)
            if (entry.Key != null) entry.Key.SetActive(entry.Value);
        savedVisibility.Clear();
        if (restoreCamera && savedCamera != null && !IsVrActive(savedCamera))
            savedCamera.transform.rotation = savedCameraRotation;
        savedCamera = null;
        restoreCamera = false;
    }

    public void SelectNextCase(int direction)
    {
        if (!IsActive || direction == 0) return;
        ClearPracticeState();
        RestoreStarColours();
        CaseIndex = (CaseIndex + (direction < 0 ? -1 : 1) + cases.Length) % cases.Length;
        HighlightCaseStars();
        RebuildDrawing();
        AimAtCase();
    }

    public void SwitchCulture()
    {
        if (!IsActive || IsPracticing) return;
        chineseSingle = !chineseSingle;
        both = false;
        RebuildDrawing();
    }

    public void ToggleBoth()
    {
        if (!IsActive || IsPracticing) return;
        both = !both;
        RebuildDrawing();
    }

    public void StartPractice(DrawingMode mode)
    {
        if (!IsActive || (mode != DrawingMode.Chinese && mode != DrawingMode.Western && mode != DrawingMode.Both)) return;
        PracticeMode = mode;
        ResetPractice();
    }

    public void StopPractice()
    {
        if (!IsActive || !IsPracticing) return;
        ClearPracticeState();
        RebuildDrawing();
    }

    private void ClearPracticeState()
    {
        chineseDrawing.Clear();
        westernDrawing.Clear();
        PracticeMode = DrawingMode.None;
        PracticePhase = DrawingPhase.None;
        PracticeSelectedHr = 0;
        PracticeAnswerVisible = false;
        ClearPracticeFeedback();
    }

    public void ResetPractice()
    {
        if (!IsActive || !IsPracticing) return;
        chineseDrawing.Clear();
        westernDrawing.Clear();
        PracticeSelectedHr = 0;
        PracticeAnswerVisible = false;
        ClearPracticeFeedback();
        PracticePhase = PracticeMode == DrawingMode.Western ? DrawingPhase.Western : DrawingPhase.Chinese;
        RebuildDrawing();
    }

    private bool CanDraw => IsActive && IsPracticing && !PracticeAnswerVisible &&
        (PracticePhase == DrawingPhase.Chinese || PracticePhase == DrawingPhase.Western);

    private List<(int, int)> CurrentDrawing => PracticePhase == DrawingPhase.Chinese ? chineseDrawing : westernDrawing;

    public bool SelectPracticeStar(int hr)
    {
        if (!CanDraw || !cases[CaseIndex].Members.Contains(hr)) return false;
        ClearPracticeFeedback();
        if (PracticeSelectedHr == 0)
            PracticeSelectedHr = hr;
        else
        {
            // Two confirmations make one segment. A third click starts a new segment.
            if (PracticeSelectedHr != hr)
            {
                var edge = Edge(PracticeSelectedHr, hr);
                if (!CurrentDrawing.Contains(edge)) CurrentDrawing.Add(edge);
            }
            PracticeSelectedHr = 0;
        }
        RebuildDrawing();
        return true;
    }

    public bool TryPickPracticeStar(Ray ray, out int hr, out Vector3 worldPosition)
    {
        hr = 0;
        worldPosition = Vector3.zero;
        if (!CanDraw || ray.direction.sqrMagnitude < 0.000001f) return false;
        Vector3 direction = ray.direction.normalized;
        float bestDot = Mathf.Cos(Mathf.Deg2Rad);
        foreach (int member in cases[CaseIndex].Members)
        {
            Vector3 position = starsByHr[member].transform.position;
            Vector3 offset = position - ray.origin;
            if (Vector3.Dot(offset, direction) <= 0f) continue;
            float dot = Vector3.Dot(offset.normalized, direction);
            if (dot > bestDot || (hr != 0 && Mathf.Approximately(dot, bestDot) && member < hr))
            {
                bestDot = dot;
                hr = member;
                worldPosition = position;
            }
        }
        return hr != 0;
    }

    public void CancelPracticeSelection()
    {
        if (!IsActive || !IsPracticing || PracticeSelectedHr == 0) return;
        PracticeSelectedHr = 0;
        ClearPracticeFeedback();
        RebuildDrawing();
    }

    public void UndoPracticeLine()
    {
        if (!CanDraw) return;
        PracticeSelectedHr = 0;
        ClearPracticeFeedback();
        if (CurrentDrawing.Count > 0) CurrentDrawing.RemoveAt(CurrentDrawing.Count - 1);
        RebuildDrawing();
    }

    public void AdvancePractice()
    {
        if (!CanDraw) return;
        PracticeSelectedHr = 0;
        bool drawingChinese = PracticePhase == DrawingPhase.Chinese;
        var expected = drawingChinese ? cases[CaseIndex].ChineseEdges : cases[CaseIndex].WesternEdges;
        PracticeFeedbackCorrect = MatchesReference(CurrentDrawing, expected);
        if (!PracticeFeedbackCorrect)
            PracticeFeedbackText = "Not matched yet. Keep drawing or undo a line.";
        else if (PracticeMode == DrawingMode.Both && drawingChinese)
        {
            PracticeFeedbackText = "Chinese pattern matched! Now draw Western.";
            PracticePhase = DrawingPhase.Western;
        }
        else
        {
            PracticeFeedbackText = PracticeMode == DrawingMode.Both ? "Correct! Both patterns completed." :
                drawingChinese ? "Correct! Chinese pattern completed." : "Correct! Western pattern completed.";
            PracticePhase = DrawingPhase.Review;
        }
        RebuildDrawing();
    }

    private static bool MatchesReference(List<(int, int)> answer, HashSet<(int, int)> expected)
    {
        if (answer.Count != expected.Count) return false;
        foreach (var edge in answer)
            if (!expected.Contains(edge)) return false;
        return true;
    }

    private void ClearPracticeFeedback()
    {
        PracticeFeedbackText = string.Empty;
        PracticeFeedbackCorrect = false;
    }

    public void RevealPracticeAnswer()
    {
        if (!IsActive || !IsPracticing) return;
        PracticeSelectedHr = 0;
        ClearPracticeFeedback();
        PracticePhase = DrawingPhase.Review;
        PracticeAnswerVisible = true;
        RebuildDrawing();
    }

    public void HidePracticeAnswer()
    {
        if (!IsActive || !IsPracticing || !PracticeAnswerVisible) return;
        PracticeAnswerVisible = false;
        RebuildDrawing();
    }

    private void HighlightCaseStars()
    {
        foreach (int hr in cases[CaseIndex].Members)
        {
            var material = starsByHr[hr].GetComponent<MeshRenderer>().material;
            savedColours.Add(hr, material.color);
            material.color = Color.white;
        }
    }

    private void RestoreStarColours()
    {
        foreach (var entry in savedColours)
            if (starsByHr[entry.Key] != null)
                starsByHr[entry.Key].GetComponent<MeshRenderer>().material.color = entry.Value;
        savedColours.Clear();
    }

    private void RebuildDrawing()
    {
        ClearDrawing();
        drawing = new GameObject("SkyCultureComparison_Case" + CaseIndex);
        drawing.transform.SetParent(transform, false);
        var item = cases[CaseIndex];
        if (IsPracticing && !PracticeAnswerVisible)
            DrawPractice(item);
        else
            DrawStandard(item, PracticeAnswerVisible ? ComparisonView.Both : CurrentView);
    }

    private void DrawStandard(ComparisonCase item, ComparisonView view)
    {
        bool showBoth = view == ComparisonView.Both;
        if (view != ComparisonView.Western)
            foreach (var edge in item.ChineseEdges)
            {
                bool shared = showBoth && item.WesternEdges.Contains(edge);
                DrawEdge(edge, shared ? SharedColour : ChineseColour,
                    shared ? "SharedEdge" : "ChineseEdge", shared ? 1.2f : 0.8f);
            }
        if (view != ComparisonView.Chinese)
            foreach (var edge in item.WesternEdges)
                if (!showBoth || !item.ChineseEdges.Contains(edge)) DrawEdge(edge, WesternColour, "WesternEdge");
        if (showBoth)
            foreach (int hr in item.SharedStars) DrawSharedRing(hr);
    }

    private void DrawPractice(ComparisonCase item)
    {
        foreach (int hr in item.Members)
            DrawStarRing(hr, "PracticeTargetStarRing", 0.2f, Color.white, 3.2f);
        if (PracticeMode == DrawingMode.Chinese)
        {
            Color reference = WesternColour;
            reference.a = 0.45f;
            foreach (var edge in item.WesternEdges) DrawEdge(edge, reference, "ReferenceWesternEdge", 0.5f);
        }
        else if (PracticeMode == DrawingMode.Western)
        {
            Color reference = ChineseColour;
            reference.a = 0.45f;
            foreach (var edge in item.ChineseEdges) DrawEdge(edge, reference, "ReferenceChineseEdge", 0.5f);
        }
        foreach (var edge in chineseDrawing)
        {
            // This tests the two user drawings, never the hidden standard answer.
            bool shared = westernDrawing.Contains(edge);
            DrawEdge(edge, shared ? SharedColour : ChineseColour,
                shared ? "PracticeSharedEdge" : "PracticeChineseEdge", shared ? 1.2f : 1f);
        }
        foreach (var edge in westernDrawing)
            if (!chineseDrawing.Contains(edge)) DrawEdge(edge, WesternColour, "PracticeWesternEdge", 1f);
        if (PracticeSelectedHr != 0)
            DrawStarRing(PracticeSelectedHr, "PracticeSelectedStar", 0.35f, Color.white, 4.2f);
    }

    private Vector3 LocalPosition(int hr)
    {
        return transform.InverseTransformPoint(starsByHr[hr].transform.position);
    }

    private LineRenderer NewLine(string name, float width, Color colour)
    {
        var go = new GameObject(name);
        go.transform.SetParent(drawing.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = colour;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void DrawEdge((int, int) edge, Color colour, string kind, float width = 0.8f)
    {
        var line = NewLine(kind + "_" + edge.Item1 + "_" + edge.Item2, width, colour);
        Vector3 a = LocalPosition(edge.Item1), b = LocalPosition(edge.Item2);
        Vector3 trim = (b - a).normalized * Mathf.Min(3f, (b - a).magnitude * 0.2f);
        line.positionCount = 2;
        line.SetPosition(0, a + trim);
        line.SetPosition(1, b - trim);
        VisibleLineCount++;
    }

    private void DrawSharedRing(int hr)
    {
        DrawStarRing(hr, "SharedStarRing", 0.2f, SharedColour, 3.2f);
    }

    private void DrawStarRing(int hr, string kind, float width, Color colour, float radius)
    {
        Vector3 position = LocalPosition(hr), normal = position.normalized;
        Vector3 right = Vector3.Cross(normal, Vector3.up);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(normal, Vector3.right);
        right.Normalize();
        Vector3 up = Vector3.Cross(normal, right).normalized;
        var line = NewLine(kind + "_" + hr, width, colour);
        line.loop = true;
        line.positionCount = 32;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, position - normal * 3f + radius * (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)));
        }
        VisibleRingCount++;
    }

    private void ClearDrawing()
    {
        if (drawing != null) { drawing.SetActive(false); Release(drawing); }
        drawing = null;
        VisibleLineCount = VisibleRingCount = 0;
    }

    private static bool IsVrActive(Camera camera)
    {
        // 场景中存在 CameraRig 不代表头显正在运行；不触发 SteamVR 初始化。
        return XRSettings.isDeviceActive || (camera != null && camera.stereoEnabled);
    }

    private void AimAtCase()
    {
        var camera = Camera.main;
        if (camera == null || IsVrActive(camera)) return;
        Vector3 direction = CaseCentre - camera.transform.position;
        if (direction.sqrMagnitude > 0.001f)
            camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void HideOriginalLabels()
    {
        var camera = Camera.main;
        if (camera != null)
            foreach (var child in camera.GetComponentsInChildren<Transform>(true))
                if (child.name == "ColorPicker") HideTemporarily(child.gameObject);
        foreach (var root in gameObject.scene.GetRootGameObjects())
            if (OriginalLabelNames.Contains(root.name)) HideTemporarily(root);
    }

    private void HideTemporarily(GameObject item)
    {
        if (savedVisibility.ContainsKey(item)) return;
        savedVisibility.Add(item, item.activeSelf);
        item.SetActive(false);
    }

    private string[] InfoLines(int index)
    {
        var item = cases[index];
        string title = $"Case [{index + 1}/{cases.Length}]  {item.ChineseName} × {item.WesternName}   |   {CurrentView}";
        string counts = $"Shared stars: {item.SharedStars.Count}   Shared edges: {item.SharedEdges} (one asterism)";
        string legend = "<color=#FFC74D>Gold: Chinese</color>   <color=#4DA6FF>Blue: Western</color>   <color=#FF4FD8>Magenta: shared lines</color>";
        string keys = "V Exit   [ / ] Case   C Chinese / Western   B Both";
        return new[] { title, counts, legend, item.Note, keys };
    }
    private string[] CompactInfoLines(int index)
    {
        string[] lines = InfoLines(index);
        return new[] { lines[0] + $"   |   Shared stars: {cases[index].SharedStars.Count}", lines[2] };
    }
    private void OnGUI()
    {
        if (!IsActive) return;
        var controls = GetComponent<SkyPracticeVrControls>();
        if (controls != null && controls.isActiveAndEnabled) return;
        if (infoStyle == null)
            infoStyle = new GUIStyle(GUI.skin.label) { font = labelFont, fontSize = 16, richText = true };
        bool compact = Screen.height < 720;
        float width = 560f, height = compact ? 48f : 142f;
        infoStyle.fontSize = compact ? 14 : 16;
        float scale = Mathf.Min(1f, Mathf.Max(1f, Screen.width - 24f) / width, Screen.height * 0.3f / height);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.TRS(new Vector3(12f, Screen.height - 12f - height * scale, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        GUI.Box(new Rect(0f, 0f, width, height), GUIContent.none);
        string[] lines = compact ? CompactInfoLines(CaseIndex) : InfoLines(CaseIndex);
        for (int i = 0; i < lines.Length; i++)
            GUI.Label(new Rect(12f, compact ? 4f + i * 20f : 8f + i * 25f, width - 24f, 24f), lines[i], infoStyle);
        GUI.matrix = previousMatrix;
    }
    private void OnDisable()
    {
        if (IsActive) GetComponent<StarField>().ExitComparisonMode();
    }

    private void OnDestroy() { Release(lineMaterial); }

    private static void Release(UnityEngine.Object item)
    {
        if (item == null) return;
        if (Application.isPlaying) Destroy(item);
        else DestroyImmediate(item);
    }
}
