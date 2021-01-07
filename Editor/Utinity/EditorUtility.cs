using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
namespace MacacaGames.ViewSystem
{
    public class VS_EditorUtility
    {
        public static Rect CalculateBoundsRectFromRects(IEnumerable<Rect> rects, Vector2 padding)
        {
            float xMin = 9999999f, xMax = -999999, yMin = 9999999f, yMax = -999999;
            foreach (var item in rects)
            {
                xMin = Mathf.Min(item.xMin, xMin);
                xMax = Mathf.Max(item.xMax, xMax);
                yMin = Mathf.Min(item.yMin, yMin);
                yMax = Mathf.Max(item.yMax, yMax);
            }

            Rect result = new Rect(new Vector2(xMin - padding.x, yMin - padding.y), new Vector2(xMax - xMin + padding.x * 2, yMax - yMin + padding.y * 2));
            return result;
        }
        public static bool IsPropertyNeedIgnore(SerializedProperty prop)
        {
            return prop.name == "m_Script" ||
                prop.name == "m_Name" ||
                prop.propertyType == SerializedPropertyType.LayerMask ||
                prop.propertyType == SerializedPropertyType.Rect ||
                prop.propertyType == SerializedPropertyType.RectInt ||
                prop.propertyType == SerializedPropertyType.Bounds ||
                prop.propertyType == SerializedPropertyType.BoundsInt ||
                prop.propertyType == SerializedPropertyType.Quaternion ||
                prop.propertyType == SerializedPropertyType.Vector2Int ||
                prop.propertyType == SerializedPropertyType.Vector3Int ||
                prop.propertyType == SerializedPropertyType.Vector4 ||
                prop.propertyType == SerializedPropertyType.Gradient ||
                prop.propertyType == SerializedPropertyType.ArraySize ||
                prop.propertyType == SerializedPropertyType.AnimationCurve ||
                prop.propertyType == SerializedPropertyType.Character ||
                prop.propertyType == SerializedPropertyType.FixedBufferSize;
        }

        static BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public static Type GetPropertyType(SerializedProperty property)
        {
            System.Type parentType = property.serializedObject.targetObject.GetType();
            System.Reflection.FieldInfo fi = parentType.GetField(property.propertyPath, flags);

            if (fi != null)
            {
                return fi.FieldType;
            }
            string p = property.propertyPath;
            if (parentType.ToString().Contains("UnityEngine."))
            {
                p = ViewSystemUtilitys.ParseUnityEngineProperty(property.propertyPath);
            }
            System.Reflection.PropertyInfo pi = parentType.GetProperty(p, flags);
            if (pi != null)
            {
                return pi.PropertyType;
            }

            return MacacaGames.Utility.GetType(property.type) ?? typeof(UnityEngine.Object);
        }

        public static bool EditorableField(Rect rect, SerializedProperty Target, PropertyOverride overProperty, out float lineHeight)
        {
            lineHeight = EditorGUIUtility.singleLineHeight * 2.5f;
            if (Target == null || overProperty == null)
            {
                GUI.Label(rect, "There is some property wrong on the override");
                return false;
            }
            GUIContent content = new GUIContent(Target?.displayName);
            EditorGUI.BeginChangeCheck();
            switch (Target.propertyType)
            {
                case SerializedPropertyType.Vector3:
                    lineHeight = EditorGUIUtility.singleLineHeight * 3.5f;
                    overProperty.SetValue(EditorGUI.Vector3Field(rect, content, (Vector3)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.Vector2:
                    lineHeight = EditorGUIUtility.singleLineHeight * 3.5f;
                    overProperty.SetValue(EditorGUI.Vector2Field(rect, content, (Vector2)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.Float:
                    overProperty.SetValue(EditorGUI.FloatField(rect, content, (float)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.Integer:
                    overProperty.SetValue(EditorGUI.IntField(rect, content, (int)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.String:
                    overProperty.StringValue = EditorGUI.TextField(rect, content, overProperty.StringValue);
                    break;
                case SerializedPropertyType.Boolean:
                    overProperty.SetValue(EditorGUI.Toggle(rect, content, (bool)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.Color:
                    overProperty.SetValue(EditorGUI.ColorField(rect, content, (Color)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.ObjectReference:
                    overProperty.ObjectReferenceValue = EditorGUI.ObjectField(rect, content, overProperty.ObjectReferenceValue, GetPropertyType(Target), false);
                    break;
                case SerializedPropertyType.Enum:
                    bool isFlag = false;
                    var _enumValue = (Enum)overProperty.GetValue();
                    isFlag = _enumValue.GetType().GetCustomAttribute(typeof(System.FlagsAttribute)) != null;
                    if (isFlag)
                    {
                        overProperty.SetValue(EditorGUI.EnumFlagsField(rect, content, (Enum)overProperty.GetValue()));
                    }
                    else
                    {
                        overProperty.SetValue(EditorGUI.EnumPopup(rect, content, (Enum)overProperty.GetValue()));
                    }
                    break;
            }
            return EditorGUI.EndChangeCheck();
        }

        public class ViewPageItemsBreakPointPopup : UnityEditor.PopupWindowContent
        {
            ViewPageItem viewPageItem;
            Vector2 scrollPos;
            System.Action<ViewElementTransform, string, string, ViewElement, Rect> OnDrawItem;
            public ViewPageItemsBreakPointPopup(ViewPageItem viewPageItem, System.Action<ViewElementTransform, string, string, ViewElement, Rect> OnDrawItem)
            {
                this.viewPageItem = viewPageItem;
                this.OnDrawItem = OnDrawItem;
                reorderableList = null;
                reorderableList = new ReorderableList(viewPageItem.breakPointViewElementTransforms, typeof(List<BreakPointViewElementTransform>), true, true, true, false);
                reorderableList.drawElementCallback += DrawViewItemElement;
                reorderableList.elementHeight = EditorGUIUtility.singleLineHeight * 6f;
                reorderableList.onAddCallback += AddItem;
                reorderableList.elementHeightCallback += ElementHight;
            }

            private void AddItem(ReorderableList list)
            {
                // this is ugly but there are a lot of cases like null types and default constructors
                var elementType = list.list.GetType().GetElementType();
                if (elementType == typeof(string))
                    list.index = list.list.Add("");
                else if (elementType != null && elementType.GetConstructor(Type.EmptyTypes) == null)
                    Debug.LogError("Cannot add element. Type " + elementType + " has no default constructor. Implement a default constructor or implement your own add behaviour.");
                else if (list.list.GetType().GetGenericArguments()[0] != null)
                    list.index = list.list.Add(Activator.CreateInstance(list.list.GetType().GetGenericArguments()[0]));
                else if (elementType != null)
                    list.index = list.list.Add(Activator.CreateInstance(elementType));
                else
                    Debug.LogError("Cannot add element of type Null.");
            }

            private void DrawViewItemElement(Rect rect, int index, bool isActive, bool isFocused)
            {
                rect.height = EditorGUIUtility.singleLineHeight;
                var item = viewPageItem.breakPointViewElementTransforms[index];
                item.breakPointName = EditorGUI.TextField(rect, item.breakPointName);
                rect.y += EditorGUIUtility.singleLineHeight;
                OnDrawItem?.Invoke(item.transformData, viewPageItem.Id, item.breakPointName, viewPageItem.previewViewElement, rect);
            }

            float ElementHight(int index)
            {
                return GetHeight();
                float GetHeight()
                {
                    return EditorGUIUtility.singleLineHeight * 7.5f + (EditorGUIUtility.singleLineHeight * 3 + 6) + (EditorGUIUtility.singleLineHeight * 2 + 8);
                }
                // with folddot feature
                // float GetHeight()
                // {
                //     return EditorGUIUtility.singleLineHeight * 7.5f +
                //    (anchorPivotFoldout[index] ? EditorGUIUtility.singleLineHeight * 3 + 6 : 0) +
                //    (rotationScaleFoldout[index] ? EditorGUIUtility.singleLineHeight * 2 + 8 : 0);
                // }
            }
            public override Vector2 GetWindowSize()
            {
                return new Vector2(300, 600);
            }

            ReorderableList reorderableList;
            float height = EditorGUIUtility.singleLineHeight * 7.5f + (EditorGUIUtility.singleLineHeight * 3 + 6) + (EditorGUIUtility.singleLineHeight * 2 + 8);
            public override void OnGUI(Rect rect)
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos))
                {
                    reorderableList.DoLayoutList();
                    //                    OnDrawItem?.Invoke(item.transformData, viewPageItem.Id, item.breakPointName, viewPageItem.previewViewElement, new Rect());

                    scrollPos = scroll.scrollPosition;
                }
            }
        }


        public class ViewPageItemDetailPopup : UnityEditor.PopupWindowContent
        {
            ViewPageItem viewPageItem;
            Rect rect;
            public ViewPageItemDetailPopup(Rect rect, ViewPageItem viewPageItem)
            {
                this.viewPageItem = viewPageItem;
                this.rect = rect;
            }
            public override Vector2 GetWindowSize()
            {
                return new Vector2(rect.width, EditorGUIUtility.singleLineHeight * 5);
            }

            GUIStyle _toggleStyle;
            GUIStyle toggleStyle
            {
                get
                {
                    if (_toggleStyle == null)
                    {
                        _toggleStyle = new GUIStyle
                        {
                            normal = {
                                background = CMEditorUtility.CreatePixelTexture("_toggleStyle_on",new Color32(64,64,64,255)),
                                textColor = Color.gray
                            },
                            onNormal = {
                                    background = CMEditorUtility.CreatePixelTexture("_toggleStyle",new Color32(128,128,128,255)),

                                 textColor = Color.white
                            },

                            alignment = TextAnchor.MiddleCenter,
                            clipping = TextClipping.Clip,
                            imagePosition = ImagePosition.TextOnly,
                            stretchHeight = true,
                            stretchWidth = true,
                            padding = new RectOffset(0, 0, 0, 0),
                            margin = new RectOffset(0, 0, 0, 0)
                        };
                    }
                    return _toggleStyle;
                }
            }

            public override void OnGUI(Rect rect)
            {
                viewPageItem.easeType = (EaseStyle)EditorGUILayout.EnumPopup(new GUIContent("Ease", "The EaseType when needs to tween."), viewPageItem.easeType);

                viewPageItem.TweenTime = EditorGUILayout.Slider(new GUIContent("Tween Time", "Tween Time use to control when ViewElement needs change parent."), viewPageItem.TweenTime, -1, 1);

                viewPageItem.delayIn = EditorGUILayout.Slider("Delay In", viewPageItem.delayIn, 0, 1);

                // viewPageItem.delayOut = EditorGUILayout.Slider("Delay Out", viewPageItem.delayOut, 0, 1);
                //viewPageItem.excludePlatform = (ViewPageItem.PlatformOption)EditorGUILayout.EnumFlagsField(new GUIContent("Excude Platform", "Excude Platform define the platform which wish to show the ViewPageItem or not"), viewPageItem.excludePlatform);
                CMEditorLayout.BitMaskField(ref viewPageItem.excludePlatform);
            }
        }

        public static void SetAnchorSmart(ViewSystemRectTransformData vs_rect, RectTransform rect, float value, int axis, bool isMax, bool smart)
        {
            SetAnchorSmart(vs_rect, rect, value, axis, isMax, smart, false, false, false);
        }

        public static void SetAnchorSmart(ViewSystemRectTransformData vs_rect, RectTransform rect, float value, int axis, bool isMax, bool smart, bool enforceExactValue)
        {
            SetAnchorSmart(vs_rect, rect, value, axis, isMax, smart, enforceExactValue, false, false);
        }

        public static void SetAnchorSmart(ViewSystemRectTransformData vs_rect, RectTransform rect, float value, int axis, bool isMax, bool smart, bool enforceExactValue, bool enforceMinNoLargerThanMax, bool moveTogether)
        {
            RectTransform parent = null;
            if (rect == null)
            {
                smart = false;
            }
            else if (rect.transform.parent == null)
            {
                smart = false;
            }
            else
            {
                parent = rect.transform.parent.GetComponent<RectTransform>();
                if (parent == null)
                    smart = false;
            }

            bool clampToParent = !AnchorAllowedOutsideParent(axis, isMax ? 1 : 0);
            if (clampToParent)
                value = Mathf.Clamp01(value);
            if (enforceMinNoLargerThanMax)
            {
                if (isMax)
                    value = Mathf.Max(value, vs_rect.anchorMin[axis]);
                else
                    value = Mathf.Min(value, vs_rect.anchorMax[axis]);
            }

            float offsetSizePixels = 0;
            float offsetPositionPixels = 0;
            if (smart)
            {
                float oldValue = isMax ? vs_rect.anchorMax[axis] : vs_rect.anchorMin[axis];

                offsetSizePixels = (value - oldValue) * parent.rect.size[axis];

                // Ensure offset is in whole pixels.
                // Note: In this particular instance we want to use Mathf.Round (which rounds towards nearest even number)
                // instead of Round from this class which always rounds down.
                // This makes the position of rect more stable when their anchors are changed.
                float roundingDelta = 0;
                if (ShouldDoIntSnapping(rect))
                    roundingDelta = Mathf.Round(offsetSizePixels) - offsetSizePixels;
                offsetSizePixels += roundingDelta;

                if (!enforceExactValue)
                {
                    value += roundingDelta / parent.rect.size[axis];

                    // Snap value to whole percent if close
                    if (Mathf.Abs(Round(value * 1000) - value * 1000) < 0.1f)
                        value = Round(value * 1000) * 0.001f;

                    if (clampToParent)
                        value = Mathf.Clamp01(value);
                    if (enforceMinNoLargerThanMax)
                    {
                        if (isMax)
                            value = Mathf.Max(value, vs_rect.anchorMin[axis]);
                        else
                            value = Mathf.Min(value, vs_rect.anchorMax[axis]);
                    }
                }

                if (moveTogether)
                    offsetPositionPixels = offsetSizePixels;
                else
                    offsetPositionPixels = (isMax ? offsetSizePixels * vs_rect.pivot[axis] : (offsetSizePixels * (1 - vs_rect.pivot[axis])));
            }

            if (isMax)
            {
                Vector2 rectAnchorMax = vs_rect.anchorMax;
                rectAnchorMax[axis] = value;
                vs_rect.anchorMax = rectAnchorMax;

                Vector2 other = vs_rect.anchorMin;
                if (moveTogether)
                    other[axis] = s_StartDragAnchorMin[axis] + rectAnchorMax[axis] - s_StartDragAnchorMax[axis];
                vs_rect.anchorMin = other;
            }
            else
            {
                Vector2 rectAnchorMin = vs_rect.anchorMin;
                rectAnchorMin[axis] = value;
                vs_rect.anchorMin = rectAnchorMin;

                Vector2 other = vs_rect.anchorMax;
                if (moveTogether)
                    other[axis] = s_StartDragAnchorMax[axis] + rectAnchorMin[axis] - s_StartDragAnchorMin[axis];
                vs_rect.anchorMax = other;
            }

            if (smart)
            {
                Vector2 rectPosition = vs_rect.anchoredPosition;
                rectPosition[axis] -= offsetPositionPixels;
                vs_rect.anchoredPosition = rectPosition;

                if (!moveTogether)
                {
                    Vector2 rectSizeDelta = vs_rect.sizeDelta;
                    rectSizeDelta[axis] += offsetSizePixels * (isMax ? -1 : 1);
                    vs_rect.sizeDelta = rectSizeDelta;
                }
            }
        }
        static float Round(float value) { return Mathf.Floor(0.5f + value); }
        static int RoundToInt(float value) { return Mathf.FloorToInt(0.5f + value); }
        private static Vector2 s_StartDragAnchorMin;
        private static Vector2 s_StartDragAnchorMax;

        static bool AnchorAllowedOutsideParent(int axis, int minmax)
        {
            // Allow dragging outside if action key is held down (same key that disables snapping).
            // Also allow when not dragging at all - for e.g. typing values into the Inspector.
            if (EditorGUI.actionKey || EditorGUIUtility.hotControl == 0)
                return true;
            // Also allow if drag started outside of range to begin with.
            float value = (minmax == 0 ? s_StartDragAnchorMin[axis] : s_StartDragAnchorMax[axis]);
            return (value < -0.001f || value > 1.001f);
        }
        private static bool ShouldDoIntSnapping(RectTransform rect)
        {
            if (rect == null) return false;
            Canvas canvas = rect.gameObject.GetComponentInParent<Canvas>();
            return (canvas != null && canvas.renderMode != RenderMode.WorldSpace);
        }

    }

    //source from UnityCsReference
    public class LayoutDropdownWindow : PopupWindowContent
    {
        class Styles
        {
            public Color tableHeaderColor;
            public Color tableLineColor;
            public Color parentColor;
            public Color selfColor;
            public Color simpleAnchorColor;
            public Color stretchAnchorColor;
            public Color anchorCornerColor;
            public Color pivotColor;

            public GUIStyle frame;
            public GUIStyle label = new GUIStyle(EditorStyles.miniLabel);

            public Styles()
            {
                frame = new GUIStyle();
                Texture2D tex = new Texture2D(4, 4);
                tex.SetPixels(new Color[]
                {
                    Color.white, Color.white, Color.white, Color.white,
                    Color.white, Color.clear, Color.clear, Color.white,
                    Color.white, Color.clear, Color.clear, Color.white,
                    Color.white, Color.white, Color.white, Color.white
                });
                tex.filterMode = FilterMode.Point;
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                frame.normal.background = tex;
                frame.border = new RectOffset(2, 2, 2, 2);

                label.alignment = TextAnchor.LowerCenter;

                if (EditorGUIUtility.isProSkin)
                {
                    tableHeaderColor = new Color(0.18f, 0.18f, 0.18f, 1);
                    tableLineColor = new Color(1, 1, 1, 0.3f);
                    parentColor = new Color(0.4f, 0.4f, 0.4f, 1);
                    selfColor = new Color(0.6f, 0.6f, 0.6f, 1);
                    simpleAnchorColor = new Color(0.7f, 0.3f, 0.3f, 1);
                    stretchAnchorColor = new Color(0.0f, 0.6f, 0.8f, 1);
                    anchorCornerColor = new Color(0.8f, 0.6f, 0.0f, 1);
                    pivotColor = new Color(0.0f, 0.6f, 0.8f, 1);
                }
                else
                {
                    tableHeaderColor = new Color(0.8f, 0.8f, 0.8f, 1);
                    tableLineColor = new Color(0, 0, 0, 0.5f);
                    parentColor = new Color(0.55f, 0.55f, 0.55f, 1);
                    selfColor = new Color(0.2f, 0.2f, 0.2f, 1);
                    simpleAnchorColor = new Color(0.8f, 0.3f, 0.3f, 1);
                    stretchAnchorColor = new Color(0.2f, 0.5f, 0.9f, 1);
                    anchorCornerColor = new Color(0.6f, 0.4f, 0.0f, 1);
                    pivotColor = new Color(0.2f, 0.5f, 0.9f, 1);
                }
            }
        }
        static Styles s_Styles;


        Vector2[,] m_InitValues;

        const int kTopPartHeight = 38;
        static float[] kPivotsForModes = new float[] { 0, 0.5f, 1, 0.5f, 0.5f }; // Only for actual modes, not for Undefined.
        static string[] kHLabels = new string[] { "custom", "left", "center", "right", "stretch", "%" };
        static string[] kVLabels = new string[] { "custom", "top", "middle", "bottom", "stretch", "%" };

        public enum LayoutMode { Undefined = -1, Min = 0, Middle = 1, Max = 2, Stretch = 3 }
        ViewSystemRectTransformData _rectTransformData;
        RectTransform _rectTransform;
        ViewSystemSaveData _saveData;
        public LayoutDropdownWindow(ViewSystemRectTransformData rectTransformData, RectTransform rectTrasnform, ViewSystemSaveData saveData)
        {
            _saveData = saveData;
            _rectTransformData = rectTransformData;
            _rectTransform = rectTrasnform;
            m_InitValues = new Vector2[1, 4];
            m_InitValues[0, 0] = rectTransformData.anchorMin;
            m_InitValues[0, 1] = rectTransformData.anchorMax;
            m_InitValues[0, 2] = rectTransformData.anchoredPosition;
            m_InitValues[0, 3] = rectTransformData.sizeDelta;
        }

        public override void OnOpen()
        {
            EditorApplication.modifierKeysChanged += editorWindow.Repaint;
        }

        public override void OnClose()
        {
            EditorApplication.modifierKeysChanged -= editorWindow.Repaint;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(262, 262 + kTopPartHeight);
        }

        public override void OnGUI(Rect rect)
        {
            if (s_Styles == null)
                s_Styles = new Styles();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                editorWindow.Close();

            GUI.Label(new Rect(rect.x + 5, rect.y + 3, rect.width - 10, 16), EditorGUIUtility.TrTextContent("Anchor Presets"), EditorStyles.boldLabel);
            // GUI.Label(new Rect(rect.x + 5, rect.y + 3 + 16, rect.width - 10, 16), EditorGUIUtility.TrTextContent("Shift: Also set pivot     Alt: Also set position"), EditorStyles.label);

            Color oldColor = GUI.color;
            GUI.color = s_Styles.tableLineColor * oldColor;
            GUI.DrawTexture(new Rect(0, kTopPartHeight - 1, 400, 1), EditorGUIUtility.whiteTexture);
            GUI.color = oldColor;

            GUI.BeginGroup(new Rect(rect.x, rect.y + kTopPartHeight, rect.width, rect.height - kTopPartHeight));
            TableGUI(rect);
            GUI.EndGroup();
        }

        static LayoutMode SwappedVMode(LayoutMode vMode)
        {
            if (vMode == LayoutMode.Min)
                return LayoutMode.Max;
            else if (vMode == LayoutMode.Max)
                return LayoutMode.Min;
            return vMode;
        }

        internal static void DrawLayoutModeHeadersOutsideRect(Rect rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 sizeDelta)
        {
            LayoutMode hMode = GetLayoutModeForAxis(anchorMin, anchorMax, 0);
            LayoutMode vMode = GetLayoutModeForAxis(anchorMin, anchorMax, 1);
            vMode = SwappedVMode(vMode);
            DrawLayoutModeHeaderOutsideRect(rect, 0, hMode);
            DrawLayoutModeHeaderOutsideRect(rect, 1, vMode);
        }

        internal static void DrawLayoutModeHeaderOutsideRect(Rect position, int axis, LayoutMode mode)
        {
            Rect headerRect = new Rect(position.x, position.y - 16, position.width, 16);

            Matrix4x4 normalMatrix = GUI.matrix;
            if (axis == 1)
                GUIUtility.RotateAroundPivot(-90, position.center);

            int index = (int)(mode) + 1;
            GUI.Label(headerRect, axis == 0 ? kHLabels[index] : kVLabels[index], s_Styles.label);

            GUI.matrix = normalMatrix;
        }

        void TableGUI(Rect rect)
        {
            int padding = 6;
            int size = 31 + padding * 2;
            int spacing = 0;
            int[] groupings = new int[] { 15, 30, 30, 30, 45, 45 };

            Color oldColor = GUI.color;

            int headerW = 62;
            GUI.color = s_Styles.tableHeaderColor * oldColor;
            GUI.DrawTexture(new Rect(0, 0, 400, headerW), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, headerW, 400), EditorGUIUtility.whiteTexture);
            GUI.color = s_Styles.tableLineColor * oldColor;
            GUI.DrawTexture(new Rect(0, headerW, 400, 1), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(headerW, 0, 1, 400), EditorGUIUtility.whiteTexture);
            GUI.color = oldColor;

            LayoutMode hMode = GetLayoutModeForAxis(_rectTransformData.anchorMin, _rectTransformData.anchorMax, 0);
            LayoutMode vMode = GetLayoutModeForAxis(_rectTransformData.anchorMin, _rectTransformData.anchorMax, 1);
            vMode = SwappedVMode(vMode);

            // bool doPivot = Event.current.shift;
            bool doPivot = false;
            // bool doPosition = Event.current.alt;
            bool doPosition = false;

            int number = 5;

            for (int i = 0; i < number; i++)
            {
                LayoutMode cellHMode = (LayoutMode)(i - 1);

                for (int j = 0; j < number; j++)
                {
                    LayoutMode cellVMode = (LayoutMode)(j - 1);

                    if (i == 0 && j == 0 && vMode >= 0 && hMode >= 0)
                        continue;

                    Rect position = new Rect(
                        i * (size + spacing) + groupings[i],
                        j * (size + spacing) + groupings[j],
                        size,
                        size);

                    if (j == 0 && !(i == 0 && hMode != LayoutMode.Undefined))
                        DrawLayoutModeHeaderOutsideRect(position, 0, cellHMode);
                    if (i == 0 && !(j == 0 && vMode != LayoutMode.Undefined))
                        DrawLayoutModeHeaderOutsideRect(position, 1, cellVMode);

                    bool selected = (cellHMode == hMode) && (cellVMode == vMode);

                    bool selectedHeader = (i == 0 && cellVMode == vMode) || (j == 0 && cellHMode == hMode);

                    if (Event.current.type == EventType.Repaint)
                    {
                        if (selected)
                        {
                            GUI.color = Color.white * oldColor;
                            s_Styles.frame.Draw(position, false, false, false, false);
                        }
                        else if (selectedHeader)
                        {
                            GUI.color = new Color(1, 1, 1, 0.7f) * oldColor;
                            s_Styles.frame.Draw(position, false, false, false, false);
                        }
                    }

                    DrawLayoutMode(
                        new Rect(position.x + padding, position.y + padding, position.width - padding * 2, position.height - padding * 2),
                        cellHMode, cellVMode,
                        doPivot, doPosition);

                    int clickCount = Event.current.clickCount;
                    if (GUI.Button(position, GUIContent.none, GUIStyle.none))
                    {
                        if (_rectTransform == null)
                        {
                            if (EditorUtility.DisplayDialog(
                                "Warning",
                                "Modify Anchor without preview will not recalculate AnchorPosition and Padding values, Do you want to continue?",
                                "Yes",
                                "No"))
                            {
                                OnTableItemClick();
                            }
                            else
                            {
                                return;
                            }
                        }
                        OnTableItemClick();

                        void OnTableItemClick()
                        {
                            SetLayoutModeForAxis(_saveData, _rectTransformData.anchorMin, _rectTransformData, _rectTransform, 0, cellHMode, doPivot, doPosition, m_InitValues);
                            SetLayoutModeForAxis(_saveData, _rectTransformData.anchorMin, _rectTransformData, _rectTransform, 1, SwappedVMode(cellVMode), doPivot, doPosition, m_InitValues);
                            if (clickCount == 2)
                                editorWindow.Close();
                            else
                                editorWindow.Repaint();

                            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                        }
                    }
                }
            }
            GUI.color = oldColor;
        }

        static LayoutMode GetLayoutModeForAxis(
            Vector2 anchorMin,
            Vector2 anchorMax,
            int axis)
        {
            if (anchorMin[axis] == 0 && anchorMax[axis] == 0)
                return LayoutMode.Min;
            if (anchorMin[axis] == 0.5f && anchorMax[axis] == 0.5f)
                return LayoutMode.Middle;
            if (anchorMin[axis] == 1 && anchorMax[axis] == 1)
                return LayoutMode.Max;
            if (anchorMin[axis] == 0 && anchorMax[axis] == 1)
                return LayoutMode.Stretch;
            return LayoutMode.Undefined;
        }

        static void SetLayoutModeForAxis(
            ViewSystemSaveData saveData,
            Vector2 anchorMin,
            ViewSystemRectTransformData vs_rect,
            RectTransform rect,
            int axis, LayoutMode layoutMode,
            bool doPivot, bool doPosition, Vector2[,] defaultValues)
        {
            // anchorMin.serializedObject.ApplyModifiedProperties();

            // for (int i = 0; i < targetObjects.Length; i++)
            // {
            // RectTransform gui = targetObjects[i] as RectTransform;
            Undo.RecordObject(saveData, "ViewSystem_ChangeRectangleAnchors");
            // if (doPosition)
            // {
            //     if (defaultValues != null && defaultValues.Length > i)
            //     {
            //         Vector2 temp;
            //         temp = vs_rect.anchorMin;
            //         temp[axis] = defaultValues[0, 0][axis];
            //         vs_rect.anchorMin = temp;

            //         temp = vs_rect.anchorMax;
            //         temp[axis] = defaultValues[0, 1][axis];
            //         vs_rect.anchorMax = temp;

            //         temp = vs_rect.anchoredPosition;
            //         temp[axis] = defaultValues[0, 2][axis];
            //         vs_rect.anchoredPosition = temp;

            //         temp = vs_rect.sizeDelta;
            //         temp[axis] = defaultValues[0, 3][axis];
            //         vs_rect.sizeDelta = temp;
            //     }
            // }

            // if (doPivot && layoutMode != LayoutMode.Undefined)
            // {
            //     VS_EditorUtility.SetPivotSmart(vs_rect, rect, kPivotsForModes[(int)layoutMode], axis, true, true);
            // }

            Vector2 refPosition = Vector2.zero;
            switch (layoutMode)
            {
                case LayoutMode.Min:
                    VS_EditorUtility.SetAnchorSmart(vs_rect, rect, 0, axis, false, true, true);
                    VS_EditorUtility.SetAnchorSmart(vs_rect, rect, 0, axis, true, true, true);
                    refPosition = vs_rect.offsetMin;
                    break;
                case LayoutMode.Middle:
                    VS_EditorUtility.SetAnchorSmart(vs_rect, rect, 0.5f, axis, false, true, true);
                    VS_EditorUtility.SetAnchorSmart(vs_rect, rect, 0.5f, axis, true, true, true);
                    refPosition = (vs_rect.offsetMin + vs_rect.offsetMax) * 0.5f;
                    break;
                case LayoutMode.Max:
                    VS_EditorUtility.SetAnchorSmart(vs_rect, rect, 1, axis, false, true, true);
                    VS_EditorUtility.SetAnchorSmart(vs_rect, rect, 1, axis, true, true, true);
                    refPosition = vs_rect.offsetMax;
                    break;
                case LayoutMode.Stretch:
                    VS_EditorUtility.SetAnchorSmart(vs_rect, rect, 0, axis, false, true, true);
                    VS_EditorUtility.SetAnchorSmart(vs_rect, rect, 1, axis, true, true, true);
                    refPosition = (vs_rect.offsetMin + vs_rect.offsetMax) * 0.5f;
                    break;
            }

            // if (doPosition)
            // {
            //     // Handle position
            //     Vector2 rectPosition = vs_rect.anchoredPosition;
            //     rectPosition[axis] -= refPosition[axis];
            //     vs_rect.anchoredPosition = rectPosition;

            //     // Handle sizeDelta
            //     if (layoutMode == LayoutMode.Stretch)
            //     {
            //         Vector2 rectSizeDelta = vs_rect.sizeDelta;
            //         rectSizeDelta[axis] = 0;
            //         vs_rect.sizeDelta = rectSizeDelta;
            //     }
            // }
            // }
            // anchorMin.serializedObject.Update();
        }

        internal static void DrawLayoutMode(Rect rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 sizeDelta)
        {
            LayoutMode hMode = GetLayoutModeForAxis(anchorMin, anchorMax, 0);
            LayoutMode vMode = GetLayoutModeForAxis(anchorMin, anchorMax, 1);
            vMode = SwappedVMode(vMode);
            DrawLayoutMode(rect, hMode, vMode);
        }

        internal static void DrawLayoutMode(Rect position, LayoutMode hMode, LayoutMode vMode)
        {
            DrawLayoutMode(position, hMode, vMode, false, false);
        }

        internal static void DrawLayoutMode(Rect position, LayoutMode hMode, LayoutMode vMode, bool doPivot)
        {
            DrawLayoutMode(position, hMode, vMode, doPivot, false);
        }

        internal static void DrawLayoutMode(Rect position, LayoutMode hMode, LayoutMode vMode, bool doPivot, bool doPosition)
        {
            if (s_Styles == null)
                s_Styles = new Styles();

            Color oldColor = GUI.color;

            // Make parent size the largest possible square, but enforce it's an uneven number.
            int parentWidth = (int)Mathf.Min(position.width, position.height);
            if (parentWidth % 2 == 0)
                parentWidth--;

            int selfWidth = parentWidth / 2;
            if (selfWidth % 2 == 0)
                selfWidth++;

            Vector2 parentSize = parentWidth * Vector2.one;
            Vector2 selfSize = selfWidth * Vector2.one;
            Vector2 padding = (position.size - parentSize) / 2;
            padding.x = Mathf.Floor(padding.x);
            padding.y = Mathf.Floor(padding.y);
            Vector2 padding2 = (position.size - selfSize) / 2;
            padding2.x = Mathf.Floor(padding2.x);
            padding2.y = Mathf.Floor(padding2.y);

            Rect outer = new Rect(position.x + padding.x, position.y + padding.y, parentSize.x, parentSize.y);
            Rect inner = new Rect(position.x + padding2.x, position.y + padding2.y, selfSize.x, selfSize.y);
            if (doPosition)
            {
                for (int axis = 0; axis < 2; axis++)
                {
                    LayoutMode mode = (axis == 0 ? hMode : vMode);

                    if (mode == LayoutMode.Min)
                    {
                        Vector2 center = inner.center;
                        center[axis] += outer.min[axis] - inner.min[axis];
                        inner.center = center;
                    }
                    if (mode == LayoutMode.Middle)
                    {
                        // TODO
                    }
                    if (mode == LayoutMode.Max)
                    {
                        Vector2 center = inner.center;
                        center[axis] += outer.max[axis] - inner.max[axis];
                        inner.center = center;
                    }
                    if (mode == LayoutMode.Stretch)
                    {
                        Vector2 innerMin = inner.min;
                        Vector2 innerMax = inner.max;
                        innerMin[axis] = outer.min[axis];
                        innerMax[axis] = outer.max[axis];
                        inner.min = innerMin;
                        inner.max = innerMax;
                    }
                }
            }

            Rect anchor = new Rect();
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;
            for (int axis = 0; axis < 2; axis++)
            {
                LayoutMode mode = (axis == 0 ? hMode : vMode);

                if (mode == LayoutMode.Min)
                {
                    min[axis] = outer.min[axis] + 0.5f;
                    max[axis] = outer.min[axis] + 0.5f;
                }
                if (mode == LayoutMode.Middle)
                {
                    min[axis] = outer.center[axis];
                    max[axis] = outer.center[axis];
                }
                if (mode == LayoutMode.Max)
                {
                    min[axis] = outer.max[axis] - 0.5f;
                    max[axis] = outer.max[axis] - 0.5f;
                }
                if (mode == LayoutMode.Stretch)
                {
                    min[axis] = outer.min[axis] + 0.5f;
                    max[axis] = outer.max[axis] - 0.5f;
                }
            }
            anchor.min = min;
            anchor.max = max;

            // Draw parent rect
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = s_Styles.parentColor * oldColor;
                s_Styles.frame.Draw(outer, false, false, false, false);
            }

            // Draw anchor lines
            if (hMode != LayoutMode.Undefined && hMode != LayoutMode.Stretch)
            {
                GUI.color = s_Styles.simpleAnchorColor * oldColor;
                GUI.DrawTexture(new Rect(anchor.xMin - 0.5f, outer.y + 1, 1, outer.height - 2), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(anchor.xMax - 0.5f, outer.y + 1, 1, outer.height - 2), EditorGUIUtility.whiteTexture);
            }
            if (vMode != LayoutMode.Undefined && vMode != LayoutMode.Stretch)
            {
                GUI.color = s_Styles.simpleAnchorColor * oldColor;
                GUI.DrawTexture(new Rect(outer.x + 1, anchor.yMin - 0.5f, outer.width - 2, 1), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(outer.x + 1, anchor.yMax - 0.5f, outer.width - 2, 1), EditorGUIUtility.whiteTexture);
            }

            // Draw stretch mode arrows
            if (hMode == LayoutMode.Stretch)
            {
                GUI.color = s_Styles.stretchAnchorColor * oldColor;
                DrawArrow(new Rect(inner.x + 1, inner.center.y - 0.5f, inner.width - 2, 1));
            }
            if (vMode == LayoutMode.Stretch)
            {
                GUI.color = s_Styles.stretchAnchorColor * oldColor;
                DrawArrow(new Rect(inner.center.x - 0.5f, inner.y + 1, 1, inner.height - 2));
            }

            // Draw self rect
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = s_Styles.selfColor * oldColor;
                s_Styles.frame.Draw(inner, false, false, false, false);
            }

            // Draw pivot
            if (doPivot && hMode != LayoutMode.Undefined && vMode != LayoutMode.Undefined)
            {
                Vector2 pivot = new Vector2(
                    Mathf.Lerp(inner.xMin + 0.5f, inner.xMax - 0.5f, kPivotsForModes[(int)hMode]),
                    Mathf.Lerp(inner.yMin + 0.5f, inner.yMax - 0.5f, kPivotsForModes[(int)vMode])
                );

                GUI.color = s_Styles.pivotColor * oldColor;
                GUI.DrawTexture(new Rect(pivot.x - 2.5f, pivot.y - 1.5f, 5, 3), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(pivot.x - 1.5f, pivot.y - 2.5f, 3, 5), EditorGUIUtility.whiteTexture);
            }

            // Draw anchor corners
            if (hMode != LayoutMode.Undefined && vMode != LayoutMode.Undefined)
            {
                GUI.color = s_Styles.anchorCornerColor * oldColor;
                GUI.DrawTexture(new Rect(anchor.xMin - 1.5f, anchor.yMin - 1.5f, 2, 2), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(anchor.xMax - 0.5f, anchor.yMin - 1.5f, 2, 2), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(anchor.xMin - 1.5f, anchor.yMax - 0.5f, 2, 2), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(anchor.xMax - 0.5f, anchor.yMax - 0.5f, 2, 2), EditorGUIUtility.whiteTexture);
            }

            GUI.color = oldColor;
        }

        static void DrawArrow(Rect lineRect)
        {
            GUI.DrawTexture(lineRect, EditorGUIUtility.whiteTexture);
            if (lineRect.width == 1)
            {
                GUI.DrawTexture(new Rect(lineRect.x - 1, lineRect.y + 1, 3, 1), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(lineRect.x - 2, lineRect.y + 2, 5, 1), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(lineRect.x - 1, lineRect.yMax - 2, 3, 1), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(lineRect.x - 2, lineRect.yMax - 3, 5, 1), EditorGUIUtility.whiteTexture);
            }
            else
            {
                GUI.DrawTexture(new Rect(lineRect.x + 1, lineRect.y - 1, 1, 3), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(lineRect.x + 2, lineRect.y - 2, 1, 5), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(lineRect.xMax - 2, lineRect.y - 1, 1, 3), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(lineRect.xMax - 3, lineRect.y - 2, 1, 5), EditorGUIUtility.whiteTexture);
            }
        }
    }


}

