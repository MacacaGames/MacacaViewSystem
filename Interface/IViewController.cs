using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{
    public interface IViewController
    {
        CloudMacaca.ViewSystem.ViewPageItem.PlatformOption platform { get; }
        bool IsOverlayTransition { get; }
    }
}