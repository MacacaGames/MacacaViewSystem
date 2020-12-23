using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace MacacaGames.ViewSystem.VisualEditor
{
    interface IViewSystemDateReader
    {
        bool Init();
        void OnViewPageDelete(ViewPageNode node);
        void OnViewStateDelete(ViewStateNode node);
        void OnViewPageAdd(ViewPageNode node);
        void OnViewStateAdd(ViewStateNode node);
        void Save(List<ViewPageNode> viewPageNodes, List<ViewStateNode> viewStateNodes);
        void OnViewPagePreview(ViewPage viewPage);
        void Normalized();
    }
}