using System;
using System.Collections.Generic;
using System.Threading;
using FMOD.Studio;
using FMODUnity;
using TimeUtils;
using UnityEngine;
using UnityEngine.Pool;
using Debug = UnityEngine.Debug;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace FS.Audio
{
    public class AudioHandle : IDisposable
    {
        public EventInstance AudioInstance { get; private set; }
        public bool IsValid => AudioInstance.isValid();
        public bool IsPlaying => AudioInstance.isValid() && AudioInstance.getPlaybackState(out var state) == FMOD.RESULT.OK && state is not PLAYBACK_STATE.STOPPED;
        public bool IsStopped => AudioInstance.isValid() && AudioInstance.getPlaybackState(out var state) == FMOD.RESULT.OK && state is PLAYBACK_STATE.STOPPED;
        
        private AudioPool m_pool;
        
        internal TimeSince m_sinceReturnedToPool;
        
        public static AudioHandle Create(EventInstance instance, AudioPool pool) => new AudioHandle(instance, pool);
        public AudioHandle(EventInstance instance, AudioPool pool)
        {
            AudioInstance = instance;
            m_pool = pool;
        }

        public void Play()
        {
            if (!AudioInstance.isValid())
            {
                Debug.LogError("[Audio System] Audio instance is not valid. Cannot play audio.");
                return;
            }
            
            // Callback to release back into the pool when audio stops
            AudioInstance.start();
        }

        public void Stop(STOP_MODE mode = STOP_MODE.IMMEDIATE)
        {
            if (AudioInstance.isValid())
            {
                AudioInstance.stop(mode); // This will invoke OnAudioStopped & release the instance back to the pool
            }
        }
        
        public void Dispose()
        {
            if (AudioInstance.isValid())
            {
                AudioInstance.setUserData(IntPtr.Zero);
                AudioInstance.setCallback(null); // Remove the callback to prevent memory leaks if it was ever set
                AudioInstance.stop(STOP_MODE.IMMEDIATE);
                AudioInstance.release(); // release memory
            }
        }

        public void SetParameters(List<AudioParameter> audioEvtParameters)
        {
            if (!AudioInstance.isValid())
            {
                Debug.LogError("[Audio System] Audio instance is not valid. Cannot set parameters.");
                return;
            }
            
            foreach (var param in audioEvtParameters)
            {
                if (AudioInstance.setParameterByName(param.Name, param.Value) != FMOD.RESULT.OK)
                {
                    Debug.LogWarning($"[Audio System] Failed to set parameter '{param.Name}' to {param.Value} on audio instance.");
                }
            }
        }
    }
    
    public class AudioPool : IDisposable
    {
        private ObjectPool<AudioHandle> m_pool;
        private EventReference m_audioEvt;
        
        private List<AudioHandle> m_activeInstances = new List<AudioHandle>();
        private TimeSince m_sinceLastPoolGet;
        
        public AudioPool(EventReference audioEvt, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
        {
            m_audioEvt = audioEvt;
            m_pool = new ObjectPool<AudioHandle>(CreatePooledAudio, GetPooledAudio, ReleasePooledAudio, DestroyPooledAudio, collectionCheck, defaultCapacity, maxSize);
            m_sinceLastPoolGet = 0f;
        }

        public async Awaitable PeriodicCleanupAsync(CancellationToken token)
        {
            try
            {
                // No active instances & been a while since we got anything from the pool? Cleanup the pool
                if (CountActive == 0)
                {
                    if (m_sinceLastPoolGet > 10f) m_pool.Clear();
                    if (m_activeInstances.Count > 0) m_activeInstances.Clear();
                    return;
                }
                
                for (int i = m_activeInstances.Count - 1; i >= 0; i--)
                {
                    var handle = m_activeInstances[i];
                    if (!handle.IsPlaying)
                    {
                        m_activeInstances.RemoveAt(i);
                        m_pool.Release(handle);
                    }
                    
                    // Wait for a frame every 250 instances to not block the main thread
                    if (i % 250 == 0) await Awaitable.NextFrameAsync(token);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Audio System] Error monitoring audio playback: {e}");
            }
        }

        public AudioHandle Get()
        {
            m_sinceLastPoolGet = 0f;
            var handle = m_pool.Get();
            m_activeInstances.Add(handle);
            return handle;
        }

        public void Release(AudioHandle audioInst) => m_pool.Release(audioInst);
        public void Dispose() => m_pool.Dispose();
        public int CountAll => m_pool.CountAll;
        public int CountActive => m_pool.CountActive;
        public int CountInactive => m_pool.CountInactive;
        
        private AudioHandle CreatePooledAudio()
        {
            var instance = RuntimeManager.CreateInstance(m_audioEvt);
            if (!instance.isValid())
            {
                Debug.LogError("[Audio System] Failed to create audio instance for pooling.");
                return null;
            }
            return new AudioHandle(instance, this);
        }
        private void GetPooledAudio(AudioHandle audioInst) {}
        private void ReleasePooledAudio(AudioHandle audioInst)
        {
            if (audioInst.IsValid)
            {
                audioInst.AudioInstance.stop(STOP_MODE.IMMEDIATE); // Use IMMEDIATE for pooling, we call stop on instance to prevent pool release being double invoked
        
                // Reset all parameters to defaults
                EventDescription desc;
                audioInst.AudioInstance.getDescription(out desc);
                int paramCount;
                desc.getParameterDescriptionCount(out paramCount);
        
                for (int i = 0; i < paramCount; i++)
                {
                    PARAMETER_DESCRIPTION paramDesc;
                    desc.getParameterDescriptionByIndex(i, out paramDesc);
                    audioInst.AudioInstance.setParameterByID(paramDesc.id, paramDesc.defaultvalue);
                }
                
                // Reset the user data to null
                audioInst.AudioInstance.setUserData(IntPtr.Zero);
                // Remove any callbacks if they were set
                audioInst.AudioInstance.setCallback(null);
        
                // Clear 3D attributes
                audioInst.AudioInstance.set3DAttributes(RuntimeUtils.To3DAttributes(Vector3.zero));
                audioInst.m_sinceReturnedToPool = 0f; // Reset the timer when returning to pool
            }
        }
        private void DestroyPooledAudio(AudioHandle audioInst) => audioInst.Dispose();

        public override string ToString()
        {
            string poolName = m_audioEvt.ToString();
#if UNITY_EDITOR
            poolName = m_audioEvt.Path;      
#endif            
            return $"-------------------\n" +
                   $"AudioPool: {poolName}\n" +
                   $" - Active Instances: {CountActive}\n" +
                   $" - Inactive Instances: {CountInactive}\n" +
                   $" - Total Instances: {CountAll}\n" +
                   "-------------------";
        }
    }
}