using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CloudMacaca.ViewSystem;
public class ViewStateBehaviour : StateMachineBehaviour
{
    static Dictionary<int, ViewElement> veDict = new Dictionary<int, ViewElement>();
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if(animator.IsInTransition(0) || !stateInfo.IsName("Disable"))
            return;

        ViewElement viewElement;
        if (!veDict.TryGetValue(animator.gameObject.GetInstanceID(), out viewElement))
        {
            viewElement = animator.GetComponentInParent<ViewElement>();
            veDict.Add(animator.gameObject.GetInstanceID(),viewElement);
        }

        viewElement.OnLeaveAnimationFinish();
    }
    // override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    // {
    // }
    // override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    // {
    // }
    // override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    // {
    // }
    // override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    // {
    // }
}
