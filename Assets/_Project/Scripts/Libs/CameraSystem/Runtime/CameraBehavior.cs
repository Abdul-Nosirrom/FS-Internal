using System;
using System.Numerics;
using PrimeTween;
using Sirenix.OdinInspector;
using TimeUtils;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace FS.CameraSystem
{
    /// <summary>
    /// 'Channel' of a behavior. Behaviors are sorted by type and executed in the order of the type enum declaration.
    /// </summary>
    public enum CameraBehaviorType
    {
        Regular = 1 << 0,
        PostProcess = 1 << 1
    }
    
    [Flags]
    public enum CameraBehaviorInheritance
    {
        None = 0,
        Pivot = 1 << 0,
        Rotation = 1 << 1,
        Distance = 1 << 2,
        FOV = 1 << 3,
        ViewOffset = 1 << 4,
        RotationOffset = 1 << 5,
        PitchLimits = 1 << 6,
        All = Pivot | Rotation | Distance | FOV | ViewOffset | RotationOffset | PitchLimits
    }
    
    /// <summary>
    /// Abstract base class for all camera behaviors. Behaviors are used to modify the player cameras orbit vector
    /// with proper blending. Contains a set of events that will be invoked by the CameraBehaviorController.
    ///
    /// Behaviors are organized by type and are executed in the order of the type enum declaration. Additionally,
    /// behaviors of a given type are sorted by their priority. Meaning that higher priority behaviors can override lower
    /// priority ones during behavior execution.
    /// </summary>
    public abstract class CameraBehavior
    {
        /// <summary>
        /// Behaviors are executed in order of priority, going from lowest priority to highest priority. Meaning higher
        /// priority behaviors can override lower priority behaviors.
        /// </summary>
        [BoxGroup("Behavior Parameters")] public uint Priority = 0;
        /// <summary>
        /// 'Channel' of this behavior. Behaviors are sorted by type and executed in the order of the type enum declaration.
        /// </summary>
        [BoxGroup("Behavior Parameters")] public CameraBehaviorType BehaviorType = CameraBehaviorType.Regular;
        /// <summary>
        /// Which OrbitVector parameters to inherit from the current state of the player CameraVector before running this behavior
        /// </summary>
        [BoxGroup("Behavior Parameters")] public CameraBehaviorInheritance InheritedCameraValues = CameraBehaviorInheritance.All;

        [BoxGroup("Behavior Parameters")] public CameraBlendParams BlendInParams = CameraBlendParams.None;
        [BoxGroup("Behavior Parameters")] public CameraBlendParams BlendOutParams = CameraBlendParams.None;
        
        public Vector3 UpVector => m_cameraController.UpVector;
        public float ZeroYaw => m_cameraController.ZeroYaw;

        public Vector3 CameraRight => m_cameraVector.Rotation.ToQuaternion() * Vector3.right;
        public Vector3 CameraForward => m_cameraVector.Rotation.ToQuaternion() * Vector3.forward;
        public Vector3 CameraUp => m_cameraVector.Rotation.ToQuaternion() * Vector3.up;
        
        protected CameraBehaviorController m_cameraController { get; private set; }
        protected CameraOrbitVector m_cameraVector;

        private string m_name = null;
        public string Name => m_name ?? GetType().Name.Replace("CameraBehavior_", "");

        public static T Create<T>(CameraBehaviorController camera, string name = null) where T : CameraBehavior, new()
        {
            var behavior = new T();
            behavior.m_cameraController = camera;
            behavior.m_name = name;
            behavior.Initialize();
            return behavior;
        }
        
        #region Activation State
        public bool IsActive { get; private set; }
        
        public void ActivateCamera()
        {
            // Already active and NOT blending out
            if (IsActive && !IsBlendingOut) return;
            
            m_blendStartCameraVector = m_cameraVector;
            IsActive = true;
            IsBlendingOut = false;

            m_blendInTimer = 0;
            
            OnCameraActivated();
        }
        
        public void DeactivateCamera()
        {
            // Already inactive or currently blending out 
            if (!IsActive || IsBlendingOut) return;
	
            m_blendStartCameraVector = m_cameraController.m_cameraVector; // NOTE: Why not m_orbitVector here?
            IsBlendingOut = BlendOutParams.m_duration > 0f;
            m_blendOutTimer = 0;
            
            if (!IsBlendingOut)
            {
                IsActive = false;
                OnCameraDeactivated();
            }
        }

        #endregion 
        
        public bool IsInheritingCameraValue(CameraBehaviorInheritance inheritance) => (InheritedCameraValues & inheritance) != 0;
        public void InheritParentCameraValues()
        {
            // Update inherited data
            if (IsInheritingCameraValue(CameraBehaviorInheritance.Pivot))
            {
                m_cameraVector.Pivot = m_cameraController.m_cameraVector.Pivot;
                // A must! The rest are fine to keep as they are but the pivot is important to keep updating
                // Otherwise we may lose view of the player
                m_blendStartCameraVector.Pivot = m_cameraController.m_cameraVector.Pivot;
            }
            if (IsInheritingCameraValue(CameraBehaviorInheritance.Distance)) m_cameraVector.Distance = m_cameraController.m_cameraVector.Distance;
            if (IsInheritingCameraValue(CameraBehaviorInheritance.Rotation)) m_cameraVector.Rotation = m_cameraController.m_cameraVector.Rotation;
            if (IsInheritingCameraValue(CameraBehaviorInheritance.FOV)) m_cameraVector.FOV = m_cameraController.m_cameraVector.FOV;
            if (IsInheritingCameraValue(CameraBehaviorInheritance.ViewOffset)) m_cameraVector.ViewOffset = m_cameraController.m_cameraVector.ViewOffset;
            if (IsInheritingCameraValue(CameraBehaviorInheritance.RotationOffset)) m_cameraVector.RotationOffset = m_cameraController.m_cameraVector.RotationOffset;
            if (IsInheritingCameraValue(CameraBehaviorInheritance.PitchLimits)) m_cameraVector.PitchLimits = m_cameraController.m_cameraVector.PitchLimits;
        }

        #region Blending State

        public bool IsBlendingOut { get; private set; }
        public bool IsBlendingIn => IsActive && !IsBlendingOut && m_blendInTimer < BlendInParams.m_duration;
        
        public CameraOrbitVector BlendTarget => IsBlendingOut && BlendOutParams.m_shouldLockOutgoing ? 
            m_blendStartCameraVector : m_cameraVector;

        private CameraOrbitVector m_blendStartCameraVector;

        private TimeSince m_blendInTimer;
        private TimeSince m_blendOutTimer;

        public float GetBlendAlpha()
        {
            // Not blending (due to not being active or just being fully blended in)
            if (!IsActive) return 0f;
            return IsBlendingOut ? 1 - BlendOutParams.Evaluate(m_blendOutTimer) : BlendInParams.Evaluate(m_blendInTimer);
        }
        
        /// <summary>
        /// Called at the start of the behavior update loop. Ensures the camera deactivates itself if blending out
        /// and the blend out is completed. This is only ever called when the camera is active!
        /// </summary>
        public void UpdateBlendState()
        {
            // Update blend state
            if (IsBlendingOut && GetBlendAlpha() == 0f)
            {
                IsBlendingOut = false;
                IsActive = false;
                OnCameraDeactivated();
            }
        }
        
        #endregion
        
        #region Events API
        protected virtual void Initialize() {}
        public virtual bool ShouldActivate() => false;
        protected virtual void OnCameraActivated() {}
        protected virtual void OnCameraDeactivated() {}
        public virtual void UpdateCamera(in CameraOrbitVector inOrbitVector) {}
        #endregion
        
        public virtual void OnDrawCameraGizmos(CameraBehaviorController camera) {}
        public virtual void OnCameraGUI(CameraBehaviorController camera) {}
    }
}