using Sirenix.OdinInspector;
using UnityEngine;

namespace FS.CameraSystem
{
    /// <summary>
    /// Component to preview Camera Render FX in the editor
    /// </summary>
    [AddComponentMenu("Free Skies/Camera System/Editor/Camera Render FX Preview")]
    [ExecuteAlways]
    public class CameraRenderFXPreview : MonoBehaviour
    {
        [SerializeReference]
        public CameraRenderingFX CameraRenderFX;
        
        [HorizontalGroup, Button]
        private void StartPreview()
        {
            CameraRenderFX.StartFX(null);
        }

        [HorizontalGroup, Button]
        private void StopPreview()
        {
            CameraRenderFX.StopFX();
        }
    }
}