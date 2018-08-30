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
        rectTransform.offsetMin = new Vector2(margin.modifyLeft ? margin.left : rectTransform.offsetMin.x, 
												margin.modifyBottom ? margin.bottom : rectTransform.offsetMin.y ); // new Vector2(left, bottom); 
        rectTransform.offsetMax = new Vector2(margin.modifyRight ? -margin.right : rectTransform.offsetMax.x,
											 margin.modifyTop ? -margin.top : rectTransform.offsetMax.y); // new Vector2(-right, -top);
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
