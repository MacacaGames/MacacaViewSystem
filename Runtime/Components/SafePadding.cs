
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if DEBUG_NOTCH_SOLUTION
using System.Linq;
#endif

//Source from https://github.com/5argon/NotchSolution
//Since Assembly Define issue, create a varient version SafePadding in ViewSystem
namespace MacacaGames.ViewSystem
{
    /// <summary>
    /// Make the <see cref="RectTransform"/> of object with this component driven into full stretch to its immediate parent, 
    /// then apply padding according to device's reported <see cref="Screen.safeArea"/>.
    /// Therefore makes an area inside this object safe for input-receiving components.
    /// 
    /// Then it is possible to make other objects safe area responsive by anchoring thier positions to this object's edges while being a child object.
    /// </summary>
    /// <remarks>
    /// Safe area defines an area on the phone's screen where it is safe to place your game-related input receiving components without colliding with
    /// other on-screen features on the phone. Usually this means it is also "visually safe" as all possible notches should be outside of safe area.
    /// 
    /// The amount of padding is a <see cref="Screen.safeArea"/> interpolated into <see cref="RectTransform"/> of root <see cref="Canvas"/> found traveling up from this object.
    /// 
    /// It should be a direct child of top canvas, or deeper child of some similarly full stretch rect in order to look right,
    /// although in reality it just pad in the shape of <see cref="Screen.safeArea"/> regardless of its parent rectangle size.
    /// </remarks>
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    [RequireComponent(typeof(RectTransform))]
    public class SafePadding : UnityEngine.EventSystems.UIBehaviour, ILayoutSelfController
    {
#pragma warning disable 0649
        [SerializeField] SupportedOrientations orientationType;
        [SerializeField] PerEdgeValues portraitOrDefaultPaddings;
        [SerializeField] PerEdgeValues landscapePaddings;

        [Tooltip("The value read from all edges are applied to the opposite side of a RectTransform instead. Useful when you have rotated or negatively scaled RectTransform.")]
        [SerializeField] bool flipPadding = false;
#pragma warning restore 0649
        public void SetPaddingValue(PerEdgeValues _edgeValues)
        {


            portraitOrDefaultPaddings = _edgeValues;
#if (UNITY_WEBGL || UNITY_STANDALONE) && !UNITY_EDITOR

        // safe padding on webgl get bad result right now, so ignore this setting on webgl platform and take a look on this issue later
        portraitOrDefaultPaddings.left = EdgeEvaluationMode.Off;
        portraitOrDefaultPaddings.bottom = EdgeEvaluationMode.Off;
        portraitOrDefaultPaddings.top = EdgeEvaluationMode.Off;
        portraitOrDefaultPaddings.right = EdgeEvaluationMode.Off;
#endif  

            UpdateRectBase();
        }
        private DrivenRectTransformTracker m_Tracker;

        private protected void UpdateRect()
        {
            if (rectTransform == null)
            {
                return;
            }

            //TODO migrate auto Orientation with ViewSystem
            // PerEdgeValues selectedOrientation =
            // orientationType == SupportedOrientations.Dual ?
            // GetCurrentOrientation() == ScreenOrientation.Landscape ?
            // landscapePaddings : portraitOrDefaultPaddings
            // : portraitOrDefaultPaddings;
            PerEdgeValues selectedOrientation = portraitOrDefaultPaddings;
            m_Tracker.Clear();
            m_Tracker.Add(this, rectTransform,
                (LockSide(selectedOrientation.left) ? DrivenTransformProperties.AnchorMinX : 0) |
                (LockSide(selectedOrientation.right) ? DrivenTransformProperties.AnchorMaxX : 0) |
                (LockSide(selectedOrientation.bottom) ? DrivenTransformProperties.AnchorMinY : 0) |
                (LockSide(selectedOrientation.top) ? DrivenTransformProperties.AnchorMaxY : 0) |
                (LockSide(selectedOrientation.left) && LockSide(selectedOrientation.right) ? (DrivenTransformProperties.SizeDeltaX | DrivenTransformProperties.AnchoredPositionX) : 0) |
                (LockSide(selectedOrientation.top) && LockSide(selectedOrientation.bottom) ? (DrivenTransformProperties.SizeDeltaY | DrivenTransformProperties.AnchoredPositionY) : 0)
            );

            bool LockSide(EdgeEvaluationMode saem)
            {
                switch (saem)
                {
                    case EdgeEvaluationMode.On:
                    case EdgeEvaluationMode.Off:
                        return true;
                    //When "Unlocked" is supported, it will be false.
                    default:
                        return false;
                }
            }

            //Lock the anchor mode to full stretch first.

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;

            var topRect = GetCanvasRect();
            var safeAreaRelative = SafeAreaRelative;

#if DEBUG_NOTCH_SOLUTION
            Debug.Log($"Top {topRect} safe {safeAreaRelative} min {safeAreaRelative.xMin} {safeAreaRelative.yMin}");
#endif

            var relativeLDUR = new float[4]
            {
                safeAreaRelative.xMin,
                safeAreaRelative.yMin,
                1 - (safeAreaRelative.yMin + safeAreaRelative.height),
                1 - (safeAreaRelative.xMin + safeAreaRelative.width),
            };

#if DEBUG_NOTCH_SOLUTION
            Debug.Log($"SafeLDUR {string.Join(" ", relativeLDUR.Select(x => x.ToString()))}");
#endif
            // fixed: sometimes relativeLDUR will be NAN when start at some android devices. 
            // if relativeLDUR is NAN then sizeDelta will be NAN, the safe area will be wrong.
            if (float.IsNaN(relativeLDUR[0]))
            {
                relativeLDUR[0] = 0;
            }

            if (float.IsNaN(relativeLDUR[1]))
            {
                relativeLDUR[1] = 0;
            }

            if (float.IsNaN(relativeLDUR[2]))
            {
                relativeLDUR[2] = 0;
            }

            if (float.IsNaN(relativeLDUR[3]))
            {
                relativeLDUR[3] = 0;
            }

            var currentRect = rectTransform.rect;

            //TODO : Calculate the current padding relative, to enable "Unlocked" mode. (Not forcing zero padding)
            var finalPaddingsLDUR = new float[4]
            {
                0,0,0,0
            };

            switch (selectedOrientation.left)
            {
                case EdgeEvaluationMode.On:
                    finalPaddingsLDUR[0] = topRect.width * relativeLDUR[0];
                    break;

            }

            switch (selectedOrientation.right)
            {
                case EdgeEvaluationMode.On:
                    finalPaddingsLDUR[3] = topRect.width * relativeLDUR[3];
                    break;

            }

            switch (selectedOrientation.bottom)
            {
                case EdgeEvaluationMode.On:
                    finalPaddingsLDUR[1] = topRect.height * relativeLDUR[1];
                    break;

            }

            switch (selectedOrientation.top)
            {
                case EdgeEvaluationMode.On:
                    finalPaddingsLDUR[2] = topRect.height * relativeLDUR[2];
                    break;
            }

            //Apply influence to the calculated padding
            finalPaddingsLDUR[0] *= portraitOrDefaultPaddings.influence * portraitOrDefaultPaddings.influenceLeft;
            finalPaddingsLDUR[1] *= portraitOrDefaultPaddings.influence * portraitOrDefaultPaddings.influenceBottom;
            finalPaddingsLDUR[2] *= portraitOrDefaultPaddings.influence * portraitOrDefaultPaddings.influenceTop;
            finalPaddingsLDUR[3] *= portraitOrDefaultPaddings.influence * portraitOrDefaultPaddings.influenceRight;

            if (flipPadding)
            {
                float remember = 0;
                finalPaddingsLDUR[0] = remember;
                finalPaddingsLDUR[0] = finalPaddingsLDUR[3];
                finalPaddingsLDUR[3] = remember;

                finalPaddingsLDUR[1] = remember;
                finalPaddingsLDUR[1] = finalPaddingsLDUR[2];
                finalPaddingsLDUR[2] = remember;
            }

#if DEBUG_NOTCH_SOLUTION
            Debug.Log($"FinalLDUR {string.Join(" ", finalPaddingsLDUR.Select(x => x.ToString()))}");
#endif

            //Combined padding becomes size delta.
            var sizeDelta = rectTransform.sizeDelta;
            sizeDelta.x = -(finalPaddingsLDUR[0] + finalPaddingsLDUR[3]);
            sizeDelta.y = -(finalPaddingsLDUR[1] + finalPaddingsLDUR[2]);
            rectTransform.sizeDelta = sizeDelta;

            //The rect remaining after subtracted the size delta.
            Vector2 rectWidthHeight = new Vector2(topRect.width + sizeDelta.x, topRect.height + sizeDelta.y);

#if DEBUG_NOTCH_SOLUTION
            Debug.Log($"RectWidthHeight {rectWidthHeight}");
#endif

            //Anchor position's answer is depending on pivot too. Where the pivot point is defines where 0 anchor point is.
            Vector2 zeroPosition = new Vector2(rectTransform.pivot.x * topRect.width, rectTransform.pivot.y * topRect.height);
            Vector2 pivotInRect = new Vector2(rectTransform.pivot.x * rectWidthHeight.x, rectTransform.pivot.y * rectWidthHeight.y);

#if DEBUG_NOTCH_SOLUTION
            Debug.Log($"zeroPosition {zeroPosition}");
#endif

            //Calculate like zero position is at bottom left first, then diff with the real zero position.
            rectTransform.anchoredPosition3D = new Vector3(
                finalPaddingsLDUR[0] + pivotInRect.x - zeroPosition.x,
                finalPaddingsLDUR[1] + pivotInRect.y - zeroPosition.y,
            rectTransform.anchoredPosition3D.z);
        }
        /// <summary>
        /// Overrides <see cref="UIBehaviour"/>
        /// 
        /// This doesn't work when flipping the orientation to opposite side (180 deg). It only works for 90 deg. rotation because that
        /// makes the rect transform changes dimension.
        /// </summary>
        protected override void OnRectTransformDimensionsChange()
        {
            UpdateRectBase();
        }
        private void UpdateRectBase()
        {
            if (!(enabled && gameObject.activeInHierarchy)) return;
            UpdateRect();
        }
        internal static Rect cachedScreenSafeArea;
        internal static Rect cachedScreenSafeAreaRelative;
        internal static bool safeAreaRelativeCached;
        internal static Rect ScreenSafeAreaRelative
        {
            get
            {
                Rect absolutePaddings = Screen.safeArea;
                cachedScreenSafeAreaRelative = ToScreenRelativeRect(absolutePaddings);
                cachedScreenSafeArea = absolutePaddings;
                safeAreaRelativeCached = true;
                return cachedScreenSafeAreaRelative;
            }
        }
        private protected Rect GetCanvasRect()
        {
            var topLevelCanvas = GetTopLevelCanvas();
            Vector2 topRectSize = topLevelCanvas.GetComponent<RectTransform>().sizeDelta;
            return new Rect(Vector2.zero, topRectSize);
            Canvas GetTopLevelCanvas()
            {
                var canvas = this.GetComponentInParent<Canvas>();
                var rootCanvas = canvas.rootCanvas;
                return rootCanvas;
            }
        }

        private static Rect ToScreenRelativeRect(Rect absoluteRect)
        {
            // int w = Screen.width;
            // int h = Screen.height;
#if UNITY_EDITOR
            int w = Screen.width;
            int h = Screen.height;
#else
                        int w = Screen.currentResolution.width;
                        int h = Screen.currentResolution.height;
#endif
            //Debug.Log($"{w} {h} {Screen.currentResolution} {absoluteRect}");
            return new Rect(
                absoluteRect.x / w,
                absoluteRect.y / h,
                absoluteRect.width / w,
                absoluteRect.height / h
            );
        }
        /// <summary>
        /// How a component looks at a particular edge to take the edge's property.
        /// Meaning depends on context of that component.
        /// </summary>
        public enum EdgeEvaluationMode
        {
            /// <summary>
            /// Do not use a value reported from that edge.
            /// </summary>
            Off,

            /// <summary>
            /// Use a value reported from that edge.
            /// </summary>
            On,
        }

        [Serializable]
        public class PerEdgeValues
        {
            public EdgeEvaluationMode left = EdgeEvaluationMode.Off;
            public EdgeEvaluationMode bottom = EdgeEvaluationMode.Off;
            public EdgeEvaluationMode top = EdgeEvaluationMode.Off;
            public EdgeEvaluationMode right = EdgeEvaluationMode.Off;
            public float influence = 1;
            public float influenceLeft = 1;
            public float influenceBottom = 1;
            public float influenceTop = 1;
            public float influenceRight = 1;
        }

        internal enum SupportedOrientations
        {
            /// <summary>
            /// The game is fixed on only portrait or landscape. Device rotation maybe possible to rotate 180 degree to the opposite side.
            /// </summary>
            Single,

            /// <summary>
            /// It is possible to rotate the screen between portrait and landscape. (90 degree rotation is possible)
            /// </summary>
            Dual,
        }
        internal static ScreenOrientation GetCurrentOrientation()
           => Screen.width > Screen.height ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
        [System.NonSerialized]
        private RectTransform m_Rect;
        private protected RectTransform rectTransform
        {
            get
            {
                if (m_Rect == null)
                    m_Rect = GetComponent<RectTransform>();
                return m_Rect;
            }
        }
        /// <summary>
        /// Already taken account whether should trust Notch Simulator or Unity's [Device Simulator package](https://docs.unity3d.com/Packages/com.unity.device-simulator@latest/).
        /// </summary>
        protected Rect SafeAreaRelative => ScreenSafeAreaRelative;
        void ILayoutController.SetLayoutHorizontal()
        {
            UpdateRectBase();
        }

        void ILayoutController.SetLayoutVertical()
        {
        }
        private WaitForEndOfFrame eofWait = new WaitForEndOfFrame();

        private void DelayedUpdate() => StartCoroutine(DelayedUpdateRoutine());
        private IEnumerator DelayedUpdateRoutine()
        {
            yield return eofWait;
            UpdateRectBase();
        }
        /// <summary>
        /// Overrides <see cref="UIBehaviour"/>
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            DelayedUpdate();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Overrides <see cref="UIBehaviour"/>
        /// </summary>
        protected override void Reset()
        {
            base.Reset();
        }

        /// <summary>
        /// Overrides <see cref="UIBehaviour"/>
        /// </summary>
        protected override void OnValidate()
        {
            if (gameObject.activeInHierarchy)
            {
                DelayedUpdate();
            }
        }
#endif
    }

}
