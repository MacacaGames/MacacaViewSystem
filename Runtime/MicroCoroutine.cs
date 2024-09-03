using System;
using System.Collections;
using System.Collections.Generic;

namespace MacacaGames.ViewSystem
{
    /// <summary>
    /// Simple supports(only yield return null) lightweight, threadsafe coroutine dispatcher.
    /// </summary>
    public class MicroCoroutine
    {
        public class Coroutine : IDisposable
        {
            public Coroutine(IEnumerator ie)
            {
                this.ie = ie;
            }
            public IEnumerator ie;

            public void Dispose()
            {
                // throw new NotImplementedException();
            }
        }
        List<Coroutine> coroutines = new List<Coroutine>();
        readonly Action<Exception> unhandledExceptionCallback;
        bool running = false;
        public Coroutine AddCoroutine(IEnumerator enumerator)
        {
            // Move Next first, if false means the coroutine is complete or empty, so we don't need to add to MicroCoroutine
            if (enumerator.MoveNext())
            {
                var c = new Coroutine(enumerator);
                coroutines.Add(c);
                return c;
            }
            return null;
        }
        Queue<Coroutine> removeQueue = new Queue<Coroutine>();

        public bool IsEmpty
        {
            get
            {
                return coroutines == null || coroutines.Count == 0;
            }
        }

        public void RemoveCoroutine(Coroutine enumerator)
        {
            if (running)
            {
                removeQueue.Enqueue(enumerator);
            }
            else
            {
                coroutines.Remove(enumerator);
            }
        }
        public MicroCoroutine(Action<Exception> unhandledExceptionCallback)
        {
            this.unhandledExceptionCallback = unhandledExceptionCallback;
        }
        public void Update()
        {
            running = true;
            for (int i = 0; i < coroutines.Count; i++)
            {
                var coroutine = coroutines[i];
                if (coroutine != null && coroutine.ie != null)
                {
                    try
                    {
                        if (!coroutine.ie.MoveNext())
                        {
                            coroutines[i] = null;
                        }
                        else
                        {
#if UNITY_EDITOR
                            // validation only on Editor.
                            if (coroutine.ie.Current != null)
                            {
                                UnityEngine.Debug.LogWarning("MicroCoroutine supports only yield return null. return value = " + coroutine.ie.Current);
                            }
#endif
                            continue; // next i 
                        }
                    }
                    catch (Exception ex)
                    {
                        coroutines[i] = null;
                        try
                        {
                            unhandledExceptionCallback(ex);
                        }
                        catch { }
                    }
                }
            }
            running = false;
            while (removeQueue.Count > 0)
            {
                var e = removeQueue.Dequeue();
                coroutines.Remove(e);
                e = null;
            }
            coroutines.RemoveAll(match => match == null);
        }
    }
}
