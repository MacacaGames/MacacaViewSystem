using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_SkewedImage : Image
{
    public float skewX;
    public float skewY;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        base.OnPopulateMesh(vh);


        var height = rectTransform.rect.height;
        var width = rectTransform.rect.width;
        var xskew = height * Mathf.Tan(Mathf.Deg2Rad * skewX);
        var yskew = width * Mathf.Tan(Mathf.Deg2Rad * skewY);

        var ymin = rectTransform.rect.yMin;
        var xmin = rectTransform.rect.xMin;
        UIVertex v = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            v.position += new Vector3(Mathf.Lerp(0, xskew, (v.position.y - ymin) / height), Mathf.Lerp(0, yskew, (v.position.x - xmin) / width), 0);
            vh.SetUIVertex(v, i);
        }

    }
}