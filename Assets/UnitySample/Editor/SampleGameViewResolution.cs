using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMediaRecorder.Example
{
    // Mirrors standalone application resolution choices in the editor Game view.
    public static class SampleGameViewResolution
    {
        // Selects or adds a fixed Game view resolution without resizing the editor desktop window.
        public static void Apply(int width, int height)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Assembly assembly = typeof(Editor).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType", flags).GetValue(sizes);
            object group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { groupType });
            Type groupClass = group.GetType();
            int count = (int)groupClass.GetMethod("GetTotalCount", flags).Invoke(group, null);
            int index = -1;
            for (int i = 0; i < count; i++)
            {
                object size = groupClass.GetMethod("GetGameViewSize", flags).Invoke(group, new object[] { i });
                Type sizeType = size.GetType();
                if ((int)sizeType.GetProperty("width", flags).GetValue(size) == width &&
                    (int)sizeType.GetProperty("height", flags).GetValue(size) == height) { index = i; break; }
            }
            if (index < 0)
            {
                Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
                Type kind = assembly.GetType("UnityEditor.GameViewSizeType");
                object size = Activator.CreateInstance(sizeType, flags, null,
                    new object[] { Enum.Parse(kind, "FixedResolution"), width, height, "UnitySample" }, null);
                groupClass.GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
                index = count;
            }
            Type gameViewType = assembly.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(gameViewType);
            gameViewType.GetProperty("selectedSizeIndex", flags).SetValue(view, index);
            view.Repaint();
        }
    }
}
