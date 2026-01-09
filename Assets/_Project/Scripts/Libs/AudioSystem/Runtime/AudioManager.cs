using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using FS.Patterns;
using FS.RuntimeDebug;
using TimeUtils;
using UnityEngine;
using Debug = UnityEngine.Debug;
using GUID = FMOD.GUID;
using STOP_MODE = FMOD.Studio.STOP_MODE;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FS.Audio
{
    public class AudioManager : PersistentSingleton<AudioManager>, IDebugProvider
    {
        private Bus m_masterBus;
        private Bus m_sfxBus;
        private Bus m_musicBus;
        private Bus m_ambienceBus;
        private Bus m_uiBus;
        
        private CancellationTokenSource m_periodicCleanupToken;
        
        protected override void InitializeSingleton()
        {
            base.InitializeSingleton();
            
            // Get all buses
            m_masterBus = FMODUnity.RuntimeManager.GetBus("bus:/");
            m_sfxBus = FMODUnity.RuntimeManager.GetBus("bus:/SFX");
            m_musicBus = FMODUnity.RuntimeManager.GetBus("bus:/Music");
            m_ambienceBus = FMODUnity.RuntimeManager.GetBus("bus:/Ambience");
            m_uiBus = FMODUnity.RuntimeManager.GetBus("bus:/UI");
            
            // Initialize the audio instance pool
            m_audioInstancePool = new Dictionary<GUID, AudioPool>();

            // Start the periodic cleanup task
            m_periodicCleanupToken = new CancellationTokenSource();
            _ = GlobalPoolCleanupMonitoringAsync(m_periodicCleanupToken.Token);
            
            // Register debug provider
            DebugManager.Instance.RegisterDebugProvider(gameObject, this);
        }

        private void OnDestroy()
        {
            m_periodicCleanupToken.Cancel();
        }

        // We should dispose of individual pools whenever their associated bank is unloaded
        private Dictionary<GUID, AudioPool> m_audioInstancePool;
        private Dictionary<GUID, int> m_loadedBankRefCounts = new();

        private async Awaitable GlobalPoolCleanupMonitoringAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // Loop through each pool & invoke their cleanup logic
                foreach (var pool in m_audioInstancePool.Values)
                {
                    await pool.PeriodicCleanupAsync(token);
                    await Awaitable.NextFrameAsync(token);
                }
                
                // Wait for a short period before the next cleanup roung
                await Task.Delay(100, token); // awaitable is affected by time scale
            }
        }

        public static void LoadBank(string bankPath, bool preloadSamples = false)
        {
            RuntimeManager.StudioSystem.lookupID(bankPath, out var bankGUID);
            if (!Instance.m_loadedBankRefCounts.TryAdd(bankGUID, 1))
            {
                Instance.m_loadedBankRefCounts[bankGUID]++;
                return;
            }

            RuntimeUtils.EnforceLibraryOrder();
            try
            {
                RuntimeManager.LoadBank(bankPath, preloadSamples);
            }
            catch (BankLoadException e)
            {
                RuntimeUtils.DebugLogException(e);
            }
            RuntimeManager.WaitForAllSampleLoading();
        }
        
        public static void UnloadBank(string bankPath)
        {
            if (Instance == null) return; // Can happen if the game is quitting where the getter will not create an instance
            
            RuntimeManager.StudioSystem.lookupID(bankPath, out var bankGUID);
            if (!Instance.m_loadedBankRefCounts.TryGetValue(bankGUID, out var refCount))
            {
                Debug.LogWarning($"[Audio System] Attempted to unload bank {bankPath} that was never loaded.");
                return;
            }

            refCount--;
            Instance.m_loadedBankRefCounts[bankGUID] = refCount;
            
            if (refCount > 0)
            {
                Debug.Log($"[Audio System] Decremented reference count for bank {bankPath}. Remaining: {refCount}");
                return; // Still in use, just decrement the reference count
            }
            
            // Dispose of the audio pool associated with this bank
            RuntimeManager.StudioSystem.getBankByID(bankGUID, out var bankDef);
            if (bankDef.isValid())
            {
                bankDef.getEventList(out var events);
                foreach (var audioDesc in events)
                {
                    audioDesc.getID(out var audioID);
                    if (Instance.m_audioInstancePool.TryGetValue(audioID, out var audioPool))
                    {
                        audioPool.Dispose();
                        Instance.m_audioInstancePool.Remove(audioID);
                    }
                }
            }

            RuntimeManager.UnloadBank(bankPath);
            Instance.m_loadedBankRefCounts.Remove(bankGUID);
        }

        public static AudioHandle PlayAudio_2D(AudioElement audioEvt) => Instance.PlayAudio_Internal(audioEvt);
        public static AudioHandle PlayAudio_3D(AudioElement audioEvt, Vector3 position) => Instance.PlayAudio_Internal(audioEvt, position);
        public static AudioHandle PlayAudio_Attached(AudioElement audioEvt, GameObject gameObject) => Instance.PlayAudio_Internal(audioEvt, null, gameObject);
        
        private bool TryGetAudioPool(AudioElement audioEvt, out AudioPool audioPool) => m_audioInstancePool.TryGetValue(audioEvt.AudioEvent.Guid, out audioPool);
        
        private AudioPool GetOrCreateAudioPool(AudioElement audioEvt)
        {
            if (!TryGetAudioPool(audioEvt, out var audioPool))
            {
                audioPool = new AudioPool(audioEvt.AudioEvent);
                m_audioInstancePool[audioEvt.AudioEvent.Guid] = audioPool;
#if UNITY_EDITOR                
                Debug.Log($"[Audio System] Creating new audio pool for event: {audioEvt.AudioEvent.Path}");
#endif                
            }
            return audioPool;
        }
        
        private AudioHandle PlayAudio_Internal(AudioElement audioEvt, Vector3? position = null, GameObject attachObject = null)
        {
            var audioPool = GetOrCreateAudioPool(audioEvt);
            
            var audioHandle = audioPool.Get();
            if (audioHandle.IsValid)
            {
                if (position.HasValue)
                    audioHandle.AudioInstance.set3DAttributes(position.Value.To3DAttributes());
                else if (attachObject != null)
                    AttachAudioInstance(audioHandle, attachObject);
                
                audioHandle.SetParameters(audioEvt.Parameters);
                audioHandle.Play();
            }
            else
                Debug.LogError($"Failed to play audio event: {audioEvt}");
            
            return audioHandle.IsValid ? audioHandle : null;
        }

        public static AudioHandle CreateAudioInstance(AudioElement audioEvt, bool play = true)
        {
            var audioPool = Instance.GetOrCreateAudioPool(audioEvt);
            var instance = audioPool.Get();
            if (!instance.IsValid)
            {
                Debug.LogError($"Failed to create audio instance for event: {audioEvt}");
                return null;
            }

            instance.SetParameters(audioEvt.Parameters);
            if (play)
            {
                instance.Play();
            }
            return instance;
        }
        
        public static void AttachAudioInstance(AudioHandle audioInst, GameObject gameObject)
        {
            if (audioInst != null && audioInst.IsValid)
                FMODUnity.RuntimeManager.AttachInstanceToGameObject(audioInst.AudioInstance, gameObject, true);
            else
                Debug.LogWarning($"[Audio System] Audio Event Instance is not valid: {audioInst}");
        }

        public static void StopAudioInstance(AudioHandle audioInst, STOP_MODE stopMode = STOP_MODE.ALLOWFADEOUT)
        {
            if (audioInst == null || !audioInst.IsValid)
            {
                Debug.LogWarning($"[Audio System] Audio Event Instance is not valid: {audioInst}");
                return;
            }
            
            audioInst.Stop(stopMode);
        }

        public static PARAMETER_DESCRIPTION GetParameterDesc(EventReference audioEvt, string paramName)
        {
            var eventDesc = RuntimeManager.GetEventDescription(audioEvt);
            eventDesc.getParameterDescriptionByName(paramName, out var paramDesc);
            return paramDesc;
        }

        public static PARAMETER_ID GetParameterID(EventReference audioEvt, string paramName) =>
            GetParameterDesc(audioEvt, paramName).id;
        
        public static void SetLocalParameter(AudioHandle audioEvt, string paramName, float value)
        {
            if (audioEvt != null && audioEvt.IsValid)
                audioEvt.AudioInstance.setParameterByName(paramName, value);
            else
                Debug.LogWarning($"Audio Event Instance is not valid: {audioEvt}");
        }
        
        public static void SetLocalLabelParameter(AudioHandle audioEvt, string paramName, string value)
        {
            if (audioEvt != null && audioEvt.IsValid)
                audioEvt.AudioInstance.setParameterByNameWithLabel(paramName, value);
            else
                Debug.LogWarning($"Audio Event Instance is not valid: {audioEvt}");
        }
        
        public static void SetGlobalParameter(string paramName, float value)
        {
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName(paramName, value);
        }
        public static void SetGlobalLabelParameter(string paramName, string value)
        {
            FMODUnity.RuntimeManager.StudioSystem.setParameterByNameWithLabel(paramName, value);
        }

        #region Audio Bus Controls
        
        public static float GetMasterVolume()
        {
            Instance.m_masterBus.getVolume(out var volume);
            return volume;
        }
        public static float GetSFXVolume()
        {
            Instance.m_sfxBus.getVolume(out var volume);
            return volume;
        }
        public static float GetMusicVolume()
        {
            Instance.m_musicBus.getVolume(out var volume);
            return volume;
        }
        public static float GetAmbienceVolume()
        {
            Instance.m_ambienceBus.getVolume(out var volume);
            return volume;
        }
        public static float GetUIVolume()
        {
            Instance.m_uiBus.getVolume(out var volume);
            return volume;
        }
        
        public static void SetMasterVolume(float volume) => Instance.m_masterBus.setVolume(Mathf.Clamp01(volume));
        public static void SetSFXVolume(float volume) => Instance.m_sfxBus.setVolume(Mathf.Clamp01(volume));
        public static void SetMusicVolume(float volume) => Instance.m_musicBus.setVolume(Mathf.Clamp01(volume));
        public static void SetAmbienceVolume(float volume) => Instance.m_ambienceBus.setVolume(Mathf.Clamp01(volume));
        public static void SetUIVolume(float volume) => Instance.m_uiBus.setVolume(Mathf.Clamp01(volume));
        
        public static void MuteMaster(bool mute) => Instance.m_masterBus.setMute(mute);
        public static void MuteSFX(bool mute) => Instance.m_sfxBus.setMute(mute);
        public static void MuteMusic(bool mute) => Instance.m_musicBus.setMute(mute);
        public static void MuteAmbience(bool mute) => Instance.m_ambienceBus.setMute(mute);
        public static void MuteUI(bool mute) => Instance.m_uiBus.setMute(mute);
        
        public static bool IsMasterMuted()
        {
            Instance.m_masterBus.getMute(out var mute);
            return mute;
        }
        public static bool IsSFXMuted()
        {
            Instance.m_sfxBus.getMute(out var mute);
            return mute;
        }
        public static bool IsMusicMuted()
        {
            Instance.m_musicBus.getMute(out var mute);
            return mute;
        }
        public static bool IsAmbienceMuted()
        {
            Instance.m_ambienceBus.getMute(out var mute);
            return mute;
        }
        public static bool IsUIMuted()
        {
            Instance.m_uiBus.getMute(out var mute);
            return mute;
        }
        
        #endregion        

#if UNITY_EDITOR

        private static int s_previewBankRefCount;

        [InitializeOnLoadMethod]
        private static void ResetEditorPreviewAudio()
        {
            s_previewBankRefCount = 0;
        }
        
        public static EventInstance EditorPlayPreview(
            AudioElement editorEvent, 
            float volume = 1F, 
            float startTime = 0F)
        {
            EditorEventRef editorEventRef = EventManager.EventFromGUID(editorEvent.AudioEvent.Guid);
            if (editorEventRef == null)
            {
                Debug.LogError($"[Audio System] No event found for GUID: {editorEvent.AudioEvent.Path}");
                return default;
            }
            
            // Prepare parameters for preview
            var previewParamValues = new Dictionary<string, float>();
            if (editorEvent.Parameters != null)
            {
                foreach (var param in editorEvent.Parameters)
                {
                    previewParamValues[param.Name] = param.Value;
                }
            }
            
            return FMODUnity.EditorUtils.PreviewEvent(editorEventRef, previewParamValues, volume, startTime);
        }
        
        public static void EditorPausePreview(EventInstance audioEvt)
        {
            if (audioEvt.isValid())
                FMODUnity.EditorUtils.PreviewPause(audioEvt);
            else
                Debug.LogWarning($"[Audio System] Audio Event Instance is not valid: {audioEvt}");
        }
        
        public static void EditorStopPreview(EventInstance audioEvt, STOP_MODE stopMode = STOP_MODE.ALLOWFADEOUT)
        {
            if (audioEvt.isValid())
                FMODUnity.EditorUtils.PreviewStop(audioEvt, stopMode);
            else
                Debug.LogWarning($"[Audio System] Audio Event Instance is not valid: {audioEvt}");
        }

        public static void EditorStopAllPreviews() => FMODUnity.EditorUtils.StopAllPreviews();

        public static bool AreEditorPreviewBanksLoaded => FMODUnity.EditorUtils.PreviewBanksLoaded;

        public static void LoadEditorPreviewBanks()
        {
            if (AreEditorPreviewBanksLoaded)
            {
                s_previewBankRefCount++;
                return;
            }
            
            FMODUnity.EditorUtils.LoadPreviewBanks();
            s_previewBankRefCount = 1;
        }
        
        public static void UnloadEditorPreviewBanks()
        {
            s_previewBankRefCount--;
            
            if (s_previewBankRefCount > 0) return; // Still in use, just decrement the reference count
            
            //EditorStopAllPreviews();
            FMODUnity.EditorUtils.UnloadPreviewBanks();
        }
#endif

        public string DebugName => "Audio System";
        private bool debug_bankFoldout;
        private bool debug_poolFoldout;
        private Queue<float> debug_cpuUsageQueue = new Queue<float>(128);
        private Queue<float> debug_memoryUsageQueue = new Queue<float>(128);
        private TimeSince debug_sinceUpdatedPlotData;
        private bool debug_PlotsFoldout = true;
        private Vector2 debug_poolScrollPos = Vector2.zero;
        private DSP debug_mixerHead;
        private string debug_performanceString = null;
        private TimeSince debug_sinceLastPerformanceUpdate;
        
        public void OnDebugGUI()
        {
            GetDebugPerformanceString();
            GUILayout.Label(debug_performanceString);
            
            // CPU State
            StringBuilder profilerString = new StringBuilder();
            FMODUnity.RuntimeManager.StudioSystem.getCPUUsage(out var cpuUsage, out var cpuUsageCore);

            // Memory State
            FMOD.Memory.GetStats(out var currentAlloc, out var maxAlloc);
            var currentAllocMB = currentAlloc >> 20;
            var maxAllocMB = maxAlloc >> 20;
            
            // Plots
            if (debug_sinceUpdatedPlotData > 0.05f)
            {
                if (debug_cpuUsageQueue.Count >= 128)
                {
                    debug_cpuUsageQueue.Dequeue();
                    debug_memoryUsageQueue.Dequeue();
                }
                
                debug_cpuUsageQueue.Enqueue(cpuUsageCore.dsp);
                debug_memoryUsageQueue.Enqueue(currentAllocMB);
                debug_sinceUpdatedPlotData = 0f;
            }
            
            debug_PlotsFoldout = DebugGUI.BeginFoldout("CPU/Memory Graphs", ref debug_PlotsFoldout, GUI.skin.box);
            if (debug_PlotsFoldout)
            {
                GUILayout.Label("CPU Usage (DSP):");
                var graphRect = GUILayoutUtility.GetAspectRect(2);
                DebugGUI.Plot(debug_cpuUsageQueue.ToArray(), graphRect, Color.red, 100, false);
                
                GUILayout.Label("Memory Usage (MB):");
                graphRect = GUILayoutUtility.GetAspectRect(2);
                DebugGUI.Plot(debug_memoryUsageQueue.ToArray(), graphRect, Color.green, maxAllocMB, false);
                
                DebugGUI.EndFoldout();
            }

            
            // Bank Loading
            RuntimeManager.StudioSystem.getBankList(out var banks);
            
            var ogBGColor = GUI.color;
            
            debug_bankFoldout = DebugGUI.BeginFoldout("Audio Banks", ref debug_bankFoldout, GUI.skin.box);
            if (debug_bankFoldout)
            {
                GUILayout.Label($"Total Banks Loaded: {banks.Length}");
                GUILayout.Label("-------------------");

                foreach (var bank in banks)
                {
                    bank.getPath(out var path);
                    bank.getLoadingState(out var loadState);
                    bool isLoaded = loadState == FMOD.Studio.LOADING_STATE.LOADED;

                    GUI.color = isLoaded ? Color.green : Color.red;
                    GUILayout.Label($"Bank: {path}");
                    GUI.color = ogBGColor;
                }
                DebugGUI.EndFoldout();
            }
            
            // Audio Pool State
            debug_poolFoldout = DebugGUI.BeginFoldout("Audio Pools", ref debug_poolFoldout, GUI.skin.box);
            if (debug_poolFoldout)
            {
                GUILayout.Label($"Total Audio Pools: {m_audioInstancePool.Count}");
                GUILayout.Label("-------------------");
                debug_poolScrollPos = GUILayout.BeginScrollView(debug_poolScrollPos, GUILayout.Height(200));
                foreach (var pool in m_audioInstancePool.Values)
                {
                    GUILayout.Label($"{pool}");
                }
                GUILayout.EndScrollView();
                DebugGUI.EndFoldout();
            }
            
            //FMODUnity.RuntimeManager.StudioSystem.getMemoryUsage(out var memoryUsage);
            //GUILayout.Label($"Audio Memory Usage: {memoryUsage.usedMemory} bytes");
            // Memory Graph
        }

        private void GetDebugPerformanceString()
        {
            if (debug_sinceLastPerformanceUpdate < 0.25f && debug_performanceString != null) return; // Update every 0.25 seconds
            
            debug_sinceLastPerformanceUpdate = 0f;
            
            if (!debug_mixerHead.hasHandle())
            {
                FMOD.ChannelGroup master;
                RuntimeManager.CoreSystem.getMasterChannelGroup(out master);
                master.getDSP(0, out debug_mixerHead);
                debug_mixerHead.setMeteringEnabled(false, true);
            }
            
            StringBuilder debug = new StringBuilder();

            FMOD.Studio.CPU_USAGE cpuUsage;
            FMOD.CPU_USAGE cpuUsage_core;
            RuntimeManager.StudioSystem.getCPUUsage(out cpuUsage, out cpuUsage_core);
            debug.AppendFormat("CPU: dsp = {0:F1}%, studio = {1:F1}%\n", cpuUsage_core.dsp, cpuUsage.update);

            int currentAlloc, maxAlloc;
            FMOD.Memory.GetStats(out currentAlloc, out maxAlloc);
            debug.AppendFormat("MEMORY: cur = {0}MB, max = {1}MB\n", currentAlloc >> 20, maxAlloc >> 20);

            int realchannels, channels;
            RuntimeManager.CoreSystem.getChannelsPlaying(out channels, out realchannels);
            debug.AppendFormat("CHANNELS: real = {0}, total = {1}\n", realchannels, channels);

            FMOD.DSP_METERING_INFO outputMetering;
            debug_mixerHead.getMeteringInfo(IntPtr.Zero, out outputMetering);
            float rms = 0;
            for (int i = 0; i < outputMetering.numchannels; i++)
            {
                rms += outputMetering.rmslevel[i] * outputMetering.rmslevel[i];
            }
            rms = Mathf.Sqrt(rms / (float)outputMetering.numchannels);

            float db = rms > 0 ? 20.0f * Mathf.Log10(rms * Mathf.Sqrt(2.0f)) : -80.0f;
            if (db > 10.0f) db = 10.0f;

            debug.AppendFormat("VOLUME: RMS = {0:f2}db\n", db);
            debug_performanceString = debug.ToString();
        }

        public void OnDebugDraw()
        {}
    }
}