using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
namespace CloudMacaca.ViewSystem
{
    public class OverridePopupChecker : EditorWindow
    {
        Transform root;
        ViewPageItem viewPageItem;
        PropertyModification[] propertyModification;
        public void SetData(Transform root, ViewPageItem viewPageItem)
        {
            this.root = root;
            this.viewPageItem = viewPageItem;

            propertyModification = PrefabUtility.GetPropertyModifications(root.gameObject)
                .Where(x => !PrefabUtility.IsDefaultOverride(x))
                .ToArray();
            foreach (var item in propertyModification)
            {
                Debug.Log("____propertyPath: " + item.propertyPath);
                Debug.Log("____target " + item.target);
                Debug.Log("____value " + item.value);
                Debug.Log("____objectReference " + item.objectReference);
            }
        }
    }

}
