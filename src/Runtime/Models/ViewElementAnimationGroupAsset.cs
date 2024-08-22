using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    public class ViewElementAnimationGroupAsset : ScriptableObject
    {
        public ViewElement targetViewElement;
        [SerializeField]
        public ViewElementAnimationGroup viewElementAnimationGroup;
    }

}