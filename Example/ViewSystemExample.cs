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
    [Sirenix.OdinInspector.Button]
    public void abc()
    {
        Debug.Log("Start" + Time.time);
        ViewControllerV2.Instance.ChangePage("Language", () => { Debug.Log("Changed" + Time.time); }, () => { Debug.Log("Complete" + Time.time); });
    }


    /// <summary>
    /// Use PageChanger Method chain to control ViewController.
    /// Call Show() method to Start the page change.
    /// </summary>
    [Sirenix.OdinInspector.Button]
    public void abc1()
    {
        ViewControllerV2
            .FullPageChanger()
            .OnStart(() => { Debug.Log("Start " + Time.time); })
            .OnChanged(() => { Debug.Log("Changed " + Time.time); })
            .OnComplete(() => { Debug.Log("Complete " + Time.time); })
            .SetPage("Language")
            .Show();

    }

    /// <summary>
    ///  Method chain also support YieldInstruction, so you can yield return it inside a IEnumerator or something else.
    /// </summary>
    [Sirenix.OdinInspector.Button]
    public void abc2()
    {
        StartCoroutine(tttt());
    }
    IEnumerator tttt()
    {
        Debug.Log("IEnumerator Start" + Time.time);
        yield return ViewControllerV2
                    .FullPageChanger()
                    .OnStart(() => { Debug.Log("tttt Start " + Time.time); })
                    .OnChanged(() => { Debug.Log("tttt Changed " + Time.time); })
                    .OnComplete(() => { Debug.Log("tttt Complete " + Time.time); })
                    .SetPage("Welcome")
                    .Show()
                    .GetYieldInstruction();
        Debug.Log("IEnumerator End" + Time.time);

    }

    /// <summary>
    /// GetYieldInstruction will call Show method inside itself, so you can dismiss Show() method on the method chain.
    /// </summary>
    [Sirenix.OdinInspector.Button]
    public void abc3()
    {
        StartCoroutine(tttt2());
    }
    IEnumerator tttt2()
    {
        Debug.Log("IEnumerator Start" + Time.time);
        yield return ViewControllerV2
                    .FullPageChanger()
                    .OnStart(() => { Debug.Log("tttt Start" + Time.time); })
                    .OnComplete(() => { Debug.Log("tttt Complete" + Time.time); })
                    .SetPage("Welcome")
                    .GetYieldInstruction();
        Debug.Log("IEnumerator End" + Time.time);
    }

}
