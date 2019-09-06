using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IViewElementLifeCycle
{
    void OnBeforeShow();
    void OnBeforeLeave();
    void OnStartShow();
    void OnStartLeave();
}