using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Colors specific GameObjects in the Unity Hierarchy based on keywords in their names.
/// Must be placed inside an "Editor" folder.
/// </summary>
[InitializeOnLoad]
public class HierarchyObjectColor
{
    private static Vector2 textOffset = new Vector2(20f, 1f);

    private static readonly Dictionary<string, Color> categoryColors = new Dictionary<string, Color>
    {
        { "MANAGERS", new Color(0.69f, 0.69f, 0.69f) },
        { "PLAYER",   new Color(0.035f, 0.047f, 0.608f) },
        { "MAP",      new Color(0.69f, 0.69f, 0.69f) },
        { "LIGHTING", new Color(0.035f, 0.047f, 0.608f) }
    };

    static HierarchyObjectColor()
    {
        EditorApplication.hierarchyWindowItemOnGUI += DrawHierarchyItem;
    }

    private static void DrawHierarchyItem(int instanceID, Rect selectionRect)
    {
        // ✅ Updated API (fixes obsolete warning)
        Object obj = EditorUtility.EntityIdToObject(instanceID);

        if (obj == null)
            return;

        string objectName = obj.name.ToUpper();

        foreach (var category in categoryColors)
        {
            if (objectName.Contains(category.Key))
            {
                DrawBackground(selectionRect, category.Value);
                DrawLabel(selectionRect, obj.name);
                break;
            }
        }
    }

    private static void DrawBackground(Rect rect, Color color)
    {
        Rect backgroundRect = new Rect(rect.x, rect.y, rect.width + 50f, rect.height);
        EditorGUI.DrawRect(backgroundRect, color);
    }

    private static void DrawLabel(Rect rect, string text)
    {
        Rect labelRect = new Rect(rect.position + textOffset, rect.size);

        GUIStyle style = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        EditorGUI.LabelField(labelRect, text, style);
    }
}