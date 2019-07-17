using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class HierarchyDrawer : Drawer
{
    Transform root;
    List<HierarchyData> hierarchyData = new List<HierarchyData>();
    public HierarchyDrawer(Transform root)
    {
        this.root = root;
        CacheItem(root, 0);
    }


    int padding = 20;
    public override void Draw()
    {
        base.Draw();
        using (var vertical = new GUILayout.VerticalScope())
        {
            foreach (var item in hierarchyData)
            {
                using (var horizon = new GUILayout.HorizontalScope())
                {
                    GUILayout.Space(item.layer * padding);
                    GUILayout.Label(prefabIcon, GUILayout.Width(20), GUILayout.Height(20));
                    if (GUILayout.Button(item.transform.name, labelStyle))
                    {
                        OnItemClick?.Invoke(item.transform.gameObject);
                    }
                    GUILayout.Label(arrowIcon, GUILayout.Width(20), GUILayout.Height(20));
                }
            }
        }
    }

    void CacheItem(Transform transform, int layer)
    {
        hierarchyData.Add(new HierarchyData(layer, transform));
        foreach (Transform item in transform)
        {
            CacheItem(item, layer + 1);
        }
    }

    class HierarchyData
    {
        public HierarchyData(int layer, Transform transform)
        {
            this.layer = layer;
            this.transform = transform;
        }
        public int layer;
        public Transform transform;
    }
    public delegate void _OnItemClick(Object target);

    public event _OnItemClick OnItemClick;
}
