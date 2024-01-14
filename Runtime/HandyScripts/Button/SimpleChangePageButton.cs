using UnityEngine;
using UnityEngine.UI;

namespace MacacaGames.ViewSystem
{
   [RequireComponent(typeof(Button))]
   public class SimpleChangePageButton : MonoBehaviour
   {
      [SerializeField, ViewSystemDropdown] private string pageName;
      Button _button;

      Button button
      {
         get
         {
            if (_button == null)
            {
               _button = GetComponent<Button>();
            }

            return _button;
         }
      }

      void Awake()
      {
         button.onClick.AddListener(() => { ChangePage(); });
      }

      void ChangePage()
      {
         ViewController.Instance.ChangePage(pageName);
      }
   }
}