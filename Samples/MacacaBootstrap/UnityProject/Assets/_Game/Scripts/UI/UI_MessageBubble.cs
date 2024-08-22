using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MacacaGames;
using MacacaGames.ViewSystem;
using System;
using System.Threading.Tasks;

public class UI_MessageBubble : MonoBehaviour, IViewElementInjectable
{
    RectTransform rectTransform;
    [SerializeField]
    RectTransform bubbleObject;
    [SerializeField]
    RectTransform triObject;
    [SerializeField]
    UnityEngine.UI.Extensions.UIFlippable flippable;
    [SerializeField]
    TMPro.TextMeshProUGUI textMeshProUGUI;

    [SerializeField]
    CanvasGroup canvasGroup;
    void Awake()
    {
        rectTransform = (RectTransform)transform;
    }
    bool isInit = false;

    async Task Init()
    {
        rectTransform = (RectTransform)transform;
        isInit = true;

        ViewController.OnViewPageChange += OnViewPageChange;
        ViewController.OnOverlayPageLeave += OnOverlayPageLeave;
    }

    private void OnOverlayPageLeave(object sender, ViewControllerBase.ViewPageEventArgs e)
    {
        if (e.currentViewPage.name != ViewSystemScriptable.ViewPages.MessageBubble)
        {
            Hide();
        }
    }

    private void OnViewPageChange(object sender, ViewControllerBase.ViewPageEventArgs e)
    {
        Hide();
    }

    /// <summary>
    /// Show a message bubble
    /// </summary>
    /// <param name="msg">the message to show</param>
    /// <param name="target">Target object to place the message bubble</param>
    /// <param name="triPosition">The triangle position of the message bubble</param>
    /// <param name="yOffset">the offect of the message box in y axis</param>
    /// <param name="showDuration">the show animation duration</param>
    /// <param name="hideDuration">the hide animation duration</param>
    /// <param name="keepDuration">how the message bubble to keeps on screen</param>
    public static void Show(string msg, Transform target, TriPosition triPosition, float yOffset = 0, float showDuration = 0.2f, float hideDuration = 0.2f, float keepDuration = 1.5f)
    {
        UI_MessageBubble uiMessageBox = ViewController.Instance.GetInjectionInstance<UI_MessageBubble>();

        if (uiMessageBox.isInit == false)
            uiMessageBox.Init();

        uiMessageBox.showTriPosition = triPosition;
        float halfHigh = (target as RectTransform).sizeDelta.y * 0.5f;
        uiMessageBox.objectOffect = triPosition == TriPosition.Down ? halfHigh : -halfHigh;
        uiMessageBox.extraOffect = yOffset;
        uiMessageBox.showPosition = target.position;
        uiMessageBox.showDuration = showDuration;
        uiMessageBox.hideDuration = hideDuration;
        uiMessageBox.keepDuration = keepDuration;
        uiMessageBox.textMeshProUGUI.text = msg;
        ViewController
            .OverlayPageChanger()
            .SetPage(ViewSystemScriptable.ViewPages.MessageBubble)
            .SetReplayWhileSamePage(true)
            .OnComplete(
                () =>
                {
                    uiMessageBox.AutoHide();
                }
            )
            .Show();
    }
    public void AutoHide()
    {
        if (hideRunner != null)
        {
            StopCoroutine(hideRunner);
        }
        hideRunner = StartCoroutine(HideRunner());
    }
    Coroutine hideRunner;
    IEnumerator HideRunner()
    {
        float time = keepDuration;
        while (time > 0)
        {
            time -= GlobalTimer.deltaTime;
            yield return null;
        }
        Hide();
        hideRunner = null;
    }

    public static void Hide()
    {
        if (!ViewController.Instance.IsOverPageLive(ViewSystemScriptable.ViewPages.MessageBubble))
        {
            Debug.Log("uimessagebubble return ");
            return;
        }
        ViewController
            .OverlayPageChanger()
            .SetPage(ViewSystemScriptable.ViewPages.MessageBubble)
            .Leave();
    }

    public TriPosition showTriPosition;
    public Vector3 showPosition;
    public float objectOffect;
    public float extraOffect;
    public float showDuration;
    public float hideDuration;
    public float keepDuration;

    public static Vector2 RectTransformToScreenSpace(RectTransform transform)
    {
        //Vector2 size = Vector2.Scale(transform.rect.size, transform.lossyScale);
        float x = transform.position.x + transform.anchoredPosition.x;
        float y = Screen.height - transform.position.y - transform.anchoredPosition.y;

        return new Vector2(x, y);
    }
    public void OnShow(System.Action OnComplete)
    {
        // throw new System.NotImplementedException();
        transform.position = showPosition;
        Vector2 screenPos = new Vector2((transform as RectTransform).anchoredPosition.x, (transform as RectTransform).anchoredPosition.y + objectOffect + extraOffect);
        (transform as RectTransform).anchoredPosition = screenPos;

        // RectTransformUtility.WorldToScreenPoint(Camera.main, transform.position);
        // RectTransformUtility.ScreenPointToWorldPointInRectangle(
        //     transform.parent as RectTransform,
        //     RectTransformToScreenSpace((transform as RectTransform)),
        //     null,
        //     out position
        // );
        //transform.position = position;

        Reset();
        StartCoroutine(CalculatePosition(showTriPosition));
        StartCoroutine(AnimShow());
    }

    public void OnLeave(System.Action OnComplete)
    {
        StartCoroutine(AnimHide(OnComplete));
    }
    IEnumerator AnimHide(System.Action OnComplete)
    {
        canvasGroup.alpha = 0;
        // yield return canvasGroup.Ease(0, 0.2f).WaitForCompletion();
        OnComplete?.Invoke();
        yield break;
    }
    IEnumerator AnimShow()
    {
        //Show
        yield return EaseUtility.To(Vector3.zero, Vector3.one, showDuration, EaseStyle.BackEaseOut, (v) => rectTransform.localScale = v);
    }


    void Reset()
    {
        rectTransform.localScale = Vector3.zero;
        canvasGroup.alpha = 0;
    }
    public enum TriPosition
    {
        Up = 0, Down = 1
    }
    // Bubble on top : Y 51
    // Bubble on bottom : Y -51
    const float padding = 50;

    IEnumerator CalculatePosition(TriPosition triPosition)
    {
        UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
        //Wait the end of frame which layout is finished calculate  
        yield return null;
        yield return Yielders.EndOfFrame;
        Vector3 worldPoint;
        bubbleObject.SetAnchor(AnchorPresets.MiddleCenter);
        bubbleObject.SetPivot(PivotPresets.MiddleCenter);
        RectTransformUtility.ScreenPointToWorldPointInRectangle(triObject, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), null, out worldPoint);
        if (triPosition == TriPosition.Up)
        {
            //Set tri to up
            flippable.vertical = false;
            triObject.anchoredPosition = new Vector2(0, 9);

            bubbleObject.position = new Vector3(worldPoint.x, bubbleObject.position.y, bubbleObject.position.z);
            bubbleObject.SetAnchor(AnchorPresets.TopCenter);
            bubbleObject.SetPivot(PivotPresets.TopCenter);
            bubbleObject.anchoredPosition = new Vector2(bubbleObject.anchoredPosition.x, -32);
        }
        else
        {
            //Set tri to down
            flippable.vertical = true;
            triObject.anchoredPosition = new Vector2(0, -9);

            bubbleObject.position = new Vector3(worldPoint.x, bubbleObject.position.y, bubbleObject.position.z);
            bubbleObject.SetAnchor(AnchorPresets.BottonCenter);
            bubbleObject.SetPivot(PivotPresets.BottomCenter);
            bubbleObject.anchoredPosition = new Vector2(bubbleObject.anchoredPosition.x, 32);
        }
        float halfOfBubbleWidth = bubbleObject.rect.width * 0.5f;

        //Debug.Log(halfOfBubbleWidth);
        float farFromTri = Mathf.Abs(bubbleObject.anchoredPosition.x);
        float finalX = 0;
        if (farFromTri > halfOfBubbleWidth)
        {
            float offect = farFromTri - halfOfBubbleWidth;
            if (bubbleObject.anchoredPosition.x > 0)
            {
                finalX = bubbleObject.anchoredPosition.x - offect - padding;
            }
            else
            {
                finalX = bubbleObject.anchoredPosition.x + offect + padding;
            }
        }
        bubbleObject.anchoredPosition = new Vector2(finalX, bubbleObject.anchoredPosition.y);
    }

}
