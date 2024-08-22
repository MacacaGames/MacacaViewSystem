using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MacacaGames.GameSystem;
using MacacaGames.ViewSystem;
using System.Threading.Tasks;
using UnityEngine.Rendering;

public class UIManager : MonoBehaviour
{
    [Inject]
    MainGamePlayData gamePlaydata;

    [ViewSystemEvent]
    public void OnPlayButtonClick(Component c)
    {
        if (ViewController.Instance.IsPageTransition)
        {
            return;
        }
        ApplicationController.Instance.StartGame();
    }

    [ViewSystemEvent]
    public void OnExitGameButtonClick(Component c)
    {
        gamePlaydata.gamePlayController.QuitGamePlay();
    }

    [ViewSystemEvent]
    public void OnGameResultClick(Component c)
    {
        gamePlaydata.ResultComplete();
    }

    [ViewSystemEvent]
    public void OnGameWinBtnClick(Component c)
    {
        gamePlaydata.gamePlayController.SuccessGamePlay();
    }
    
    [ViewSystemEvent]
    public void OnGameLoseBtnClick(Component c)
    {
        gamePlaydata.gamePlayController.FailedGamePlay();
    }
    
    [ViewSystemEvent("OverridePropertyPage")]
    public void OnOverridePropertyPageBtnPressed(Component c)
    {
        var messageBoxCoroutine = UI_MessageBox.Show("Title", "call a function with ViewSystemEvent attribute in UIManager",
            new UI_MessageBox.BtnWrapper[]{
                new UI_MessageBox.BtnWrapper{
                    text = "Confirm",
                    OnClick = ()=>{ },
                    color = UI_MessageBox.BtnColor.Success,
                    interactable = true
                }
            }
        );
    }
}
