using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MacacaGames.GameSystem;
using MacacaGames.ViewSystem;
using System.Threading.Tasks;

public class UIManager : MonoBehaviourLifeCycle
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

    public override async Task Init()
    {
    }
}
