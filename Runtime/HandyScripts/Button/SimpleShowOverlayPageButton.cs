using UnityEngine;
using UnityEngine.UI;

namespace MacacaGames.ViewSystem
{
   [RequireComponent(typeof(Button))]
   public class SimpleShowOverlayPageButton : MonoBehaviour
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
         button.onClick.AddListener(() => { OpenOverlayPage(); });
      }

      void OpenOverlayPage()
      {
         ViewController.Instance.ShowOverlayViewPage(pageName);
      }
   }
}