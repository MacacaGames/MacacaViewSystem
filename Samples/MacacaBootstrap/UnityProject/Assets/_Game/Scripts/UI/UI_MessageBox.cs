using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MacacaGames.ViewSystem;
using System;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MacacaGames;
using TMPro;

public class UI_MessageBox : MonoBehaviour, IViewElementInjectable, IViewElementLifeCycle
{
    [SerializeField]
    Image image;

    [SerializeField]
    ViewElement btnTemplate;

    [SerializeField]
    Transform btnHroizonContainer;
    [SerializeField]
    Transform btnVerticalContainer;

    [SerializeField]
    TextMeshProUGUI titleText;
    [SerializeField]
    TextMeshProUGUI contentText;

    [SerializeField]
    SelectablePalette[] btnPalette;
    public void SetText(string _title, string _content)
    {
        titleText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(_title));
        titleText.SetText(_title);
        contentText.SetText(_content);
    }

    ViewElementRequestedPool viewElementPool;

    bool isInit = false;
    public void Init()
    {
        viewElementPool = new ViewElementRequestedPool(btnTemplate);
        viewElementPool.recoveryAction = ve =>
        {
            var btn = ve.GetComponent<ReferenceLinker>().Get<Button>("Button");
            btn.onClick.RemoveAllListeners();
        };

        isInit = true;
    }
    IEnumerable<BtnWrapper> btns;
    public enum BtnLayout
    {
        Hide,
        Horizon,
        Vertical
    }
    public enum BtnColor
    {
        Primary = 0,
        Secondary = 1,
        Success = 2,
        Danger = 3,
        Warning = 4,
    }
    BtnLayout layout = BtnLayout.Hide;
    bool closeOnAnyBtnClick = true;
    public static float autoLeaveTime = 5;
    Action OnAutoClose;

    public struct BtnWrapper
    {
        public string text;
        public Action OnClick;
        public BtnColor color;
        public bool interactable;
    }

    /// <summary>
    /// Show a message box, aka dialog
    /// </summary>
    /// <param name="title">the title of the box</param>
    /// <param name="content">the content of the box</param>
    /// <param name="btns">Set up the btns in your Message box</param>
    /// <param name="sprite">Set the icon to show on the messagebox, set to null if you don't want to show icon</param>
    /// <param name="layuot">Set button layout, horizontal or vertical</param>
    /// <param name="OnAutoClose">Does the message box should automatically count down close, and the behavour while it automaticall closed; set to null to not to use the auto count down close feature</param>
    /// <param name="CloseOnAnyBtnClick">Automatically close the message box or not while any btn is clicked, true to automatically close</param>
    /// <returns>The show progress while messagebox is showing</returns>
    public static CustomYieldInstruction Show(string title, string content, IEnumerable<BtnWrapper> btns, Sprite sprite = null, BtnLayout layuot = BtnLayout.Horizon, Action OnAutoClose = null, bool CloseOnAnyBtnClick = true)
    {
        UI_MessageBox uiMessageBox = ViewController.Instance.GetInjectionInstance<UI_MessageBox>();

        if (uiMessageBox.isInit == false)
            uiMessageBox.Init();

        uiMessageBox.SetText(title, content);
        uiMessageBox.btns = btns;
        uiMessageBox.layout = layuot;
        uiMessageBox.closeOnAnyBtnClick = CloseOnAnyBtnClick;
        uiMessageBox.OnAutoClose = OnAutoClose;
        uiMessageBox.image.gameObject.SetActive(sprite != null);
        uiMessageBox.image.sprite = sprite;
        // return ViewController.Instance.ShowOverlayViewPage(ViewSystemScriptable.ViewPages.MessageBox, true);
        return ViewController.OverlayPageChanger().SetPage(ViewSystemScriptable.ViewPages.MessageBox).SetWaitPreviousPageFinish(true).Show(true);
    }

    public static void Close()
    {
        ViewController.Instance.LeaveOverlayViewPage(ViewSystemScriptable.ViewPages.MessageBox);
    }

    public void OnBeforeShow()
    {
    }

    [SerializeField]
    string autoCloseTextTerm;
    Coroutine AutoCloseCoroutine;
    IEnumerator AutoClose(TextMeshProUGUI text, Button closeBtn)
    {
        float time = autoLeaveTime;
        text.SetText(autoCloseTextTerm);
        
        while (time > 0)
        {
            time -= MacacaGames.GlobalTimer.deltaTime;
            yield return null;
        }

        yield return null;
        yield return null;

        closeBtn.OnSubmit(null);
        AutoCloseCoroutine = null;
    }


    public void OnBeforeLeave()
    {// throw new NotImplementedException();
        viewElementPool.RecoveryAll(false);

    }
    List<ReferenceLinker> btnLinkers = new List<ReferenceLinker>();
    public void OnStartShow()
    {
        btnLinkers.Clear();
        if (AutoCloseCoroutine != null)
            StopCoroutine(AutoCloseCoroutine);

        Transform container = null;

        if (layout == BtnLayout.Horizon)
        {
            btnHroizonContainer.gameObject.SetActive(true);
            container = btnHroizonContainer;
        }
        else
        {
            btnHroizonContainer.gameObject.SetActive(false);
            container = btnVerticalContainer;
        }

        foreach (var item in btns)
        {
            ViewElement ve = viewElementPool.Request(container);
            ReferenceLinker referenceLinker = ve.GetComponent<ReferenceLinker>();
            referenceLinker.Get<TextMeshProUGUI>("Text").SetText(item.text);
            var btn = referenceLinker.Get<Button>("Button");
            btn.interactable = item.interactable;
            btn.onClick.AddListener(() =>
            {
                item.OnClick?.Invoke();
                if (closeOnAnyBtnClick) Close();
            });
            btnLinkers.Add(referenceLinker);

            var palette = ve.GetComponent<PaletteControl>();
            int p = ((int)item.color) < btnPalette.Length ? ((int)item.color) : 0;
            palette.SetPalette(btnPalette[p]);

            ve.OnShow();
        }

        if (OnAutoClose != null)
        {
            ViewElement ve = viewElementPool.Request(container);
            ReferenceLinker referenceLinker = ve.GetComponent<ReferenceLinker>();
            var text = referenceLinker.Get<TextMeshProUGUI>("Text");
            var btn = referenceLinker.Get<Button>("Button");

            btn.onClick.AddListener(() =>
            {
                OnAutoClose?.Invoke();
                if (closeOnAnyBtnClick) Close();
            });

            AutoCloseCoroutine = StartCoroutine(AutoClose(text, btn));
            btnLinkers.Add(referenceLinker);

            ve.OnShow();
        }

        LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
    }

    public void OnStartLeave()
    {//
     // throw new NotImplementedException();
    }

    public void OnChangePage(bool show)
    {
        // throw new NotImplementedException();
    }
    public void OnChangedPage()
    {

    }

    public void RefreshView()
    {
        
    }

#if (UNITY_EDITOR)

    public Button GetButton(int index)
    {
        var btn = btnLinkers[index].Get<Button>("Button");
        return btn;
    }

#endif

}
