﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MacacaGames;
using UnityEngine.UI;
namespace MacacaGames.ViewSystem
{
    [ExecuteInEditMode]
    [System.Obsolete("ViewMarginFixer only required while using Custom Parent implement. Use RectTransform implement for more benfit")]
    public class ViewMarginFixer : ViewElementLifeCycle
    {
        [SerializeField]
        bool RebuildLayoutAfterFix = false;
        [SerializeField]
        Margin margin;

        RectTransform rectTransform
        {
            get
            {
                if (_rectTransform == null)
                {
                    _rectTransform = GetComponent<RectTransform>();
                }
                return _rectTransform;
            }
        }
        RectTransform _rectTransform;

        // Use this for initialization
        public override void OnBeforeShow()
        {
            ApplyModifyValue();
        }

        public void SetModifyValueFromRectTransform()
        {
            var anchor = _rectTransform.GetAnchorPresets();
            AutoGuessFixTarget(anchor);

            switch (anchor)
            {
                case (AnchorPresets.HorStretchTop):
                    {
                        margin.left = rectTransform.offsetMin.x;
                        margin.right = -rectTransform.offsetMax.x;
                        margin.top = 0;
                        margin.bottom = 0;
                        break;
                    }
                case (AnchorPresets.HorStretchMiddle):
                    {
                        margin.left = rectTransform.offsetMin.x;
                        margin.right = -rectTransform.offsetMax.x;
                        margin.top = 0;
                        margin.bottom = 0;
                        break;
                    }
                case (AnchorPresets.HorStretchBottom):
                    {
                        margin.left = rectTransform.offsetMin.x;
                        margin.right = -rectTransform.offsetMax.x;
                        margin.top = 0;
                        margin.bottom = 0;
                        break;
                    }

                case (AnchorPresets.VertStretchLeft):
                    {
                        margin.left = 0;
                        margin.right = 0;
                        margin.top = -rectTransform.offsetMax.y;
                        margin.bottom = rectTransform.offsetMin.y;
                        break;
                    }
                case (AnchorPresets.VertStretchCenter):
                    {
                        margin.left = 0;
                        margin.right = 0;
                        margin.top = -rectTransform.offsetMax.y;
                        margin.bottom = rectTransform.offsetMin.y;
                        break;
                    }
                case (AnchorPresets.VertStretchRight):
                    {
                        margin.left = 0;
                        margin.right = 0;
                        margin.top = -rectTransform.offsetMax.y;
                        margin.bottom = rectTransform.offsetMin.y;
                        break;
                    }

                case (AnchorPresets.StretchAll):
                    {
                        margin.left = rectTransform.offsetMin.x;
                        margin.right = -rectTransform.offsetMax.x;
                        margin.top = -rectTransform.offsetMax.y;
                        margin.bottom = rectTransform.offsetMin.y;

                        break;
                    }
            }
        }
        public void ApplyModifyValue()
        {
            rectTransform.offsetMin = new Vector2(margin.modifyLeft ? margin.left : rectTransform.offsetMin.x,
                                                    margin.modifyBottom ? margin.bottom : rectTransform.offsetMin.y); // new Vector2(left, bottom); 
            rectTransform.offsetMax = new Vector2(margin.modifyRight ? -margin.right : rectTransform.offsetMax.x,
                                                 margin.modifyTop ? -margin.top : rectTransform.offsetMax.y); // new Vector2(-right, -top);

            if (RebuildLayoutAfterFix == false && Application.isPlaying && gameObject.activeSelf)
                LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);

        }

        // IEnumerator ForceRefreshLayout()
        // {
        //     if (RebuildLayoutAfterFix == false) yield break;

        //     //LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        // }

        public void AutoGuessFixTarget(MacacaGames.AnchorPresets anchor = MacacaGames.AnchorPresets.UnKnown)
        {
            if (anchor == MacacaGames.AnchorPresets.UnKnown)
            {
                anchor = rectTransform.GetAnchorPresets();
            }
            //Min new Vector2(left, bottom); 
            //Max new Vector2(-right, -top);
            switch (anchor)
            {
                case (AnchorPresets.TopLeft):
                    {
                        // source.anchorMin = new Vector2(0, 1);
                        // source.anchorMax = new Vector2(0, 1);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }
                case (AnchorPresets.TopCenter):
                    {
                        // source.anchorMin = new Vector2(0.5f, 1);
                        // source.anchorMax = new Vector2(0.5f, 1);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }
                case (AnchorPresets.TopRight):
                    {
                        // source.anchorMin = new Vector2(1, 1);
                        // source.anchorMax = new Vector2(1, 1);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }

                case (AnchorPresets.MiddleLeft):
                    {
                        // source.anchorMin = new Vector2(0, 0.5f);
                        // source.anchorMax = new Vector2(0, 0.5f);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }
                case (AnchorPresets.MiddleCenter):
                    {
                        // source.anchorMin = new Vector2(0.5f, 0.5f);
                        // source.anchorMax = new Vector2(0.5f, 0.5f);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }
                case (AnchorPresets.MiddleRight):
                    {
                        // source.anchorMin = new Vector2(1, 0.5f);
                        // source.anchorMax = new Vector2(1, 0.5f);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }

                case (AnchorPresets.BottomLeft):
                    {
                        // source.anchorMin = new Vector2(0, 0);
                        // source.anchorMax = new Vector2(0, 0);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }
                case (AnchorPresets.BottonCenter):
                    {
                        // source.anchorMin = new Vector2(0.5f, 0);
                        // source.anchorMax = new Vector2(0.5f, 0);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }
                case (AnchorPresets.BottomRight):
                    {
                        // source.anchorMin = new Vector2(1, 0);
                        // source.anchorMax = new Vector2(1, 0);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }

                case (AnchorPresets.HorStretchTop):
                    {
                        // source.anchorMin = new Vector2(0, 1);
                        // source.anchorMax = new Vector2(1, 1);
                        margin.modifyLeft = true;
                        margin.modifyRight = true;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }
                case (AnchorPresets.HorStretchMiddle):
                    {
                        // source.anchorMin = new Vector2(0, 0.5f);
                        // source.anchorMax = new Vector2(1, 0.5f);
                        margin.modifyLeft = true;
                        margin.modifyRight = true;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }
                case (AnchorPresets.HorStretchBottom):
                    {
                        // source.anchorMin = new Vector2(0, 0);
                        // source.anchorMax = new Vector2(1, 0);
                        margin.modifyLeft = true;
                        margin.modifyRight = true;
                        margin.modifyTop = false;
                        margin.modifyBottom = false;
                        break;
                    }

                case (AnchorPresets.VertStretchLeft):
                    {
                        // source.anchorMin = new Vector2(0, 0);
                        // source.anchorMax = new Vector2(0, 1);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = true;
                        margin.modifyBottom = true;
                        break;
                    }
                case (AnchorPresets.VertStretchCenter):
                    {
                        // source.anchorMin = new Vector2(0.5f, 0);
                        // source.anchorMax = new Vector2(0.5f, 1);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = true;
                        margin.modifyBottom = true;
                        break;
                    }
                case (AnchorPresets.VertStretchRight):
                    {
                        // source.anchorMin = new Vector2(1, 0);
                        // source.anchorMax = new Vector2(1, 1);
                        margin.modifyLeft = false;
                        margin.modifyRight = false;
                        margin.modifyTop = true;
                        margin.modifyBottom = true;
                        break;
                    }

                case (AnchorPresets.StretchAll):
                    {
                        // source.anchorMin = new Vector2(0, 0);
                        // source.anchorMax = new Vector2(1, 1);
                        margin.modifyLeft = true;
                        margin.modifyRight = true;
                        margin.modifyTop = true;
                        margin.modifyBottom = true;
                        break;
                    }
            }
        }


        [System.Serializable]
        public class Margin
        {
            public bool modifyTop = true;
            public float top;
            public bool modifyBottom = true;
            public float bottom;
            public bool modifyRight = true;
            public float right;
            public bool modifyLeft = true;
            public float left;
        }
    }
}