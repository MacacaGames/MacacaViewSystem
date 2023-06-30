using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MacacaGames;
using UnityEngine.UI;

namespace MacacaGames.ViewSystem
{
    [ExecuteInEditMode]
    public class ViewSystemLayoutRebuilder : MonoBehaviour, IViewElementLifeCycle
    {

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
        public void OnBeforeShow()
        {
        }

        public void OnBeforeLeave()
        {
        }

        public void OnStartShow()
        {
            StartCoroutine(RebuildLayout());
        }

        IEnumerator RebuildLayout()
        {
            yield return null;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        public void OnStartLeave()
        {
        }

        public void OnChangePage(bool show)
        {
        }

        public void OnChangedPage()
        {
        }
        public void RefreshView() { }
    }
}