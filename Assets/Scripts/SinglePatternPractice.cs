using System;
using System.Collections.Generic;
using UnityEngine;

// Uses the same scene stars and source connections as the visible Chinese and Western figures.
public class SinglePatternPractice : MonoBehaviour
{
    private sealed class Figure
    {
        internal string Id, Name;
        internal int Region = -1, WesternIndex = -1;
        internal Transform[] Members;
        internal readonly HashSet<(int, int)> Edges = new HashSet<(int, int)>();
    }

    private readonly List<Figure> figures = new List<Figure>();
    private readonly List<Figure> candidates = new List<Figure>();
    private readonly List<(int, int)> drawn = new List<(int, int)>();
    private ChineseAsterismController chinese;
    private StarField field;
    private Figure selected;
    private Transform pickedStar;
    private GameObject overlay;
    private int pending = -1;

    public bool HasSelection => selected != null;
    public bool SelectedIsChinese => HasSelection && selected.Region >= 0;
    public string SelectedId => selected?.Id;
    public string SelectedName => selected?.Name;
    public string CultureLabel => SelectedIsChinese ? "Chinese asterism" : "Western constellation";
    public int CandidateIndex => candidates.IndexOf(selected);
    public int CandidateCount => candidates.Count;
    public int MemberCount => selected?.Members.Length ?? 0;
    public int LineCount => selected?.Edges.Count ?? 0;
    public int DrawnLineCount => drawn.Count;
    public bool CanDraw => LineCount > 0;
    public bool HasPendingStart => pending >= 0;
    public bool IsDrawing { get; private set; }
    public bool AnswerVisible { get; private set; }
    public string Feedback { get; private set; } = string.Empty;
    public bool FeedbackCorrect { get; private set; }
    public Vector3 SelectedCentre
    {
        get
        {
            if (!HasSelection) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var star in selected.Members) sum += star.position;
            return sum / MemberCount;
        }
    }

    internal void Initialize(StarField owner, ChineseAsterismController catalogue,
        List<GameObject> starObjects, IReadOnlyList<(int[], int[])> western)
    {
        if (figures.Count != 0 || !catalogue.IsReady) return;
        field = owner;
        chinese = catalogue;
        for (int region = 0; region < chinese.RegionCount; region++)
            foreach (string id in chinese.RegionPatternIds(region))
            {
                if (!chinese.TryGetAsterism(id, out var definition)) continue;
                var figure = new Figure { Id = id, Name = ChineseAsterismController.GetDisplayName(definition),
                    Region = region, Members = Array.ConvertAll(definition.memberIds, chinese.GetMemberTransform) };
                foreach (var edge in definition.segments)
                    figure.Edges.Add(Edge(Array.IndexOf(definition.memberIds, edge.fromId),
                        Array.IndexOf(definition.memberIds, edge.toId)));
                figures.Add(figure);
            }
        for (int i = 0; i < western.Count; i++)
        {
            var (members, connections) = western[i];
            var name = field.GetWesternPatternName(i);
            var figure = new Figure { Id = name, Name = name, WesternIndex = i,
                Members = Array.ConvertAll(members, hr => starObjects[hr - 1].transform) };
            for (int j = 0; j < connections.Length; j += 2)
                figure.Edges.Add(Edge(Array.IndexOf(members, connections[j]), Array.IndexOf(members, connections[j + 1])));
            figures.Add(figure);
        }
    }

    private static (int, int) Edge(int a, int b) => a < b ? (a, b) : (b, a);

    private bool IsVisible(Figure figure)
    {
        return figure.Region >= 0 ? chinese.IsRegionVisible && chinese.SelectedRegionIndex == figure.Region :
            field.IsWesternPatternVisible(figure.WesternIndex);
    }

    public bool TryPickBrowseStar(Ray ray, out Transform star)
    {
        star = null;
        if (field == null || IsDrawing || field.IsComparisonActive || ray.direction.sqrMagnitude < 0.000001f) return false;
        Vector3 direction = ray.direction.normalized;
        float best = PickLimit;
        foreach (var figure in figures)
            if (IsVisible(figure)) PickNearest(figure, ray.origin, direction, ref best, ref star);
        return star != null;
    }

    // Squared angular separation keeps even closely spaced catalogue stars independently selectable.
    private static readonly float PickLimit = Mathf.Pow(Mathf.Sin(Mathf.Deg2Rad), 2f);

    private static void PickNearest(Figure figure, Vector3 origin, Vector3 direction, ref float best, ref Transform match)
    {
        foreach (Transform star in figure.Members)
        {
            Vector3 offset = star.position - origin;
            if (Vector3.Dot(offset, direction) <= 0f) continue;
            float distance = Vector3.Cross(direction, offset).sqrMagnitude / offset.sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            match = star;
        }
    }

    public bool SelectBrowseStar(Transform star)
    {
        if (field == null || IsDrawing || field.IsComparisonActive || star == null) return false;
        CollectCandidates(star);
        if (candidates.Count == 0) { ClearSelection(); return false; }
        pickedStar = star;
        var choice = selected;
        if (choice == null || !candidates.Contains(choice))
        {
            choice = candidates[0];
            foreach (var candidate in candidates)
                if (candidate.Region >= 0 && candidate.Id == chinese.FocusedAsterismId) choice = candidate;
        }
        SelectFigure(choice);
        return true;
    }

    private void CollectCandidates(Transform star)
    {
        candidates.Clear();
        foreach (var figure in figures)
            if (IsVisible(figure) && Array.IndexOf(figure.Members, star) >= 0)
                candidates.Add(figure);
    }

    public void RefreshSelection()
    {
        if (!HasSelection || IsDrawing) return;
        if (field.IsComparisonActive || !IsVisible(selected) ||
            (SelectedIsChinese && chinese.FocusedAsterismId != selected.Id))
        {
            ClearSelection();
            return;
        }
        CollectCandidates(pickedStar);
    }

    public void CycleCandidate(int direction)
    {
        RefreshSelection();
        if (IsDrawing || candidates.Count < 2 || direction == 0) return;
        int index = (CandidateIndex + Math.Sign(direction) + candidates.Count) % candidates.Count;
        SelectFigure(candidates[index]);
    }

    private void SelectFigure(Figure figure)
    {
        selected = figure;
        if (SelectedIsChinese) chinese.FocusPattern(figure.Id);
        Redraw();
    }

    public void ClearSelection()
    {
        if (IsDrawing) return;
        selected = null;
        pickedStar = null;
        candidates.Clear();
        ClearOverlay();
    }

    public bool StartDrawing()
    {
        RefreshSelection();
        if (IsDrawing || !CanDraw) return false;
        IsDrawing = true;
        field.SuspendBrowseFigures(true);
        Restart();
        return true;
    }

    public bool TryPickDrawingStar(Ray ray, out Transform star)
    {
        star = null;
        if (!IsDrawing || AnswerVisible || ray.direction.sqrMagnitude < 0.000001f) return false;
        float best = PickLimit;
        PickNearest(selected, ray.origin, ray.direction.normalized, ref best, ref star);
        return star != null;
    }

    public void SelectDrawingStar(Transform star)
    {
        if (!IsDrawing || AnswerVisible) return;
        int index = Array.IndexOf(selected.Members, star);
        if (index < 0) return;
        ClearFeedback();
        if (pending < 0) pending = index;
        else
        {
            var edge = Edge(pending, index);
            if (pending != index && !drawn.Contains(edge)) drawn.Add(edge);
            pending = -1;
        }
        Redraw();
    }

    public void CancelStart()
    {
        if (!IsDrawing || !HasPendingStart) return;
        pending = -1;
        Redraw();
    }

    public void Undo()
    {
        if (!IsDrawing || AnswerVisible) return;
        pending = -1;
        if (drawn.Count > 0) drawn.RemoveAt(drawn.Count - 1);
        ClearFeedback();
        Redraw();
    }

    public void Restart()
    {
        if (!IsDrawing) return;
        drawn.Clear();
        pending = -1;
        AnswerVisible = false;
        ClearFeedback();
        Redraw();
    }

    public void Finish()
    {
        if (!IsDrawing || AnswerVisible) return;
        pending = -1;
        FeedbackCorrect = selected.Edges.SetEquals(drawn);
        Feedback = FeedbackCorrect ? "Correct! Pattern completed." : "Not matched yet. Keep drawing or undo a line.";
        Redraw();
    }

    public void ToggleAnswer()
    {
        if (!IsDrawing) return;
        pending = -1;
        AnswerVisible = !AnswerVisible;
        ClearFeedback();
        Redraw();
    }

    private void ClearFeedback()
    {
        Feedback = string.Empty;
        FeedbackCorrect = false;
    }

    public void ExitDrawing()
    {
        if (!IsDrawing) return;
        IsDrawing = false;
        AnswerVisible = false;
        pending = -1;
        drawn.Clear();
        ClearFeedback();
        field.RestoreBrowseFigures();
        ClearSelection();
    }

    private void Redraw()
    {
        ClearOverlay();
        // The ordinary Chinese focus already supplies the member rings and highlighted lines.
        if (!HasSelection || (!IsDrawing && SelectedIsChinese)) return;
        overlay = new GameObject(IsDrawing ? "SinglePatternDrawing" : "SelectedWesternPattern");
        overlay.transform.SetParent(transform, false);
        foreach (Transform star in selected.Members)
            chinese.DrawPatternRing(overlay, "Member_" + star.name, star, 3.2f, 0.24f, Color.white);
        bool standard = !IsDrawing || AnswerVisible;
        var colour = SelectedIsChinese ? new Color(1f, 0.78f, 0.3f) : new Color(0.3f, 0.65f, 1f);
        IEnumerable<(int, int)> edges = standard ? (IEnumerable<(int, int)>)selected.Edges : drawn;
        foreach (var (from, to) in edges)
            chinese.DrawPatternEdge(overlay, standard ? "PatternEdge" : "PlayerEdge",
                selected.Members[from], selected.Members[to], 1.15f, colour);
        if (HasPendingStart)
            chinese.DrawPatternRing(overlay, "Start", selected.Members[pending], 4.6f, 0.45f, colour);
    }

    private void ClearOverlay()
    {
        if (overlay == null) return;
        overlay.SetActive(false);
        if (Application.isPlaying) Destroy(overlay);
        else DestroyImmediate(overlay);
        overlay = null;
    }

    private void OnDisable()
    {
        if (IsDrawing) ExitDrawing();
        else ClearSelection();
    }
}
