using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class ComponentDrawer : Drawer
{
    GameObject go;
    public ComponentDrawer(GameObject go)
    {
        this.go = go;
        CacheItem(go, 0);
    }
    public override void Draw()
    {
        base.Draw();
        using (var vertical = new GUILayout.VerticalScope())
        {
            foreach (var item in serializedObjects)
            {
                using (var horizon = new GUILayout.HorizontalScope())
                {
                    var _cachedContent = new GUIContent(EditorGUIUtility.ObjectContent(item.targetObject, item.targetObject.GetType()));
                    GUILayout.Label(_cachedContent.image, GUILayout.Width(20), GUILayout.Height(20));

                    if (item.targetObject as Component == null)
                    {
                        _cachedContent.text = "GameObject Properties";
                    }
                    else
                    {
                        _cachedContent.text = item.targetObject.GetType().Name;
                    }

                    if (GUILayout.Button(_cachedContent.text, labelStyle))
                    {
                        OnItemClick?.Invoke(item);
                    }
                    GUILayout.Label(arrowIcon, GUILayout.Width(20), GUILayout.Height(20));
                }
            }
        }
    }
    List<SerializedObject> serializedObjects = new List<SerializedObject>();
    void CacheItem(GameObject go, int layer)
    {
        var components = go.GetComponents(typeof(Component));
        foreach (var item in components)
        {
            serializedObjects.Add(new SerializedObject(item));
        }
    }
    public delegate void _OnItemClick(SerializedObject target);
    public event _OnItemClick OnItemClick;

}
