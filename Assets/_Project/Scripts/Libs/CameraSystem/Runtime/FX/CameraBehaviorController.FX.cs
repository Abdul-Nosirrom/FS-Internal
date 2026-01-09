using System.Collections.Generic;
using UnityEngine;

namespace FS.CameraSystem
{
    public partial class CameraBehaviorController
    {
        private List<CameraFX> m_activeCameraFX = new List<CameraFX>();
        
        public bool AddCameraFX(CameraFX fx)
        {
            if (fx == null)
            {
                Debug.LogError($"[Camera System] Trying to add null CameraFX to CameraBehaviorController on {name}");
                return false;
            }

            if (m_activeCameraFX.Contains(fx))
                Debug.LogWarning($"[Camera System] Trying to add CameraFX {fx.GetType().Name} that is already active on CameraBehaviorController on {name}");
            else
            {
                m_activeCameraFX.Add(fx);
            }
            
            fx.StartFX(this);
            return true;
        }
        
        public T AddCameraFX<T>() where T : CameraFX, new()
        {
            var fx = new T();
            AddCameraFX(fx);
            return fx;
        }

        public bool RemoveFX(CameraFX fx)
        {
            if (fx == null)
            {
                Debug.LogError($"[Camera System] Trying to remove null CameraFX from CameraBehaviorController on {name}");
                return false;
            }
            if (!m_activeCameraFX.Contains(fx))
            {
                Debug.LogWarning($"[Camera System] Trying to remove CameraFX {fx.GetType().Name} that is not active on CameraBehaviorController on {name}");
                return false;
            }
            
            fx.StopFX();
            if (fx is CameraRenderingFX renderFX) 
                renderFX.Dispose();
            
            m_activeCameraFX.Remove(fx);
            
            return true;
        }

        private HashSet<int> m_fxToRemove = new();
        public void ExecuteCameraFX(CameraFX.Mode fxMode)
        {
            m_fxToRemove.Clear();
            
            for (int fxIndex = 0; fxIndex < m_activeCameraFX.Count; fxIndex++)
            {
                if (m_activeCameraFX[fxIndex].FXMode != fxMode)
                    continue;
                
                var fx = m_activeCameraFX[fxIndex];
                if (fx == null || !fx.IsActive)
                {
                    m_fxToRemove.Add(fxIndex);
                    continue;
                }

                fx.OnUpdateFX();
            }
            
            // Remove any FX that are no longer active
            foreach (var fxIndex in m_fxToRemove)
                RemoveFX(m_activeCameraFX[fxIndex]);
        }
    }
}