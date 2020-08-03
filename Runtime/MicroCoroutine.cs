using System;
using System.Collections;
using System.Collections.Generic;

namespace CloudMacaca.ViewSystem
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
        static bool running = false;
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
                            coroutines[i]= null;
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

    //         const int InitialSize = 16;

    //         readonly object runningAndQueueLock = new object();
    //         readonly object arrayLock = new object();
    //         readonly Action<Exception> unhandledExceptionCallback;

    //         int tail = 0;
    //         bool running = false;
    //         IEnumerator[] coroutines = new IEnumerator[InitialSize];
    //         Queue<IEnumerator> waitQueue = new Queue<IEnumerator>();

    //         public MicroCoroutine(Action<Exception> unhandledExceptionCallback)
    //         {
    //             this.unhandledExceptionCallback = unhandledExceptionCallback;
    //         }

    //         public void AddCoroutine(IEnumerator enumerator)
    //         {
    //             lock (runningAndQueueLock)
    //             {
    //                 if (running)
    //                 {
    //                     waitQueue.Enqueue(enumerator);
    //                     return;
    //                 }
    //             }

    //             // worst case at multi threading, wait lock until finish Update() but it is super rarely.
    //             lock (arrayLock)
    //             {
    //                 // Ensure Capacity
    //                 if (coroutines.Length == tail)
    //                 {
    //                     Array.Resize(ref coroutines, checked(tail * 2));
    //                 }
    //                 coroutines[tail++] = enumerator;
    //             }
    //         }

    //         public void Update()
    //         {
    //             lock (runningAndQueueLock)
    //             {
    //                 running = true;
    //             }

    //             lock (arrayLock)
    //             {
    //                 var j = tail - 1;

    //                 // eliminate array-bound check for i
    //                 for (int i = 0; i < coroutines.Length; i++)
    //                 {
    //                     var coroutine = coroutines[i];
    //                     if (coroutine != null)
    //                     {
    //                         try
    //                         {
    //                             if (!coroutine.MoveNext())
    //                             {
    //                                 coroutines[i] = null;
    //                             }
    //                             else
    //                             {
    // #if UNITY_EDITOR
    //                                 // validation only on Editor.
    //                                 if (coroutine.Current != null)
    //                                 {
    //                                     UnityEngine.Debug.LogWarning("MicroCoroutine supports only yield return null. return value = " + coroutine.Current);
    //                                 }
    // #endif

    //                                 continue; // next i 
    //                             }
    //                         }
    //                         catch (Exception ex)
    //                         {
    //                             coroutines[i] = null;
    //                             try
    //                             {
    //                                 unhandledExceptionCallback(ex);
    //                             }
    //                             catch { }
    //                         }
    //                     }

    //                     // find null, loop from tail
    //                     while (i < j)
    //                     {
    //                         var fromTail = coroutines[j];
    //                         if (fromTail != null)
    //                         {
    //                             try
    //                             {
    //                                 if (!fromTail.MoveNext())
    //                                 {
    //                                     coroutines[j] = null;
    //                                     j--;
    //                                     continue; // next j
    //                                 }
    //                                 else
    //                                 {
    // #if UNITY_EDITOR
    //                                     // validation only on Editor.
    //                                     if (fromTail.Current != null)
    //                                     {
    //                                         UnityEngine.Debug.LogWarning("MicroCoroutine supports only yield return null. return value = " + coroutine.Current);
    //                                     }
    // #endif

    //                                     // swap
    //                                     coroutines[i] = fromTail;
    //                                     coroutines[j] = null;
    //                                     j--;
    //                                     goto NEXT_LOOP; // next i
    //                                 }
    //                             }
    //                             catch (Exception ex)
    //                             {
    //                                 coroutines[j] = null;
    //                                 j--;
    //                                 try
    //                                 {
    //                                     unhandledExceptionCallback(ex);
    //                                 }
    //                                 catch { }
    //                                 continue; // next j
    //                             }
    //                         }
    //                         else
    //                         {
    //                             j--;
    //                         }
    //                     }

    //                     tail = i; // loop end
    //                     break; // LOOP END

    //                 NEXT_LOOP:
    //                     continue;
    //                 }


    //                 lock (runningAndQueueLock)
    //                 {
    //                     running = false;
    //                     while (waitQueue.Count != 0)
    //                     {
    //                         if (coroutines.Length == tail)
    //                         {
    //                             Array.Resize(ref coroutines, checked(tail * 2));
    //                         }
    //                         coroutines[tail++] = waitQueue.Dequeue();
    //                     }
    //                 }
    //             }
    //         }
    //     }
}
