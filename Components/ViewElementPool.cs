using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
[ExecuteInEditMode]
public class ViewElementPool : TransformCacheBase
{
	public bool EnableWidthAndHeightSyneInEditorMode = false;
    Canvas _canvas;
    Canvas canvas
    {
        get
        {
            if (_canvas == null)
            {
                _canvas = (Canvas)FindObjectOfType(typeof(Canvas));
            }
            return _canvas;
        }
    }
    RectTransform _canvasRectTransform;
    RectTransform canvasRectTransform
    {
        get
        {
            if (_canvasRectTransform == null)
            {
                _canvasRectTransform = canvas.GetComponent<RectTransform>();
            }
            return _canvasRectTransform;
        }
    }
    RectTransform _rectTransform;
    public RectTransform rectTransform
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
    // Use this for initialization
    void Start()
    {
        canvasRectTransform.ObserveEveryValueChanged(m => m.sizeDelta).Subscribe(
            (s) =>
            {
                rectTransform.sizeDelta = new Vector2(s.x, s.y);
            }
        );
    }
#if UNITY_EDITOR
    void OnGUI()
    {
        if(EnableWidthAndHeightSyneInEditorMode == true)
			rectTransform.sizeDelta = new Vector2(canvasRectTransform.sizeDelta.x, canvasRectTransform.sizeDelta.y);
    }

#endif
}
