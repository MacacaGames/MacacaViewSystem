// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// namespace MacacaGames.ViewSystem
// {
//     public class ViewCYInstruction : MonoBehaviour
//     {
//         public class WaitForStandardYieldInstruction : CustomYieldInstruction
//         {
//             bool isRunning;

//             private MonoBehaviour mono;
//             private Coroutine coroutine;
//             private YieldInstruction yieldInstruction;

//             // in case we need to do anything with it
//             public Coroutine Coroutine
//             {
//                 get
//                 {
//                     return coroutine;
//                 }
//             }

//             // we need a standard Coroutine running for DOTween's YieldInstruction to work
//             // so we invoke it here, running on the monobehaviour we passed as argument
//             public WaitForStandardYieldInstruction(MonoBehaviour mono, YieldInstruction yieldInstruction)
//             {
//                 this.mono = mono;
//                 this.yieldInstruction = yieldInstruction;
//                 this.coroutine = mono.StartCoroutine(IEWaitForYieldInstruction());

//             }

//             // this is where the original YieldInstruction is called, to halt the flow until complete
//             // in the example case we are waiting on `myTween.WaitForCompletion()`
//             public IEnumerator IEWaitForYieldInstruction()
//             {
//                 isRunning = true;
//                 yield return yieldInstruction;
//                 isRunning = false;
//             }

//             public override bool keepWaiting
//             {
//                 get
//                 {
//                     return isRunning;
//                 }
//             }
//         }
//     }
// }