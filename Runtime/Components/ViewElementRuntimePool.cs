using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem
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
        [SerializeField]
        Dictionary<int, ViewElement> uniqueVeDicts = new Dictionary<int, ViewElement>();
        Queue<ViewElement> recycleQueue = new Queue<ViewElement>();
        public void QueueViewElementToRecovery(ViewElement toRecovery)
        {
            recycleQueue.Enqueue(toRecovery);
        }
        public void RecoveryViewElement(ViewElement toRecovery)
        {
            if (toRecovery.IsUnique)
            {
                //Currentlly nothing needs to do.
            }
            else
            {
                if (!veDicts.TryGetValue(toRecovery.PoolKey, out Queue<ViewElement> veQueue))
                {
                    ViewSystemLog.LogWarning("Cannot find pool of ViewElement " + toRecovery.name + ", Destroy directly.");
                    UnityEngine.Object.Destroy(toRecovery);
                    return;
                }
                toRecovery.gameObject.SetActive(false);
                veQueue.Enqueue(toRecovery);
            }
        }
        const int maxRecoveryPerFrame = 5;
        public void RecoveryQueuedViewElement(bool force = false)
        {
            StartCoroutine(RecoveryQueuedViewElementRunner(force));
        }

        IEnumerator RecoveryQueuedViewElementRunner(bool force)
        {
        RESTART:
            int max = force ? recycleQueue.Count : 5;
            for (int i = 0; i < max; i++)
            {
                if (recycleQueue.Count > 0)
                {
                    var a = recycleQueue.Dequeue();
                    RecoveryViewElement(a);
                }
            }
            yield return null;
            if (recycleQueue.Count > 0)
            {
                goto RESTART;
            }
        }

        public ViewElement PrewarmUniqueViewElement(ViewElement source)
        {
            if (!source.IsUnique)
            {
                ViewSystemLog.LogWarning("The ViewElement trying to Prewarm is not an unique ViewElement");
                return null;
            }

            if (!uniqueVeDicts.ContainsKey(source.GetInstanceID()))
            {
                var temp = UnityEngine.Object.Instantiate(source, _hierachyPool.rectTransform);
                temp.name = source.name;
                uniqueVeDicts.Add(source.GetInstanceID(), temp);
                temp.gameObject.SetActive(false);
                return temp;
            }
            else
            {
                ViewSystemLog.LogWarning("ViewElement " + source.name + " has been prewarmed");
                return uniqueVeDicts[source.GetInstanceID()];
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
                }
                if (veQueue.Count == 0)
                {
                    var a = UnityEngine.Object.Instantiate(source, _hierachyPool.rectTransform);
                    a.name = source.name;
                    veQueue.Enqueue(a);
                }
                result = veQueue.Dequeue();
            }
            result.PoolKey = source.GetInstanceID();
            return result;
        }
    }
}