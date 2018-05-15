using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
[ExecuteInEditMode]
public class ViewElementPool : MonoBehaviour {
	Canvas canvas;
	RectTransform canvasRectTransform;
	RectTransform rectTransform ;
	// Use this for initialization
	void Start () {
		canvas = (Canvas)FindObjectOfType(typeof(Canvas));
		canvasRectTransform = canvas.GetComponent<RectTransform>();
		rectTransform = GetComponent<RectTransform>();

		canvasRectTransform.ObserveEveryValueChanged(m=>m.sizeDelta).Subscribe(
			(s)=>{
				rectTransform.sizeDelta = new Vector2( s.x, s.y); 
			}
		);
	}
	
	
}
