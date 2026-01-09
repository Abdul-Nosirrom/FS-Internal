using System.Collections.Generic;
using UnityEngine;

namespace FS.Utility
{
    public static class Yields
    {
        private static readonly Dictionary<float, WaitForSeconds> s_waitForSecondsCache = new Dictionary<float, WaitForSeconds>();
        private static readonly Dictionary<float, WaitForSecondsRealtime> s_waitForSecondsRealtimeCache = new Dictionary<float, WaitForSecondsRealtime>();

        public static object WaitForNextFrame => null; // yield return null
        public static WaitForEndOfFrame WaitForEndOfFrame { get; } = new WaitForEndOfFrame();
        public static WaitForFixedUpdate WaitForFixedUpdate { get; } = new WaitForFixedUpdate();

        public static WaitForSeconds WaitForSeconds(float seconds)
        {
            if (!s_waitForSecondsCache.TryGetValue(seconds, out var wait))
            {
                wait = new WaitForSeconds(seconds);
                s_waitForSecondsCache[seconds] = wait;
            }
            
            return wait;
        }
        public static WaitForSecondsRealtime WaitForSecondsRealtime(float seconds)
        {
            if (!s_waitForSecondsRealtimeCache.TryGetValue(seconds, out var wait))
            {
                wait = new WaitForSecondsRealtime(seconds);
                s_waitForSecondsRealtimeCache[seconds] = wait;
            }
            return wait;
        }
    }
}