See [Document](https://macacagames.github.io/MacacaViewSystem/) for more detail.



# Overview
**ViewSystem** is a element based UI management system based on Unity GUI. It is developed and used by Macaca Games.
UI management in Unity3D is always a hard work, the goal of **ViewSystem** is to make UI management more easier and flexable.

## Feature
- Element based UI manager system
- UI Element pooling system
- Property and Event override in runtime
- Node based editor
  
# Concept
## ViewElement
ViewElement is the base unit in ViewSystem, any item shows on an UI page can be a ViewElement, such as a button, a icon or anything else.

For example, the red square part in the screenshot is a ViewElement.
<img src="./Img~/viewelement.png" alt="Screenshot2" height="400"/>

And the most important thing, ViewElement only focus how it will show or leave and **doesn't** care where it will be placed.

Currently there is 5 method to transition while we try to showing or leaving a ViewElement: **Animator**, **CanvasGroup Fade**, **Active Switch**, **ViewElement Animation**, **Custom**.

### ViewElement Animation

ViewElementAnimation is a simple tool helps to making a Animation on a ViewElement, it can control the Transfomr(pos, rot, scale) and the CanvasGroup(alpha) with Tween animation.

<img src="./Img~/viewelement_animation.png" height="700"/>

## ViewPage
ViewPage compose with one or more ViewElements and define where the ViewElements should be placed. Base on it's default behaviour there is two kind of ViewPage : FullPage, OverlayPage.

### FullPage
The basic type of ViewPage, the system only allow one FullPage shows on the screen at the same time.
When the ChangePage action is fire, system will leave all ViewElements defined in last FullPage and then show the ViewElements defined in next FullPage.

### OverlayPage
Sometimes we may wants an UI page shows and covered current screen, the OverlayPage can helps to implement the feature.

This is very useful when making Dialog, LoadingView ect.

OverlayPage allow to shows more than one page in the same time, each OverlayPage maintain it's own lifecycle.

> Currently, different OverlayPage can be show in the same time, but the same OverlayPage still has only one instance the duplicate call to shows an OverlayPage which is already on the screen is not allowed and the call will be ignore, but if you wish to replay the show animation in the ViewElement you can set the parameter **ReShowWhileSamePage** to True while calling the ShowOverlayPage API.

## ViewState
ViewState is similar as ViewPage, ViewState can define the duplicate part in two or more ViewPage.

Each ViewPage can setup at most one ViewState.

And also the ViewElements define in ViewState will not be update until the ViewState is changed.

## ViewController
ViewController is the core component of ViewSystem, all control of the UI is base on this component.

# Installation

### Option 1: Installation via OpenUPM (Recommend)

```sh
openupm add com.macacagames.viewsystem
```

## Option 2: Unity Package file
Add it to your editor's manifest.json file like this:
```json
{
    "dependencies": {
        "com.macacagames.utility": "https://github.com/MacacaGames/MacacaUtility.git",
        "com.macacagames.viewsystem": "https://github.com/MacacaGames/MacacaViewSystem.git"
    }
}
```

## Option 3: Git SubModule
```bash
git submodule add https://github.com/MacacaGames/MacacaViewSystem.git Assets/MacacaViewSystem
```
Note: ViewSystem is dependency with MacacaUtility, so also add MacacaUtility in git submodule.
```bash
git submodule add hhttps://github.com/MacacaGames/MacacaUtility.git Assets/MacacaUtility
```

# Setup
## 1. Editor
Menu Path : MacacaGames > ViewSystem > Visual Editor

ViewSystem will create required data and save under Assets/ViewSystemResources folder.

## 2. Create ViewController
In the Scene which you wish to add UI, create a new GameObject and attach ViewControll Component, then drag ViewSystemData to component.
<img src="./Img~/how_to_1.png" width="600"/>

## 3. Create UGUI Canvas
Click ``GlobalSetting`` button on toolbar.

- Click the ``Generate default UI Root Object`` button to automatically generate your first UI root.
- Set ViewController gameObject name to ``View Controller GameObject`` field. (As the screenshot is ``UI``)
- Remember click ``Save`` button on toolbar after all step is done
<img src="./Img~/how_to_2.png" width="600"/>

## 4. Ready to go!
Now, all setup step is done, use Example Project to learn how to edit your UI.

# Usage
Use the Visual Editor to edit your UI page.

Menu Path : MacacaGames > ViewSystem > Visual Editor
## Make a ViewPage
You can define the ViewElement and and its RectTransform info by Visual Editor.
<img src="./Img~/add_viewelement.gif" />

### Position a ViewElement on ViewPage

There are 2 different ways to define the position of a ViewElement on a ViewPage: RectTransform or CustomParent.
- RectTransform

On the Visual Editor, as the gif you can define the RectTransform information of a ViewElement just like modifing the RectTransform Component.
When you're previewing a ViewPage, all modify on the RectTransform information will automatically update to the previewing Screen.

- Custom Parent

Another way to position your ViewElement is using Custom Parent mode, in Custom Parent mode the system will find the Transform object which you define and set to that Object's child.

The Custom Parent mode is despreded, we only recommend using this mode in special situation such as you would like to put a ViewElement as another ViewElement's child in the runtime.


## Override property on a ViewElement
You can override any property on ViewElement, use preview to take effect the override.
With the override system, you can simply create the ViewElement variant in different ViewPage.
<img src="./Img~/override_demo.gif" />

### Why using ViewSystem's override but not Unity Prefab variant?
ViewSystem override is a runtime function, it means all modify only exsit during the Game is runing, use the ViewSystem override helps you to avoid to make a lot of Prefab variant assets.

Limitation, the ViewSystem override has no ability to add/remove Component, GameObject etc. In this case use Unity Prefab variant.

## Override UnityEvent on a ViewElement
The override system also support to bind UnityEvent on an UGUI selectable.

Make a method with Component parameter and attact ``ViewSystemEvent`` attribute on it, the method will show on up the override window.
<img src="./Img~/event_demo.gif" />

Example: (In UIManager.cs)
```csharp
[MacacaGames.ViewSystem.ViewSystemEvent]
public void MyEvent(Component selectable)
{
    //Do something
}
```

## Override Property or Button.onClick on a ViewElement via script in a ViewElementBehaviour
You can override a property via Attribute in a script, take this example, this means override the `sprite` property on `UnityEngine.UI.Image` component on a child GameObject which name is `Frame` by the value of `someSprite` variable.

```csharp
// Is require a child class of ViewElementBehaviour
public class MyUILogic : ViewElementBehaviour{
    [OverrideProperty("Frame", typeof(UnityEngine.UI.Image), nameof(UnityEngine.UI.Image.sprite)) ]
    [SerializeField]
    Sprite someSprite;

    [OverrideButtonEvent("TopRect/Button")]
    void Test(Component component)
    {
        Debug.Log("success");
    }
}
```

## Safe Area
ViewSystem support Safe Area adjustment on the screen.
Each can setup its owned Safe Area setting, or using the Safe Area global setting, the Safe Area support is modified from [5argon/NotchSolution](https://github.com/5argon/NotchSolution) and with deep intergation with ViewSystem, thanks for his/her great works!


<img src="./Img~/safearea_page.png" />
<img src="./Img~/safearea_global.png" />

## Page Ordering

Since the ViewSystem allow have more than one `Overlay ViewPage` in the same time, the `Overlay ViewPage` may covering eachother, therefore you need to maintain ording of the `Overlay ViewPage`s your self, to do so using the `Overlay order` tools to helps your complete the works.

<img src="./Img~/page_ordering.png" />

# Components

## ViewElementGroup
Something we me may wish to use already exsited ViewElement inside another ViewElement, in this way the ViewElementGroup can helps.
ViewElementGroup works a little like CanvasGroup, if the ViewElement has ViewElementGroup attached,the OnShow/OnLeave intent will also send into the children ViewElement, therefore the whole ViewElement will show/leave correctlly.

As the attach screenshot, the ConfirmBox is a ViewElement and BtnNegitive, BtnPositive is children ViewElement.
<img src="./Img~/viewelementgroup.png" />

There is a **Only Manual Mode** switch on ViewElementGroup, if the swich on, ViewElement will ignore the OnShow/OnLeave intent send by ViewController.
It is helpful while we wish to control the ViewElement show/leave via script.
```csharp
[SerializeField]
ViewElement someViewelement;

// Set the parameter to true to manual show the ViewElement which ViewElementGroup's **Only Manual Mode** is on.
someViewelement.OnShow(true);

// If the ViewElement is child of other ViewElement set the first bool to false to aviod the ViewElement to be pooled while OnLeave.
someViewelement.OnLeave(false, true);
```
<img src="./Img~/viewelementgroup_manual.png" width="400"/>

## ViewMarginFixer (Deprecated, only using in Custom Parent Mode)

> This component is Deprecated, for most of the case you should use the RectTransform mode directly.

ViewElement manage by the ViewSystem will be pooled if is not in use, that means the RectTransfrom's anchor stretch value may be wrong while it is taken out from pool. (cause by the Transfrom.SetParent(true);)

ViewMarginFixer is a helper to solve this issue, which override the anchor stretch value base on the ViewElement life cycle.

<img src="./Img~/viewmarginfixer.png" width="400"/>
<img src="./Img~/transform_anchor.png" width="400"/>

# LifeCycle Hook and Injection

## IViewElementSingleton
The component which inherit **IViewElementSingleton** interface will be created as singleton instance, we call it a ViewElementSingleton.

**ViewElementSingleton** instance is managed by the ViewController and will have only one instance during the runtime, use the **ViewController.Instance.GetInjectionInstance<T>()** API to the runtime instance directlly.

```csharp
public class MyViewElementSingletonSample : MonoBehaviour, IViewElementSingleton
{}

// Use GetInjectionInstance method on ViewController to get the singleton instance of ViewElement.
MyViewElementSingletonSample someInjectableClass = ViewController.Instance.GetInjectionInstance<MyViewElementSingletonSample>();
```

> Note : The ViewElement also needs to swtich the **IsUnique** boolean on to makes IViewElementSingleton works.

## IViewElementLifeCycle
We can hooks the lifecycle on ViewElement by **IViewElementLifeCycle** interface, implemented the interface to get lifecycle callback on ViewElement.
```csharp
void OnBeforeShow();
void OnBeforeLeave();
void OnStartShow();
void OnStartLeave();
void OnChangePage(bool show);
void OnChangedPage();
void RefreshView();
```

### ViewElementBehaviour

The **ViewElementBehaviour** implemented IViewElementLifeCycle and provide more useful feature.
It is useful if we wish to setup callback via inspector with UnityEvents, or inherit the component to overrid the method.

```csharp
public class SomeClass : ViewElementBehaviour
{
    public override void OnBeforeShow()
    {
       // Do something
    }
}
```

> Note : Component implemented **ViewElementBehaviour** needs to attach on ViewElement or its children.

Use the ViewController.Instance.RefreshAll(); to refresh all ViewElement on the screen.

### ViewElementInject (Model Inject)

With ViewElementBehaviour Componment, we can use a powerful feature that help us to sending the data to a Runtime ViewElement we call it **Model Inject**.

See follow example:
```csharp
// The MyUILogic.cs is attach on a ViewElement and this ViewElement is setting on the ViewPage "MyPage"
public class MyUILogic : ViewElementBehaviour
{
    [ViewElementInject]
    int testIntInject;

    [ViewElementInject]
    string testStringInject{get;set;} // also support using property
}

// Call the change page API and use SetPageModel() to set the Model data instance
ViewController.FullPageChanger()
    .SetPage("MyPage")
    .SetPageModel(23456, "my string value")
    .Show();
```
As the result, the value **23456** and **"my string value"** will automatically set into the MyUILogic.cs field(testIntInject in this case) or property(testStringInject in this case) after the ViewElement is showed!



In theory it supports all Types including custom Type
```csharp

public class MyClass{
    public int intValue;
    public bool boolValue;
}
// The MyUILogic.cs is attach on a ViewElement and this ViewElement is setting on the ViewPage "MyPage"
public class MyUILogic : ViewElementBehaviour
{
    [ViewElementInject]
    MyClass testMyClass;

    [ViewElementInject]
    List<string> testStringList{get;set;} // also support using property
}

// Call the change page API and use SetPageModel() to set the Model data instance
ViewController.FullPageChanger()
    .SetPage("MyPage")
    .SetPageModel(
        new MyClass{ intValue = 123, boolValue = false},
        new List<string>{
            "item 1",
            "item 2"
        }
    )
    .Show();
```

### Use with OverrideProperty Attribute

The model inject will complete before the ViewSystem runtime override, so you can combine the usage with the RuntimeOverride!
```csharp

public class MyUILogic : ViewElementBehaviour{
    [ViewElementInject]
    [OverrideProperty("Text", typeof(TextMeshProUGUI), nameof(TextMeshProUGUI.text))]
    string someString; // the value will set into the TextMeshProUGUI.text on the GameObject "Text"
}
```

### Page Model and Shared Model
Until now, all sample use the SetPageModel() API to set the model data, by this way we call it **Page Model**, means those model data only works during the ViewPage lifecycle.

There is another model scope which is call **Shared Model**, the Shared Model is manage by the ViewSystem, by default all **IViewElementSingleton** will become Shared Moedl automatically, which means you can use  [ViewElementInject] to inject them in a ViewElementBehaviour.

See the example:
```csharp
// Define a IViewElementSingleton sample
public class MyViewElementSingletonSample : MonoBehaviour, IViewElementSingleton{}

// The MyUILogic.cs is attach on a ViewElement and this ViewElement is setting on the ViewPage "MyPage"
public class MyUILogic : ViewElementBehaviour
{
    [ViewElementInject]
    MyViewElementSingletonSample myViewElementSingletonSample; // Since MyViewElementSingletonSample is a IViewElementSingleton, we don't need to use SetPageModel(), the system still can complete the value inject;
}
```

Or you can Set the Shared Model to the System use the API, ViewController.Instance.SetSharedMoedl();
See the example:
```csharp

public class MyClass{
    public int intValue;
    public bool boolValue;
}

// The MyUILogic.cs is attach on a ViewElement and this ViewElement is setting on the ViewPage "MyPage"
public class MyUILogic : ViewElementBehaviour
{
    [ViewElementInject]
    MyClass myClass;
}

// Call the ViewController.Instance.SetSharedMoedl(); somewhere before the ChangePage API is called.
/// Set the model data to the System, it will become a Shared Model
/// Each type can only have one value/instance, the system will automatically override the new value if duplicate type is trying to Set
ViewController.Instance.SetSharedMoedl(new MyClass{intValue = 123, boolValue = false});

// Call the change page API this time don't use SetPageModel()
ViewController.FullPageChanger()
    .SetPage("MyPage")
    .Show();
```

As the result, though we don't use SetPageModel() API, the value still injected! Due to the system will automatically fallback to search the **Shared Model**

#### Model Search Scope
There are 4 ways to control the model searching scope, we can use the enum **InjectScope** to control.

The default scope is PageFirst

    InjectScope.PageFirst : Search the value from the PageModel first and then SharedModel
    InjectScope.PageOnly : Search the value from the PageModel only.
    InjectScope.SharedFirst : Search the value from the SharedModel first, and then PageModel, 
    InjectScope.SharedOnly : Search the value from the SharedModel only.

```csharp
// The MyUILogic.cs is attach on a ViewElement and this ViewElement is setting on the ViewPage "MyPage"
public class MyUILogic : ViewElementBehaviour
{
    [ViewElementInject(InjectScope.PageOnly)] // change the search scope
    MyClass myClass;
}
```


# System LifeCycle
## ViewController Initialization
> Here shows the Initialize proccess in ViewController. (Since V1 is dropped.)
1. Finding the UIRoot parent GameObject setup in GlobalSetting.
2. Instantiate UIRoot GameObject setup in GlobalSetting.
3. Generate ViewElementPool instance in scene.
3. Generate ViewElementRuntimePool instance in scene and initialize it.
4. Load ViewPage and ViewState data store in ViewSystemSaveData Object.
5. Pre-generate the ViewElement which has component inherited IViewElementInjectable

## FullPage ChangePage
> Once the ChangePage API is call in ViewController, the event, callback, lifecycle hack excude order. (Same behaviour while using FullPageChanger)

<img src="./Img~/changepage_lifecycle.jpg" alt="Screenshot2" height="800"/>

# How to...
## Get an runtime ViewElement reference in ViewPage/ViewState
If the target is an Unique ViewElement, you get it's instance via implement IViewElementInjectable on one of its component, then using ViewController.Instance.GetInjectionInstance<SomeInjectableClass>() API to get the instance. 
```csharp
// SomeInjectableClass is attach on target ViewElement
public class SomeInjectableClass : MonoBehaviour, IViewElementInjectable
{}

SomeInjectableClass someInjectableClass = ViewController.Instance.GetInjectionInstance<SomeInjectableClass>();
```

Otherwise GetViewPageElementByName or GetViewStateElementByName API to get the runtime instance in target ViewPage/ViewState.

Note:Since ViewElement is pooled and managed by ViewSystem, so those API only works while the target ViewPage/ViewState is live.
ViewElement reference may changed after each ChangePage() call is complete.
```csharp
public ViewElement GetViewPageElementByName(ViewPage viewPage, string viewPageItemName);

public ViewElement GetViewPageElementByName(string viewPageName, string viewPageItemName);

public T GetViewPageElementComponentByName<T>(string viewPageName, string viewPageItemName) where T : Component;

public ViewElement GetCurrentViewPageElementByName(string viewPageItemName);

public T GetCurrentViewPageElementComponentByName<T>(string viewPageItemName) where T : Component;

//Get viewElement in statePage
public ViewElement GetViewStateElementByName(ViewState viewState, string viewStateItemName);
        
public T GetViewStateElementComponentByName<T>(ViewState viewState, string viewStateItemName) where T : Component;

public ViewElement GetViewStateElementByName(string viewStateName, string viewStateItemName);

public T GetViewStateElementComponentByName<T>(string viewStateName, string viewStateItemName) where T : Component;

public ViewElement GetCurrentViewStateElementByName(string viewStateItemName);

public T GetCurrentViewStateElementComponentByName<T>(string viewStateItemName) where T : Component;
```

# Made with ViewSystem
Those product use the ViewSystem as the UI manage tool.

<table align="center">
  <tr>
    <td>
        <a href="https://itunes.apple.com/app/id1560796657">
            <img src="https://play-lh.googleusercontent.com/gE3o9Fy930eSUgrCZ4vA4NyfNl1VXS4U6JQVl3v4tsJKyxS8b7j3_0HvNQLs3Tvkq57g=w240-h480-rw" alt="Screenshot2" width="320"/> 
            <p sytle="text-align:center;">Rhythm GO</p>
        </a>
    </td>
    <td>
        <a href="https://apps.apple.com/app/id1499441526">
            <img src="https://play-lh.googleusercontent.com/8eK24QTBPqYVk_UUeWi5rP88-MRhuW9p0r0jzpFtUQXLkRwSp8hv-HYDDPGpYTH40gg=w240-h480" alt="Screenshot2" width="320"/> 
            <p sytle="text-align:center;">Sky Bandit</p>
        </a>
    </td>
    <td>
        <a href="https://apps.apple.com/app/id1315384852">
            <img src="https://play-lh.googleusercontent.com/L7RwZ823l0BbIhodHwHVz8Y-nEaYWumib7FdZP9n6JlncYMR0z5ZdR6Ha3XNFRYwt1k=w240-h480" alt="Screenshot2" width="320"/> 
            <p sytle="text-align:center;">Sky Surfing</p>
        </a>
    </td>
  </tr>
  <tr>
    <td>
        <a href="https://apps.apple.com/app/id1315384852">
            <img src="https://play-lh.googleusercontent.com/VCTCcZKQb7Gnau5IdJLw_3WJLftgY0q6P_PHJgPerczsvk1bHP5oPxWF24YRGXK-qQ=w240-h480-rw" alt="Screenshot2" width="320"/> 
            <p sytle="text-align:center;">Merge & Shoot!</p>
        </a>
    </td>
    <td>
        <a href="https://play.google.com/store/apps/details?id=com.MacacaGames.G2_ProjectI&hl=zh_TW&gl=US">
            <img src="https://play-lh.googleusercontent.com/tWDyQbjL8voS2hebdIfku7Tzc_0QhgPGsLcb3yCxDSeRHWEqBl17R7tcNvifMij3GYJb=w240-h480" alt="Screenshot2" width="320"/> 
            <p sytle="text-align:center;">Fall A Sleep</p>
        </a>
    </td>
      <td>
        <a href="https://play.google.com/store/apps/details?id=com.MacacaGames.Cream&hl=zh_TW&gl=US">
            <img src="https://play-lh.googleusercontent.com/5UP2Hi5gEq0A5M1sMEQY8bR687xhDGjgMhG_s7-JJLQvDejpVoPXOvzMlHiUx4nGgQ=w240-h480" alt="Screenshot2" width="320"/> 
            <p sytle="text-align:center;">Cream Runner</p>
        </a>
    </td>
  </tr>
    <tr>
    <td>
        <a href="https://play.google.com/store/apps/details?id=com.MacacaGames.IceVillage&hl=zh_TW&gl=US">
            <img src="https://play-lh.googleusercontent.com/Nugy4lnCDG83KOY3bgadvL23XQDgciGznYwjZmI-tixEku1RQPtBB9t-YM4CQVn7zpI=w240-h480" alt="Screenshot2" width="320"/> 
            <p sytle="text-align:center;">Ice Village</p>
        </a>
    </td>
    <td>
        <a href="https://play.google.com/store/apps/details?id=com.MacacaGames.G2_ProjectK&hl=zh_TW&gl=US">
            <img src="https://play-lh.googleusercontent.com/1J3SIhZk07TUm_1LQVjKKKoO1onh0y0VxG0EMHCPIDlyZ4NzCmIivgTmeuRGoiG7tfJ_=w240-h480" alt="Screenshot2" width="320"/> 
            <p sytle="text-align:center;">Food Snatcher</p>
        </a>
    </td>
      <td>
        <a href="https://play.google.com/store/apps/details?id=com.MacacaGames.G2_ProjectF&hl=zh_TW&gl=US">
            <img src="https://play-lh.googleusercontent.com/IXT9_pjcbGYZk-2kw-dRmCklW1EPHaWkZS_rvMXjKg6zbqkErAj60MyWkr-4so1Evpf8=w240-h480" alt="Screenshot2" width="320"/> 
            <p sytle="text-align:center;">Ring Runner</p>
        </a>
    </td>
  </tr>
</table>

