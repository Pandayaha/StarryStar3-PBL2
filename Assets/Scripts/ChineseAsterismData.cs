using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ChineseAsterismCatalog
{
    public int schemaVersion;
    public string source, sourceUrl, retrievedDate, coordinateEpoch, license, attribution, adaptation;
    public ChineseSkyObject[] objects;
    public ChineseAsterismDefinition[] asterisms;
}

[Serializable]
public class ChineseSkyObject
{
    public string id, kind, positionSource, mappingSource, note;
    public int hr;
    public bool hasPosition, hasMagnitude;
    public double raRadians, decRadians;
    public float magnitude;
}

[Serializable]
public class ChineseAsterismDefinition
{
    public string id, sourceId, nameZh, nameEn, pronunciation, descriptionEn;
    public string[] memberIds;
    public ChineseAsterismSegment[] segments;
}

[Serializable]
public class ChineseAsterismSegment
{
    public string fromId, toId;
}

public static class ChineseAsterismData
{
    public static ChineseAsterismCatalog Parse(string json, ISet<int> availableHr)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("The Chinese asterism JSON is empty.");
        var catalog = JsonUtility.FromJson<ChineseAsterismCatalog>(json);
        if (catalog == null || catalog.schemaVersion != 2 || catalog.objects == null ||
            catalog.asterisms == null || catalog.asterisms.Length == 0)
            throw new ArgumentException("Expected Chinese asterism schema version 2.");

        var objects = new Dictionary<string, ChineseSkyObject>();
        foreach (var item in catalog.objects)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) ||
                objects.ContainsKey(item.id) || (item.kind != "star" && item.kind != "open_cluster") ||
                item.hr < 0)
                throw new ArgumentException("Invalid or duplicate sky-object definition.");

            bool existing = item.hr > 0 && availableHr != null && availableHr.Contains(item.hr);
            if (item.hasPosition && (!Finite(item.raRadians) || !Finite(item.decRadians) ||
                item.raRadians < 0 || item.raRadians >= Math.PI * 2 ||
                item.decRadians < -Math.PI / 2 || item.decRadians > Math.PI / 2))
                throw new ArgumentException("Invalid J2000 coordinates for " + item.id);
            if (!existing && !item.hasPosition)
                throw new ArgumentException("No existing HR or fallback position for " + item.id);
            if (item.kind == "open_cluster" && (item.hr != 0 || !item.hasPosition))
                throw new ArgumentException("A cluster requires an independent catalogue position.");
            if (item.hasMagnitude && (float.IsNaN(item.magnitude) || float.IsInfinity(item.magnitude)))
                throw new ArgumentException("Invalid magnitude for " + item.id);
            objects.Add(item.id, item);
        }

        var ids = new HashSet<string>();
        foreach (var group in catalog.asterisms)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.id) || !ids.Add(group.id) ||
                string.IsNullOrWhiteSpace(group.nameEn) || group.memberIds == null ||
                group.memberIds.Length == 0 || group.segments == null)
                throw new ArgumentException("Invalid or duplicate asterism definition.");
            var members = new HashSet<string>();
            foreach (string id in group.memberIds)
                if (id == null || !members.Add(id) || !objects.ContainsKey(id))
                    throw new ArgumentException("Unknown or duplicate member in " + group.id);
            foreach (var edge in group.segments)
                if (edge == null || edge.fromId == edge.toId ||
                    edge.fromId == null || edge.toId == null ||
                    !members.Contains(edge.fromId) || !members.Contains(edge.toId))
                    throw new ArgumentException("Invalid segment in " + group.id);
        }
        return catalog;
    }

    private static bool Finite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

}

// Small keyboard-selection state, independent of Unity rendering.
// The source array order is retained; labels use SelectedIndex + 1.
public sealed class ChineseAsterismSelection
{
    private readonly ChineseAsterismDefinition[] items;
    public int SelectedIndex { get; private set; }
    public bool IsVisible { get; private set; }
    public ChineseAsterismDefinition Current => items[SelectedIndex];

    public ChineseAsterismSelection(ChineseAsterismCatalog catalog, string defaultId = "beidou")
    {
        if (catalog == null || catalog.asterisms == null || catalog.asterisms.Length == 0)
            throw new ArgumentException("No asterisms to select.");
        items = catalog.asterisms;
        SelectedIndex = Math.Max(0, Array.FindIndex(items, item => item.id == defaultId));
    }

    public bool Select(string id, bool show = true)
    {
        int index = Array.FindIndex(items, item => item.id == id);
        if (index < 0) return false;
        SelectedIndex = index;
        IsVisible = show;
        return true;
    }

    public bool Toggle(string id)
    {
        int index = Array.FindIndex(items, item => item.id == id);
        if (index < 0) return false;
        IsVisible = index != SelectedIndex || !IsVisible;
        SelectedIndex = index;
        return true;
    }

    public void ToggleCurrent() { IsVisible = !IsVisible; }
    public void Hide() { IsVisible = false; }

    public void Step(int direction)
    {
        if (direction == 0) return;
        SelectedIndex = (SelectedIndex + (direction < 0 ? -1 : 1) + items.Length) % items.Length;
        IsVisible = true;
    }
}
