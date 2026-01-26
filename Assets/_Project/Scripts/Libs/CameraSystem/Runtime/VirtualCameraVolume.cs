using FS.Player;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FS.CameraSystem
{
    [AddComponentMenu("Free Skies/Camera/Camera Trigger Volume")]
    [RequireComponent(typeof(VirtualCamera))]
    public class VirtualCameraVolume : MonoBehaviour, ISelfValidator
    {
        [SerializeField] private CameraBlendParams BlendInParams = CameraBlendParams.Create(0.4f, Ease.InOutQuad, false);
        [SerializeField] private CameraBlendParams BlendOutParams  = CameraBlendParams.Create(0.4f, Ease.InOutQuad, true);
        
        private VirtualCamera m_virtualCamera;
        
        private void Awake()
        {
            m_virtualCamera = GetComponent<VirtualCamera>();
        }

        private void OnTriggerEnter(Collider other)
        {
            var cameraSystem = PlayerManager.GetSystem<PlayerCameraSystem>(other.gameObject);
            if (cameraSystem != null)
            {
                cameraSystem.BlendToCamera(m_virtualCamera,  BlendInParams);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var cameraSystem = PlayerManager.GetSystem<PlayerCameraSystem>(other.gameObject);
            if (cameraSystem != null)
            {
                cameraSystem.PopVirtualCamera(m_virtualCamera, BlendOutParams);
            }
        }

        public void Validate(SelfValidationResult result)
        {
            // Do we have a trigger volume?
            if (!TryGetComponent<Collider>(out var trigger) || !trigger.isTrigger)
                result.AddWarning($"[Camera Volume] This camera volume doesn't have a collider that's set to trigger attached. Won't trigger.").SetSelectionObject(this);
        }
    }
}