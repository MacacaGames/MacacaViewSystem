using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MacacaGames.GameSystem;
using MacacaGames.ViewSystem;
using TMPro;
using Rayark.Mast;
using System.Threading.Tasks;

[CreateAssetMenu(menuName = "GameSystem/MainGamePlayData")]
public class MainGamePlayData : ScriptableObjectGamePlayData
{
    public override bool IsContinueAvailable => true;

    public override async Task Init()
    {

    }

    public override async void OnEnterLobby()
    {
        ViewController
            .FullPageChanger()
            .SetPage(ViewSystemScriptable.ViewPages.Welcome_P1)
            .Show();
    }

    public override void OnGameValueReset()
    {
        resultEnd = false;
    }

    public override async Task OnEnterGame()
    {
        ViewController
            .FullPageChanger()
            .SetPage(ViewSystemScriptable.ViewPages.Play)
            .Show(true);
    }

    public override IEnumerator GamePlay()
    {
        while (true)
        {
            yield return null;

        }

    }

    public override void OnLeaveGame()
    {

    }

    public override void OnGameSuccess()
    {

    }

    public override void OnGameLose()
    {

    }

    private bool resultEnd = false;
    public override async Task GameResult()
    {
        ViewController
            .OverlayPageChanger()
            .SetPage(ViewSystemScriptable.ViewPages.Loading)
            .Show();

        ViewController
            .FullPageChanger()
            .SetPage(ViewSystemScriptable.ViewPages.Result)
            .Show(true);

        //Rayark.Mast.Coroutine.Sleep(1f);
        await Task.Delay(1000);

        ViewController
            .OverlayPageChanger()
            .SetPage(ViewSystemScriptable.ViewPages.Loading)
            .Leave();

        var gameStatus = "";
        var message = ViewController.Instance.GetViewPageElementByName(ViewSystemScriptable.ViewPages.Result, "Message");
        var text = message.GetComponentInChildren<TextMeshProUGUI>();

        gameStatus += "isFailed: " + gamePlayController.isFailed.ToString() + "\n";


        text.text = gameStatus;
        await TaskUtils.WaitUntil(() => resultEnd);
    }

    public void ResultComplete()
    {
        resultEnd = true;
        System.GC.Collect();
    }

    public override async void OnGameEnd()
    {
    }

    public override async Task OnGameFaild()
    {
    }

    public static bool? isContinueResult = null;
    public override async Task<bool> OnContinueFlow()
    {
        isContinueResult = null;
        var messageBoxCoroutine = UI_MessageBox.Show("Title", "Continue?",
            new UI_MessageBox.BtnWrapper[]{
                new UI_MessageBox.BtnWrapper{
                    text = "Yes",
                    OnClick = ()=>{
                        isContinueResult = true;
                    },
                    color = UI_MessageBox.BtnColor.Success,
                    interactable = true
                },
                new UI_MessageBox.BtnWrapper{
                    text = "No",
                    OnClick = ()=>{
                        isContinueResult = false;
                    },
                    color = UI_MessageBox.BtnColor.Warning,
                    interactable = true
                }
            },
            layuot: UI_MessageBox.BtnLayout.Vertical,
            OnAutoClose: () =>
            {
                isContinueResult = false;
            }
        );
        await TaskUtils.WaitUntil(() => isContinueResult != null);
        return isContinueResult.Value;

    }

    public override void OnContinue()
    {

    }

    public override void OnGUI()
    {
    }

    public override void OnChangeGamePlayData_Launch()
    {
    }

    public override void OnChangeGamePlayData_Retire()
    {
    }
}
