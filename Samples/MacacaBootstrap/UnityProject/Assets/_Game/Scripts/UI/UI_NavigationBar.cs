using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MacacaGames.ViewSystem;
using MacacaGames;

public class UI_NavigationBar : ViewElementLifeCycle, IViewElementInjectable
{
    [SerializeField] RectTransform navBgLRect;
    [SerializeField] RectTransform navBgRRect;
    [SerializeField] RectTransform navBgCenterRect;
    [SerializeField] public GameObject NavBtnRoot;
    [SerializeField]
    List<UI_NavBtn> NavBtns = new List<UI_NavBtn>();
    [SerializeField]
    Color IconColorSelected = new Color32(52, 98, 110, 255);
    float navWidth;
    [ReadOnly] public int currentIndex = 0;

    void Awake()
    {
        for (int i = 0; i < NavBtns.Count; i++)
        {
            NavBtns[i].Init(i);
        }
    }


    const string Key_EventHint = "Key_EventHint";
    const string Key_StoreHint = "Key_StoreHint";
   

    float navTargetPosition;
    float leftTargetWidth;
    float rightTargetWidth;
    [SerializeField]
    float NavIconYSelected = 2f;
    [SerializeField]
    float NavIconYNormal = -42f;

    public void MoveBackground(int index, float time = 0.4f)
    {
        // UI_NavItemSlide.Instance.MoveItem(index);
        currentIndex = index;

        // Color
        foreach (var item in NavBtns)
        {
           
            item.RefreshView();
        }
        for (int i = 0; i < NavBtns.Count; i++)
        {
            NavBtns[i].ImageCircle.rectTransform.localScale = (i == index ? Vector3.one : Vector3.zero);
            NavBtns[i].ImageCircle.rectTransform.anchoredPosition = new Vector2(NavBtns[i].ImageCircle.rectTransform.anchoredPosition.x, (i == index ? NavIconYSelected : NavIconYNormal));
            NavBtns[i].Image.rectTransform.anchoredPosition = new Vector2(NavBtns[i].Image.rectTransform.anchoredPosition.x, (i == index ? NavIconYSelected : NavIconYNormal));
            // moveSequence.Join(NavBtns[i].ImageCircle.rectTransform.DOScale((i == index ? Vector3.one : Vector3.zero), time));
            // moveSequence.Join(NavBtns[i].ImageCircle.rectTransform.DOAnchorPosY((i == index ? NavIconYSelected : NavIconYNormal), time));
            // moveSequence.Join(NavBtns[i].Image.rectTransform.DOAnchorPosY((i == index ? NavIconYSelected : NavIconYNormal), time));
        }
        NavBtns[index].Image.color = IconColorSelected;
        NavBtns[index].Text.alpha = 0;
        // moveSequence.Join(NavBtns[index].Image.DOColor(IconColorSelected, time));
        // moveSequence.Join(NavBtns[index].Text.DOFade(0f, time));
    }

    float previewAmount = 0;

    public void Preview(float progress)
    {
        previewAmount = progress;
    }

    void Update()
    {
        navTargetPosition = NavBtns[currentIndex].rectTransform.anchoredPosition.x - navBgCenterRect.sizeDelta.x * 0.5f;

        Vector2 navTargetPos = new Vector2(navTargetPosition + previewAmount, navBgCenterRect.anchoredPosition.y);
        if (Input.GetMouseButton(0))
        {
            navBgCenterRect.anchoredPosition = navTargetPos;
        }
        else
        {
            navBgCenterRect.anchoredPosition = Vector2.Lerp(
                navBgCenterRect.anchoredPosition,
                navTargetPos,
                1 - Mathf.Pow(.85f, GlobalTimer.deltaTime * 60));
        }

        CalculateTargetValue();

        navBgLRect.sizeDelta = new Vector2(leftTargetWidth, navBgLRect.sizeDelta.y);

        navBgRRect.sizeDelta = new Vector2(rightTargetWidth, navBgRRect.sizeDelta.y);
    }

    void CalculateTargetValue()
    {
        leftTargetWidth = navBgCenterRect.anchoredPosition.x + 4;
        rightTargetWidth = navWidth - navBgCenterRect.anchoredPosition.x - navBgCenterRect.sizeDelta.x + 4;
    }

  
    public override void OnStartShow()
    {
        float time = 0.4f;
        for (int i = 0; i < NavBtns.Count; i++)
        {
            NavBtns[i].ImageCircle.rectTransform.localScale = (i == currentIndex ? Vector3.one : Vector3.zero);
            NavBtns[i].ImageCircle.rectTransform.anchoredPosition = new Vector2(NavBtns[i].ImageCircle.rectTransform.anchoredPosition.x, (i == currentIndex ? NavIconYSelected : NavIconYNormal));
            NavBtns[i].Image.rectTransform.anchoredPosition = new Vector2(NavBtns[i].Image.rectTransform.anchoredPosition.x, (i == currentIndex ? NavIconYSelected : NavIconYNormal));
            // moveSequence.Join(NavBtns[i].ImageCircle.rectTransform.DOScale((i == currentIndex ? Vector3.one : Vector3.zero), time));
            // moveSequence.Join(NavBtns[i].ImageCircle.rectTransform.DOAnchorPosY((i == currentIndex ? NavIconYSelected : NavIconYNormal), time));
            // moveSequence.Join(NavBtns[i].Image.rectTransform.DOAnchorPosY((i == currentIndex ? NavIconYSelected : NavIconYNormal), time));
        }
        NavBtns[currentIndex].Image.color = IconColorSelected;
        NavBtns[currentIndex].Text.alpha = 0;
        // moveSequence.Join(NavBtns[currentIndex].Image.DOColor(IconColorSelected, time));
        // moveSequence.Join(NavBtns[currentIndex].Text.DOFade(0f, time));
    }

    public static void SetBottom(RectTransform rt, float bottom)
    {
        rt.offsetMin = new Vector2(rt.offsetMin.x, bottom);
    }

    void Start()
    {

        // TutorialManager.Instance.OnTutorialFlagChanged += (t) => { StartCoroutine(RefreshButton()); };
    }


    static string[] navPageNames
    {
        get
        {
            return new string[]
            {
                ViewSystemScriptable.ViewPages.Welcome_P1,
                ViewSystemScriptable.ViewPages.Welcome_P2,
                ViewSystemScriptable.ViewPages.Welcome_P3,
            };
        }
    }

    

    public static string[] leaveIfNavigationChange = new string[]
    {
       
    };

    public static string[] leaveStateIfNavigationChange = new string[]
    {
      
    };

    public void TransitionUI(string page)
    {
        CheckPageBeforeNavProccess();
        ViewController
            .FullPageChanger()
            .SetPage(page)
            .SetWaitPreviousPageFinish(true)
            .Show();

        void CheckPageBeforeNavProccess()
        {
            foreach (var item in leaveStateIfNavigationChange)
            {
                if (ViewController.Instance.IsOverPageStateLive(item, out string viewpage))
                {
                    ViewController
                        .OverlayPageChanger()
                        .SetPage(viewpage)
                        .Leave();
                }
            }

            foreach (var item in leaveIfNavigationChange)
            {
                if (ViewController.Instance.IsOverPageLive(item))
                {
                    ViewController
                        .OverlayPageChanger()
                        .SetPage(item)
                        .Leave();
                }
            }
        }
    }


    /// <summary>
    /// return if go to page succuss
    /// </summary>
    /// <param name="page"></param>
    /// <returns></returns>
    public bool GoToPage(int page, string pageName)
    {
        if (page < 0 || page >= NavBtns.Count)
        {
            return false;
        }

        if (page == currentIndex)
        {
            NavigationActionWhileSamePage(currentIndex);
            return false;
        }

        if (ViewController.Instance.IsPageTransition)
        {
            return false;
        }

      
        TransitionUI(pageName);
        MoveBackground(page);

        return true;
    }

    public bool GoToPage(int page)
    {
        return GoToPage(page, navPageNames[page]);
    }

    void NavigationActionWhileSamePage(int index)
    {
        string pageName = navPageNames[index];
        var currentPage = ViewController.Instance.currentViewPage;

      
    }

  
}