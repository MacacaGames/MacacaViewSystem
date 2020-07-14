using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IViewElementLifeCycle
{
    /// <summary>
    /// While the ViewElement prepare to Show
    /// </summary>
    void OnBeforeShow();
    /// <summary>
    /// While the ViewElement prepare to Leave
    /// </summary>
    void OnBeforeLeave();
    /// <summary>
    /// While the ViewElement start to Show
    /// </summary>
    void OnStartShow();
    /// <summary>
    /// While the ViewElement start to Leave
    /// </summary>
    void OnStartLeave();
    /// <summary>
    /// While the ChangePage method on ViewElement has been called.
    /// </summary>
    void OnChangePage(bool show);
    /// <summary>
    /// While the ViewPage is Changed on ViewController.
    /// </summary>
    void OnChangedPage();
}