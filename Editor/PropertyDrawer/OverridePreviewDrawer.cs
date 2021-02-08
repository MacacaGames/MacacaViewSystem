using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    [CustomPropertyDrawer(typeof(OverridePreviewAttribute))]
    public class OverridePreviewDrawer : DecoratorDrawer
    {
        public override void OnGUI(Rect position)
        {
            if(GUILayout.Button("Preview")){

            }
        }
    }
}