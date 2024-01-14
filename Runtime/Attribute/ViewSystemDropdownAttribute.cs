using System;
using UnityEngine;

namespace MacacaGames.ViewSystem
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class ViewSystemDropdownAttribute : PropertyAttribute
    {
        // This is a marker attribute, so it's empty
    }
}