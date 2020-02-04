using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{
    public class ViewCYInstruction : MonoBehaviour
    {
        public class WaitForStandardCoroutine : CustomYieldInstruction
        {
            private bool isRunning;

            private MonoBehaviour mono;
            private IEnumerator ie;
            private Coroutine coroutine;

            public Coroutine Coroutine
            {
                get
                {
                    return coroutine;
                }
            }

            public WaitForStandardCoroutine(MonoBehaviour mono, IEnumerator ie)
            {
                this.mono = mono;
                this.ie = ie;
                this.coroutine = mono.StartCoroutine(IEWaitForCoroutine());
            }
            public WaitForStandardCoroutine(Coroutine coroutine)
            {
                this.coroutine = coroutine;
            }

            public IEnumerator IEWaitForCoroutine()
            {
                isRunning = true;
                if (coroutine == null) coroutine = mono.StartCoroutine(ie);
                yield return coroutine;
                isRunning = false;
            }

            public override bool keepWaiting
            {
                get
                {
                    return isRunning;
                }
            }
        }
    }
}