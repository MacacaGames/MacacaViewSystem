using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    public interface IViewElementSingleton
    { }
    [System.Obsolete("IViewElementInjectable is obsolete, use IViewElementSingleton instead")]
    public interface IViewElementInjectable : IViewElementSingleton
    { }
}