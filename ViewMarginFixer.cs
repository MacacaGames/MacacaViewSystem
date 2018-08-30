using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[ExecuteInEditMode]
public class ViewMarginFixer : MonoBehaviour
{
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
    void OnEnable()
    {
        rectTransform.offsetMin = new Vector2(margin.left, margin.bottom); // new Vector2(left, bottom); 
        rectTransform.offsetMax = new Vector2(-margin.right, -margin.top); // new Vector2(-right, -top);
    }

    [System.Serializable]
    public class Margin
    {
        public float top;
        public float bottom;
        public float right;
        public float left;
    }
}
