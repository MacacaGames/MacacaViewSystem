using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
 
public static class EditorExtensionMethods
{
    public static IEnumerable<SerializedProperty> GetChildren(this SerializedProperty property)
    {
        property = property.Copy();
        var nextElement = property.Copy();
        bool hasNextElement = nextElement.NextVisible(false);
        if (!hasNextElement)
        {
            nextElement = null;
        }
 
        property.NextVisible(true);
        while (true)
        {
            if ((SerializedProperty.EqualContents(property, nextElement)))
            {
                yield break;
            }
 
            yield return property;
 
            bool hasNext = property.NextVisible(false);
            if (!hasNext)
            {
                break;
            }
        }
    }
}
 