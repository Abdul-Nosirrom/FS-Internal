using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FS.Audio
{
    /// <summary>
    /// Reference counted FMod Bank Loader. Ensures banks are properly loaded and unloaded based on reference counts.
    /// </summary>
    [AddComponentMenu("Free Skies/Audio/Bank Loader")]
    public class AudioBankLoader : MonoBehaviour
    {
        [SerializeField, Required, BankRef]
        private string m_bankToLoad;
        [SerializeField] 
        private bool m_preloadSamples = false;
        
        private void Start()
        {
            AudioManager.LoadBank(m_bankToLoad);
        }

        private void OnDestroy()
        {
            AudioManager.UnloadBank(m_bankToLoad);
        }
    }
}