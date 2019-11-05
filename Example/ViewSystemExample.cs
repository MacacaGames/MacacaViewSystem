using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CloudMacaca.ViewSystem;

public class ViewSystemExample : MonoBehaviour
{
    /// <summary>
    /// Use PageChanger Method chain to control ViewController.
    /// Call Show() method to Start the page change.
    /// </summary>
    public void abc()
    {
        ViewControllerV2
            .Changer()
            .OnStart(() => { Debug.Log("Start" + Time.time); })
            .OnComplete(() => { Debug.Log("Complete" + Time.time); })
            .SetPage(ViewSystemScriptable.ViewPages.Setting)
            .Show();

    }

    /// <summary>
    ///  Method chain also support YieldInstruction, so you can yield return it inside a IEnumerator or something else.
    /// </summary>
    public void abc2()
    {
        StartCoroutine(tttt());
    }
    IEnumerator tttt()
    {
        Debug.Log("IEnumerator Start" + Time.time);
        yield return ViewControllerV2
                    .Changer()
                    .OnStart(() => { Debug.Log("tttt Start" + Time.time); })
                    .OnComplete(() => { Debug.Log("tttt Complete" + Time.time); })
                    .SetPage(ViewSystemScriptable.ViewPages.Welcome)
                    .Show()
                    .GetYieldInstruction();
        Debug.Log("IEnumerator End" + Time.time);
        
    }

    /// <summary>
    /// GetYieldInstruction will call Show method inside itself, so you can omission Show() method on the method chain.
    /// </summary>
    public void abc3()
    {
        StartCoroutine(tttt2());
    }
    IEnumerator tttt2()
    {
        Debug.Log("IEnumerator Start" + Time.time);
        yield return ViewControllerV2
                    .Changer()
                    .OnStart(() => { Debug.Log("tttt Start" + Time.time); })
                    .OnComplete(() => { Debug.Log("tttt Complete" + Time.time); })
                    .SetPage(ViewSystemScriptable.ViewPages.Welcome)
                    .GetYieldInstruction();
        Debug.Log("IEnumerator End" + Time.time);
    }

}
