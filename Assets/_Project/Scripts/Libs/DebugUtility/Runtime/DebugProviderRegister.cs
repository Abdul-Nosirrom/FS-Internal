using System;
using UnityEngine;

namespace FS.RuntimeDebug
{
    public class DebugProviderRegister : MonoBehaviour
    {
        [SerializeField] private GameObject m_contextObject;
        private void Awake()
        {
            var debugProviders = GetComponentsInChildren<IDebugProvider>();
            foreach (var provider in debugProviders)
            {
                DebugManager.Instance.RegisterDebugProvider(m_contextObject ?? gameObject, provider);
            }
        }

        private void OnDestroy()
        {
            if (!DebugManager.Instance) return;
            DebugManager.Instance.UnregisterDebugProvider(m_contextObject ?? gameObject);
        }
    }
}