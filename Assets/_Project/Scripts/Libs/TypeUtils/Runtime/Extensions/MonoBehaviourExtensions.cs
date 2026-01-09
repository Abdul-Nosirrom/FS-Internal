using System;
using System.Collections;
using UnityEngine;

namespace FS.Extensions
{
    public static class MonoBehaviourExtensions
    {
        /// <summary>
        /// Stops the given coroutine and disposes it if it implements IDisposable (compile generates it if it has a yield instruction).
        /// The dispose is necessary to ensure if using a try/finally in the coroutine, the finally block is executed.
        /// </summary>
        public static void StopAndDisposeCoroutine(this MonoBehaviour mono, IEnumerator coroutine)
        {
            mono.StopCoroutine(coroutine);
            if (coroutine is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}