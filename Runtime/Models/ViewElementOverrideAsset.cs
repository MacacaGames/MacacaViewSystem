using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    public class ViewElementOverrideAsset : ScriptableObject
    {
        public ViewElement targetViewElement;
        public List<ViewElementPropertyOverrideData> viewElementOverride;
    }

}