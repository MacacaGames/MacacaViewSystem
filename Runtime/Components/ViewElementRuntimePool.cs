using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    public class ViewElementRuntimePool : MonoBehaviour
    {
        bool init = false;
        ViewElementPool _hierachyPool;
        public void Init(ViewElementPool hierachyPool)
        {
            _hierachyPool = hierachyPool;
            init = true;
        }
        [SerializeField]
        Dictionary<int, Queue<ViewElement>> veDicts = new Dictionary<int, Queue<ViewElement>>();

#if UNITY_EDITOR
        public Dictionary<int, Queue<ViewElement>> GetDicts()
        {
            return veDicts;
        }
        public Dictionary<int, string> veNameDicts = new Dictionary<int, string>();
        public Queue<ViewElement> GetRecycleQueue()
        {
            return recycleQueue;
        }
#endif
        [SerializeField]
        Dictionary<int, ViewElement> uniqueVeDicts = new Dictionary<int, ViewElement>();
        Queue<ViewElement> recycleQueue = new Queue<ViewElement>();
        public void QueueViewElementToRecovery(ViewElement toRecovery)
        {


            toRecovery.rectTransform.SetParent(_hierachyPool.transformCache, true);

            if (toRecovery.IsUnique)
            {
                // unique ViewElement just needs to disable gameObject
                RecoveryViewElement(toRecovery);
            }
            else
            {
                if (!recycleQueue.Contains(toRecovery))
                    recycleQueue.Enqueue(toRecovery);
            }
        }
        public void RecoveryViewElement(ViewElement toRecovery)
        {
            // Debug.Log($"Recovery {toRecovery.name}");
            if (toRecovery.IsUnique)
            {
                // unique ViewElement just needs to disable gameObject
                toRecovery.gameObject.SetActive(false);
            }
            else
            {
                if (!veDicts.TryGetValue(toRecovery.PoolKey, out Queue<ViewElement> veQueue))
                {
                    ViewSystemLog.LogWarning("Cannot find pool of ViewElement " + toRecovery.name + ", Destroy directly.", toRecovery);
                    UnityEngine.Object.Destroy(toRecovery);
                    return;
                }
                toRecovery.gameObject.SetActive(false);
                veQueue.Enqueue(toRecovery);
            }
        }
        const int maxRecoveryPerFrame = 5;
        public Coroutine RecoveryQueuedViewElement(bool force = false)
        {
            return StartCoroutine(RecoveryQueuedViewElementRunner(force));
        }

        IEnumerator RecoveryQueuedViewElementRunner(bool force)
        {
            int max = force ? recycleQueue.Count : maxRecoveryPerFrame;
            while (recycleQueue.Count > 0)
            {
                for (int i = 0; i < max; i++)
                {
                    if (recycleQueue.Count > 0)
                    {
                        var a = recycleQueue.Dequeue();
                        RecoveryViewElement(a);
                    }
                }
                yield return null;
            }
        }
        int i = 0;
        public ViewElement PrewarmUniqueViewElement(ViewElement source)
        {
            if (!source.IsUnique)
            {
                ViewSystemLog.LogWarning("The ViewElement trying to Prewarm is not an unique ViewElement");
                return null;
            }
            var i = source.GetInstanceID();
            if (!uniqueVeDicts.ContainsKey(source.GetInstanceID()))
            {

                var temp = UnityEngine.Object.Instantiate(source, _hierachyPool.rectTransform);
                temp.name = source.name;
                uniqueVeDicts.Add(i, temp);
                temp.gameObject.SetActive(false);
                return temp;
            }
            else
            {
                ViewSystemLog.LogWarning("ViewElement " + source.name + " has been prewarmed");
                return uniqueVeDicts[i];
            }
        }
        public ViewElement RequestViewElement(ViewElement source)
        {
            ViewElement result;

            if (source.IsUnique)
            {
                if (!uniqueVeDicts.TryGetValue(source.GetInstanceID(), out result))
                {
                    result = UnityEngine.Object.Instantiate(source, _hierachyPool.rectTransform);
                    result.gameObject.SetActive(false);
                    result.name = source.name;
                    uniqueVeDicts.Add(source.GetInstanceID(), result);
                }
            }
            else
            {
                Queue<ViewElement> veQueue;
                if (!veDicts.TryGetValue(source.GetInstanceID(), out veQueue))
                {
                    veQueue = new Queue<ViewElement>();
                    veDicts.Add(source.GetInstanceID(), veQueue);
#if UNITY_EDITOR
                    veNameDicts.Add(source.GetInstanceID(), source.name);
#endif
                }
                if (veQueue.Count > 0)
                {
                    result = veQueue.Dequeue();
                    // Debug.Log($"Request {source.name} from dequeue :  { Time.frameCount}");
                }
                else
                {
                    var a = UnityEngine.Object.Instantiate(source, _hierachyPool.rectTransform);
                    a.gameObject.SetActive(false);
                    a.name = source.name;
                    result = a;
                    // Debug.Log($"Request {source.name} from generate new one : { Time.frameCount}");
                }
            }
            result.PoolKey = source.GetInstanceID();
            return result;
        }
    }
}