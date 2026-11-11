using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// StarField initializes this component after creating the original BSC5 stars.
// All catalogue files are local. No scene edit or additional package is needed.
public class ChineseAsterismController : MonoBehaviour
{
    private const string DefaultRegionId = "ziwei";
    // View choices only; the source catalogue below still contains all 312 asterisms.
    private static readonly ChineseAsterismDefinition[] Regions =
    {
        new ChineseAsterismDefinition { id = "ziwei", nameZh = "紫微垣精选", nameEn = "Purple Forbidden Enclosure (Selection)" },
        new ChineseAsterismDefinition { id = "taiwei", nameZh = "太微垣精选", nameEn = "Supreme Palace Enclosure (Selection)" },
        new ChineseAsterismDefinition { id = "tianshi", nameZh = "天市垣精选", nameEn = "Heavenly Market Enclosure (Selection)" },
        new ChineseAsterismDefinition { id = "shen", nameZh = "参宿天区精选", nameEn = "Three Stars Mansion (Selection)" },
        new ChineseAsterismDefinition { id = "bi", nameZh = "毕宿天区精选", nameEn = "Net Mansion (Selection)" },
        new ChineseAsterismDefinition { id = "niu", nameZh = "牛宿天区精选", nameEn = "Ox Mansion (Selection)" },
    };
    private static readonly string[][] RegionAsterismIds =
    {
        new[] { "beidou", "chinese_041", "chinese_083", "chinese_033", "chinese_032", "chinese_265" },
        new[] { "chinese_020", "chinese_019", "chinese_268", "chinese_131", "chinese_132", "chinese_049" },
        new[] { "chinese_022", "chinese_021", "chinese_065", "chinese_067", "chinese_098", "chinese_052", "chinese_140", "chinese_189" },
        new[] { "chinese_003", "chinese_069", "chinese_291", "chinese_123", "chinese_164" },
        new[] { "chinese_001", "chinese_266", "chinese_308", "chinese_224", "chinese_222", "chinese_046" },
        new[] { "chinese_016", "chinese_093", "chinese_304", "chinese_216", "chinese_156", "chinese_111" },
    };
    private readonly Dictionary<int, Transform> starsByHr = new Dictionary<int, Transform>();
    private readonly Dictionary<string, Transform> objectTransforms = new Dictionary<string, Transform>();
    private readonly Dictionary<string, ChineseSkyObject> objectDefinitions = new Dictionary<string, ChineseSkyObject>();
    private readonly List<Material> supplementalMaterials = new List<Material>();
    private readonly Dictionary<string, ChineseAsterismDefinition> asterismsById = new Dictionary<string, ChineseAsterismDefinition>();
    private readonly List<GameObject> activeHolders = new List<GameObject>();
    private readonly List<TextMesh> activeLabels = new List<TextMesh>();
    private ChineseAsterismCatalog catalog;
    private ChineseAsterismSelection selection;
    private GameObject supplementalHolder;
    private Material lineMaterial;
    private Font labelFont;
    private float sphereRadius = 400f;
    private int focusedAsterismIndex;
    private int focusedRegionIndex = -1;
    private GameObject focusMarkers;

    public bool IsReady { get; private set; }
    public int AsterismCount => catalog == null ? 0 : catalog.asterisms.Length;
    public int RegionCount => Regions.Length;
    public string ActiveRegionId => selection != null && selection.IsVisible ? selection.Current.id : null;
    public string SelectedRegionId => selection == null ? null : selection.Current.id;
    public int SelectedRegionIndex => selection == null ? -1 : selection.SelectedIndex;
    public string SelectedRegionName => selection == null ? null : selection.Current.nameZh;
    public string SelectedRegionNameEn => selection == null ? null : selection.Current.nameEn;
    public int VisibleSegmentCount { get; private set; }
    public int VisibleMarkerCount { get; private set; }
    public int SupplementalStarCount { get; private set; }
    public int VisibleAsterismCount => activeHolders.Count;
    public bool IsRegionVisible => IsReady && isActiveAndEnabled && selection != null && selection.IsVisible && activeHolders.Count > 0;
    public string FocusedAsterismId => IsReady && selection != null ?
        RegionAsterismIds[selection.SelectedIndex][focusedAsterismIndex] : null;
    public string FocusedAsterismNameEn => FocusedAsterismId != null ? GetDisplayName(asterismsById[FocusedAsterismId]) : null;

    // Short screen labels only; catalogue names and identities stay unchanged.
    public static string GetDisplayName(ChineseAsterismDefinition definition)
    {
        if (definition == null) return string.Empty;
        switch (definition.id)
        {
            case "chinese_020": case "chinese_022": case "chinese_033": return "Left Wall";
            case "chinese_019": case "chinese_021": case "chinese_032": return "Right Wall";
            case "chinese_265": return "Admin Centre";
            case "chinese_268": return "Five Emperors";
            case "chinese_131": return "Guard Captain";
            case "chinese_132": return "Imperial Guards";
            case "chinese_069": return "Armed Forces";
            case "chinese_308": return "Pillars";
            case "chinese_046": return "Three Stars Banner";
            case "chinese_208": return "Flail";
            case "chinese_210": return "Granary";
            default: return definition.nameEn;
        }
    }

    // 仅供同片星空对照读取，沿用已加载的目录与字体。
    public bool TryGetAsterism(string id, out ChineseAsterismDefinition definition)
    {
        return asterismsById.TryGetValue(id, out definition);
    }

    public int GetObjectHr(string id)
    {
        return objectDefinitions.TryGetValue(id, out var definition) ? definition.hr : 0;
    }

    public Font LabelFont => labelFont;
    public void Initialize(List<StarDataLoader.Star> stars, List<GameObject> starObjects)
    {
        if (IsReady) return;
        try
        {
            if (stars == null || starObjects == null || stars.Count != starObjects.Count)
                throw new ArgumentException("Star data and objects do not match.");
            HideRegion();
            Release(lineMaterial);
            Release(labelFont);
            lineMaterial = null;
            labelFont = null;
            foreach (var material in supplementalMaterials) Release(material);
            supplementalMaterials.Clear();
            Release(supplementalHolder);
            starsByHr.Clear();
            objectTransforms.Clear();
            objectDefinitions.Clear();
            asterismsById.Clear();
            SupplementalStarCount = 0;
            for (int i = 0; i < stars.Count; i++)
            {
                int hr = Mathf.RoundToInt(stars[i].catalog_number);
                if (starObjects[i] == null || starsByHr.ContainsKey(hr))
                    throw new ArgumentException("Missing or duplicate HR object: " + hr);
                starsByHr.Add(hr, starObjects[i].transform);
            }
            if (starObjects.Count > 0 && starObjects[0].transform.localPosition.magnitude > 1f)
                sphereRadius = starObjects[0].transform.localPosition.magnitude;

            var asset = Resources.Load<TextAsset>("ChineseAsterisms");
            if (asset == null) throw new ArgumentException("Missing Resources/ChineseAsterisms.json.");
            catalog = ChineseAsterismData.Parse(asset.text, new HashSet<int>(starsByHr.Keys));
            foreach (var definition in catalog.asterisms) asterismsById.Add(definition.id, definition);
            foreach (var regionIds in RegionAsterismIds)
                foreach (string id in regionIds)
                    if (!asterismsById.ContainsKey(id))
                        throw new ArgumentException("Missing selected-region asterism: " + id);
            var shader = Resources.Load<Shader>("ChineseAsterismLine");
            if (shader == null || !shader.isSupported)
                throw new ArgumentException("ChineseAsterismLine shader is missing or unsupported.");
            lineMaterial = new Material(shader) { name = "Chinese asterism lines (runtime)" };
            CreateLabelFont();

            supplementalHolder = new GameObject("ChineseSupplementalObjects");
            supplementalHolder.transform.SetParent(transform, false);
            foreach (var item in catalog.objects)
            {
                objectDefinitions.Add(item.id, item);
                Transform position;
                if (item.kind == "star" && item.hr > 0 && starsByHr.TryGetValue(item.hr, out position))
                {
                    objectTransforms.Add(item.id, position);
                    continue;
                }
                var go = item.kind == "star"
                    ? GameObject.CreatePrimitive(PrimitiveType.Quad) : new GameObject();
                go.name = item.id;
                go.transform.SetParent(supplementalHolder.transform, false);
                double cosDec = Math.Cos(item.decRadians);
                go.transform.localPosition = new Vector3(
                    (float)(cosDec * Math.Cos(item.raRadians)),
                    (float)Math.Sin(item.decRadians),
                    (float)(cosDec * Math.Sin(item.raRadians))) * sphereRadius;
                if (item.kind == "star")
                {
                    var starShader = Shader.Find("Unlit/StarShader");
                    if (starShader == null) throw new ArgumentException("The existing StarShader is unavailable.");
                    var material = new Material(starShader) { name = item.id + " (runtime)" };
                    float size = item.hasMagnitude
                        ? Mathf.Lerp(0f, 5f, 1f - Mathf.InverseLerp(-1.46f, 7.96f, item.magnitude)) : 1.2f;
                    // Keep very faint added members visible without asserting unknown photometry.
                    material.SetFloat("_Size", Mathf.Max(0.65f, size));
                    material.color = Color.white;
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                    supplementalMaterials.Add(material);
                    go.transform.LookAt(transform.position);
                    go.transform.Rotate(0, 180, 0);
                    var collider = go.GetComponent<Collider>();
                    if (collider != null) Release(collider);
                    SupplementalStarCount++;
                }
                objectTransforms.Add(item.id, go.transform);
            }
            // Reuse the keyboard state for six view choices, without changing the full catalogue.
            selection = new ChineseAsterismSelection(
                new ChineseAsterismCatalog { asterisms = Regions }, DefaultRegionId);
            IsReady = true;
            Debug.Log($"Chinese sky: {AsterismCount} catalogue entries, {RegionCount} selected regions. C: switch Western/Chinese; B: toggle both; T: default Purple Forbidden region; Y: show/hide whole region; [ / ]: replace region.", this);
        }
        catch (Exception exception)
        {
            Debug.LogError("Chinese asterisms: " + exception.Message, this);
        }
    }

    public void ToggleDefaultRegion()
    {
        if (!IsReady || !isActiveAndEnabled || !selection.Toggle(DefaultRegionId)) return;
        RebuildSelectedRegion();
    }

    public void ToggleSelectedRegion()
    {
        if (!IsReady || !isActiveAndEnabled) return;
        selection.ToggleCurrent();
        RebuildSelectedRegion();
    }

    public bool SelectRegion(string id, bool show = true)
    {
        if (!IsReady || !isActiveAndEnabled || !selection.Select(id, show)) return false;
        RebuildSelectedRegion();
        return true;
    }

    public bool ShowSelectedRegion() { return SelectRegion(SelectedRegionId); }

    public void SelectNextRegion(int direction)
    {
        if (!IsReady || !isActiveAndEnabled) return;
        selection.Step(direction);
        RebuildSelectedRegion();
    }

    public void SelectNextAsterism(int direction)
    {
        if (!IsRegionVisible || direction == 0) return;
        int count = RegionAsterismIds[selection.SelectedIndex].Length;
        focusedAsterismIndex = (focusedAsterismIndex + (direction < 0 ? -1 : 1) + count) % count;
        ApplyFocus();
    }

    public bool TryFocus(Ray ray)
    {
        if (!IsRegionVisible || ray.direction.sqrMagnitude < 0.000001f) return false;
        Vector3 direction = ray.direction.normalized;
        float bestDot = Mathf.Cos(Mathf.Deg2Rad);
        int bestIndex = -1;
        var ids = RegionAsterismIds[selection.SelectedIndex];
        for (int i = 0; i < ids.Length; i++)
            foreach (string member in asterismsById[ids[i]].memberIds)
            {
                Vector3 offset = objectTransforms[member].position - ray.origin;
                if (Vector3.Dot(offset, direction) <= 0f) continue;
                float dot = Vector3.Dot(offset.normalized, direction);
                if (dot > bestDot || (bestIndex >= 0 && Mathf.Approximately(dot, bestDot) && i == focusedAsterismIndex))
                {
                    bestDot = dot;
                    bestIndex = i;
                }
            }
        if (bestIndex < 0) return false;
        if (bestIndex != focusedAsterismIndex)
        {
            focusedAsterismIndex = bestIndex;
            ApplyFocus();
        }
        return true;
    }

    private void RebuildSelectedRegion()
    {
        if (focusedRegionIndex != selection.SelectedIndex)
        {
            focusedRegionIndex = selection.SelectedIndex;
            focusedAsterismIndex = 0;
        }
        ClearDrawing();
        var region = selection.Current;
        Debug.Log($"Chinese region [{selection.SelectedIndex + 1}/{RegionCount}]: " +
            region.nameEn +
            (selection.IsVisible ? " (shown)" : " (hidden)"), this);
        if (!selection.IsVisible) return;
        foreach (string id in RegionAsterismIds[selection.SelectedIndex])
            DrawAsterism(asterismsById[id]);
        ApplyFocus();
        FaceViewer();
    }

    private void ApplyFocus()
    {
        if (focusMarkers != null) { focusMarkers.SetActive(false); Release(focusMarkers); }
        focusMarkers = null;
        for (int i = 0; i < activeHolders.Count; i++)
        {
            bool focused = i == focusedAsterismIndex;
            foreach (var line in activeHolders[i].GetComponentsInChildren<LineRenderer>(true))
            {
                Color colour = line.startColor;
                // The local shader blends each vertex's alpha; RGB and geometry are untouched.
                colour.a = (line.loop ? 1f : 0.95f) * (focused ? 1f : 0.22f);
                line.startColor = line.endColor = colour;
            }
            activeLabels[i].gameObject.SetActive(focused);
        }
        focusMarkers = new GameObject("ChineseFocusedMembers");
        focusMarkers.transform.SetParent(transform, false);
        foreach (string member in asterismsById[FocusedAsterismId].memberIds)
            DrawRing(focusMarkers, member, "FocusedStarMarker_", 3.2f, 0.2f, Color.white);
        FaceViewer();
    }

    private void DrawAsterism(ChineseAsterismDefinition definition)
    {
        var holder = new GameObject("ChineseAsterism_" + definition.id);
        holder.transform.SetParent(transform, false);
        activeHolders.Add(holder);
        foreach (var edge in definition.segments)
        {
            var line = NewLine(holder, edge.fromId + " -> " + edge.toId, 0.8f, new Color(1f, 0.78f, 0.3f, 0.95f));
            Vector3 a = LocalPosition(edge.fromId), b = LocalPosition(edge.toId);
            Vector3 trim = (b - a).normalized * Mathf.Min(3f, (b - a).magnitude * 0.2f);
            line.positionCount = 2;
            line.SetPosition(0, a + trim);
            line.SetPosition(1, b - trim);
            VisibleSegmentCount++;
        }
        foreach (string member in definition.memberIds)
        {
            bool cluster = objectDefinitions[member].kind == "open_cluster";
            if (cluster || definition.memberIds.Length == 1) DrawMarker(holder, member, cluster);
        }

        Vector3 centre = GetLocalCentre(definition);
        var labelObject = new GameObject("AsterismLabel");
        labelObject.transform.SetParent(holder.transform, false);
        labelObject.transform.localPosition = centre - centre.normalized * 8f - Vector3.up * 10f;
        var label = labelObject.AddComponent<TextMesh>();
        activeLabels.Add(label);
        label.font = labelFont;
        label.fontSize = 64;
        label.characterSize = 1.25f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = new Color(1f, 0.85f, 0.45f);
        label.text = GetDisplayName(definition);
        labelObject.GetComponent<MeshRenderer>().sharedMaterial = labelFont.material;
    }

    private LineRenderer NewLine(GameObject holder, string name, float width, Color colour)
    {
        var go = new GameObject(name);
        go.transform.SetParent(holder.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = colour;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void DrawMarker(GameObject holder, string id, bool cluster)
    {
        DrawRing(holder, id, cluster ? "ClusterMarker_" : "SingleStarMarker_", cluster ? 4.5f : 2.8f, 0.4f,
            cluster ? new Color(0.35f, 0.95f, 0.85f) : new Color(1f, 0.78f, 0.3f));
        VisibleMarkerCount++;
    }

    private void DrawRing(GameObject holder, string id, string prefix, float radius, float width, Color colour)
    {
        // A ring indicates selection; its radius is symbolic, not a cluster's measured extent.
        Vector3 position = LocalPosition(id);
        Vector3 normal = position.normalized;
        Vector3 right = Vector3.Cross(normal, Vector3.up);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(normal, Vector3.right);
        right.Normalize();
        Vector3 up = Vector3.Cross(normal, right).normalized;
        var line = NewLine(holder, prefix + id, width, colour);
        line.loop = true;
        line.positionCount = 32;
        for (int i = 0; i < 32; i++)
        {
            float angle = i * Mathf.PI * 2f / 32;
            line.SetPosition(i, position - normal * 3f + radius * (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)));
        }
    }

    public void HideRegion()
    {
        if (selection != null) selection.Hide();
        ClearDrawing();
    }

    private void ClearDrawing()
    {
        if (focusMarkers != null) { focusMarkers.SetActive(false); Release(focusMarkers); }
        focusMarkers = null;
        foreach (var holder in activeHolders)
            if (holder != null) { holder.SetActive(false); Release(holder); }
        activeHolders.Clear();
        activeLabels.Clear();
        VisibleSegmentCount = VisibleMarkerCount = 0;
    }

    private Vector3 LocalPosition(string id)
    {
        return transform.InverseTransformPoint(objectTransforms[id].position);
    }

    private Vector3 GetLocalCentre(ChineseAsterismDefinition definition)
    {
        Vector3 result = Vector3.zero;
        foreach (string id in definition.memberIds) result += LocalPosition(id);
        return result / definition.memberIds.Length;
    }

    private void LateUpdate() { FaceViewer(); }

    private void FaceViewer()
    {
        var viewer = Camera.main;
        if (viewer == null) return;
        foreach (var label in activeLabels)
        {
            if (label == null) continue;
            Vector3 direction = label.transform.position - viewer.transform.position;
            if (direction.sqrMagnitude > 0.001f)
                label.transform.rotation = Quaternion.LookRotation(direction, viewer.transform.up);
        }
    }

    private void CreateLabelFont()
    {
        var available = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
        string[] preferred = { "Microsoft YaHei", "Microsoft YaHei UI", "Yu Gothic", "Meiryo",
            "Noto Sans CJK SC", "Noto Sans CJK JP", "PingFang SC", "Arial Unicode MS", "Arial" };
        foreach (string family in preferred)
        {
            if (!available.Contains(family)) continue;
            labelFont = Font.CreateDynamicFontFromOSFont(family, 64);
            if (labelFont != null) break;
        }
        if (labelFont == null) labelFont = Font.CreateDynamicFontFromOSFont(new[] { "sans-serif", "Arial" }, 64);
        if (labelFont == null) throw new ArgumentException("No system font is available.");
    }

    private void OnDisable()
    {
        if (focusMarkers != null) focusMarkers.SetActive(false);
        foreach (var holder in activeHolders)
            if (holder != null) holder.SetActive(false);
        if (supplementalHolder != null) supplementalHolder.SetActive(false);
    }

    private void OnEnable()
    {
        if (focusMarkers != null) focusMarkers.SetActive(true);
        foreach (var holder in activeHolders)
            if (holder != null) holder.SetActive(true);
        if (supplementalHolder != null) supplementalHolder.SetActive(true);
    }

    private void OnDestroy()
    {
        HideRegion();
        Release(supplementalHolder);
        foreach (var material in supplementalMaterials) Release(material);
        Release(lineMaterial);
        Release(labelFont);
    }

    private static void Release(UnityEngine.Object item)
    {
        if (item == null) return;
        if (Application.isPlaying) Destroy(item);
        else DestroyImmediate(item);
    }
}
