* [Overview](#overview)
* [Concept](#concept)
    + [ViewElement](#viewelement)
    + [ViewPage](#viewpage)
        + [FullPage](#fullpage)
        + [OverlayPage](#overlaypage)
    + [ViewState](#viewstate)
    + [ViewController](#viewcontroller)
* [Installation](#installation)
    + [Option 1: Git SubModule](#option-1-git-submodule)
    + [Option 2: Unity Package file](#option-2-unity-package-file)
    + [Option 3: Unity Package manager](#option-3-unity-package-manager)
* [System LifeCycle](#system-lifecycle)
    + [ViewController Initialization](#viewcontroller-initialization)
    + [FullPage ChangePage](#fullpage-changepage)
    + [OverlayPage Show](#overlaypage-show)
    + [OverlayPage Leave](#overlaypage-leave)



# Overview
**ViewSystem** is a SoC UI management system based on Unity GUI. It is developed and used by Macaca Games.
UI management in Unity3D is always a hard work, the goal of **ViewSystem** is to make UI management more easier and flexable.

# Concept
## ViewElement
ViewElement is the basic unit in ViewSystem, any item shows on an UI page can be a ViewElement, such as a button, a icon or anything else.

For example, the red square part in the screenshot is a ViewElement.
<img src="./Img/viewelement.png" alt="Screenshot2" height="400"/>

And the most important thing, ViewElement only focus how it will show or leave and **doesn't** care where it will be placed.

Currently there is four method to transition while we try to showing or leaving a ViewElement: **Animator**, **CanvasGroup Fade**, **Active Switch**, **Custom**.

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
> Currently, since the V1 branch has been Archived the ViewController in /master branch is obsolete, use ViewControllerV2 instead.

ViewController is the core component of ViewSystem, all control of the UI is base on this component.

# Installation
## Option 1: Git SubModule
```bash
git submodule add https://github.com/MacacaGames/MacacaViewSystem.git Assets/MacacaViewSystem
```
Note: ViewSystem is dependency with MacacaUtility, so also add MacacaUtility in git submodule.
```bash
git submodule add hhttps://github.com/MacacaGames/MacacaUtility.git Assets/MacacaUtility
```
## Option 2: Unity Package file
> Work in progress

## Option 3: Unity Package manager
> Work in progress

# System LifeCycle
## ViewController Initialization
> Here shows the Initialize proccess in ViewControllerV2. (Since V1 is dropped.)
1. Finding the UIRoot parent GameObject setup in GlobalSetting.
2. Instantiate UIRoot GameObject setup in GlobalSetting.
3. Generate ViewElementPool instance in scene.
3. Generate ViewElementRuntimePool instance in scene and initialize it.
4. Load ViewPage and ViewState data store in ViewSystemSaveData Object.
5. Pre-generate the ViewElement which has component inherited IViewElementInjectable

## FullPage ChangePage
> Once the ChangePage API is call in ViewControllerV2, the event, callback, lifecycle hack excude order. (Same behaviour while using FullPageChanger)

<img src="./Img/changepage_lifecycle.jpg" alt="Screenshot2" height="800"/>