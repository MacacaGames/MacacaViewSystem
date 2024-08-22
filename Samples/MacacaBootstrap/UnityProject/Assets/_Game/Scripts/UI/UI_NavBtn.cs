using System;
using System.Collections;
using System.Collections.Generic;
using MacacaGames.ViewSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_NavBtn : ViewElementLifeCycle
{
    public int Index;
    public RectTransform rectTransform;
    public Image Image;
    public Image ImageCircle;
    public TextMeshProUGUI Text;
    public Button Button;


    public void Init(int index)
    {
        this.Index = index;
        Button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
    
        UI_NavigationBar navigationBar = ViewController.Instance.GetInjectionInstance<UI_NavigationBar>();
        navigationBar.GoToPage(Index);
     
    }

    Color activeColor = Color.white;
    public void RefreshView()
    {
        // if (!isUnlockCache)
        //     activeColor = new Color(.2f, .2f, .2f, .4f);
        Image.color = activeColor;
        Text.color = activeColor;
    }
    public override void OnStartShow()
    {
        base.OnStartShow();
        RefreshView();
    }
}
