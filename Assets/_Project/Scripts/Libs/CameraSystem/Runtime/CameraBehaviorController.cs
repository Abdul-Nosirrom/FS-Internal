using System.Collections.Generic;
using Drawing;
using FS.Player;
using PrimeTween;
using Sirenix.OdinInspector;
using TimeUtils;
using UnityEngine;

namespace FS.CameraSystem
{
    /// <summary>
    /// Base class responsible for controlling player camera state, primarily for updating <see cref="CameraBehavior"/> that
    /// have been added to this camera. Configures the base camera-orbit parameters as well that can be later modified by behaviors
    /// </summary>
    public abstract partial class CameraBehaviorController : MonoBehaviour
    {
        #region Default Camera State

        [Required] public Camera m_camera;// { get; private set; }

        [FoldoutGroup("System"), SerializeField]
        protected bool m_enableInputSmoothing = true;
        [FoldoutGroup("System"), SerializeField, Range(0, 100), EnableIf("m_enableInputSmoothing")]
        protected float m_inputSmoothingSpeed = 5f;
        
        [FoldoutGroup("System"), SerializeField, Range(0, 100)] 
        protected float m_sensitivityX = 2;
        [FoldoutGroup("System"), SerializeField, Range(0, 100)] 
        protected float m_sensitivityY = 0.5f;
        [FoldoutGroup("System"), SerializeField, Range(0, 5)]
        protected float m_obstructionDistanceInterpSpeed = 1f;
        
        [FoldoutGroup("Camera View"), SerializeField, Range(0, 140)]
        protected float m_fov = 90f;
        [FoldoutGroup("Camera View"), SerializeField, Range(0, 10)]
        protected float m_cameraDistance = 5f;
        [FoldoutGroup("Camera View"), SerializeField, MinMaxSlider(-90, 90, true)]
        protected Vector2 m_pitchLimits = new Vector2(-75f, 60f);
        
        /// <summary>
        /// Represents the offset of the camera pivot in the world space (* a locally computed 'basis')
        /// </summary>
        [FoldoutGroup("Camera View"), SerializeField]
        protected Vector3 m_targetOffset = Vector3.zero;
        
        /// <summary>
        /// Represents the transform 'target' of the camera, pivot of the orbit vector
        /// </summary>
        [FoldoutGroup("Camera View"), SerializeField]
        protected Transform m_followTarget;
        public Vector3 FollowTarget => m_followTarget ? m_followTarget.position : transform.position;
        
        public Vector3 PlayerPivot => FollowTarget + m_basis * m_targetOffset;
        //protected virtual Vector3 PlayerPivotLocal => FollowTarget + transform.rotation * m_targetOffset;

        private CameraOrbitVector DefaultCameraVector => new()
        {
            Pivot = PlayerPivot,
            Distance = m_cameraDistance,
            FOV = m_fov,
            PitchLimits = m_pitchLimits,
            Rotation = gameObject.transform.rotation,
            RotationOffset = Quaternion.identity,
            ViewOffset = Vector2.zero
        };
        #endregion

        private CameraState m_cameraState;
        public CameraState CameraState 
        {
            get
            {
                m_cameraState ??= new CameraState(m_camera);
                return m_cameraState;
            }
        }
        
        public CameraOrbitVector m_cameraVector;
        public CameraOrbitVector m_prevCameraVector; // vector of last frame
        
        //public SortedList<CameraBehaviorType, List<CameraBehavior>> m_behaviorByType = new();
        public List<CameraBehavior> m_cameraBehaviors = new();
        
        protected Quaternion m_basis = Quaternion.identity;
        public Quaternion Basis => m_basis;
        
        /// <summary>
        /// Up vector of the camera, which accounts for the cameras basis.
        /// </summary>
        public Vector3 UpVector => Basis * Vector3.up;
        
        /// <summary>
        /// Yaw representing the "zero" rotation, in the sense that the camera points forward in the direction of the player
        /// </summary>
        public float ZeroYaw => AimYaw(transform.forward);

        /// <summary>
        /// Gets the Yaw value corresponding to the given aim direction, relative to the cameras basis forward vector
        /// </summary>
        public float AimYaw(Vector3 aimDir)
        {
            Vector3 projectedForward = Vector3.ProjectOnPlane(aimDir, UpVector).normalized;
            bool isNearlyParallel = Mathf.Abs(Vector3.Dot(transform.up, UpVector)) < 0.1f;

            if (isNearlyParallel)
            {
                // Try using the down vector vector
                projectedForward = Vector3.ProjectOnPlane(-transform.up, UpVector);
            }
                
            Vector3 basisForward = Basis * Vector3.forward;
            float yaw = Vector3.SignedAngle(basisForward, projectedForward, UpVector);
            return yaw;
        }

        protected CameraObstruction m_cameraObstruction;

        protected PlayerCameraSystem m_cameraSystem;
        protected PlayerInputSystem m_input;
        protected Vector2 m_prevLookInputRaw;
        public EulerAngles LookInputDelta { get; private set; }
        public TimeSince TimeSinceLastInput { get; private set; } // TODO: on input system instead?

        /// <summary>
        /// Override this to explicitly declare the behaviors that'll live on your camera
        /// TODO: Just explicitly declare them as serialized fields instead?
        /// </summary>
        protected abstract void SetupBehaviors();
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                m_cameraVector = DefaultCameraVector;
                return;
            }
            
            m_cameraVector = DefaultCameraVector;
            m_camera ??= transform.parent?.GetComponentInChildren<Camera>();
            if (m_camera == null) return;
            if (m_camera.gameObject == gameObject)
            {
                Debug.LogError($"[CameraSystem] Camera should not be on the same gameobject as the controller");
                m_camera = null;
                return;
            }

            m_camera.transform.position = m_cameraVector.ToPosition();
            m_camera.transform.rotation = m_cameraVector.ToRotation();
            m_camera.fieldOfView = m_cameraVector.FOV;
        }
#endif         
        
        protected virtual void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            m_cameraObstruction.Init(CameraState);
            
            // Initialize default orbit vector
            m_cameraVector = DefaultCameraVector;
            m_prevCameraVector = m_cameraVector;
        }

        protected virtual void Start()
        {
            SetupBehaviors();

            m_input = PlayerManager.Instance.GetSubsystem<PlayerInputSystem>(gameObject);
            m_cameraSystem = PlayerManager.Instance.GetSubsystem<PlayerCameraSystem>(gameObject);
            
            m_cameraSystem.OnBeginBlendToPlayerCamera += OnBeginBlendToPlayerCamera;
        }
        
        private void OnBeginBlendToPlayerCamera()
        {
            // Reset rotation to "look forward"
            m_cameraVector.Rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.up);
        }

        protected T AddBehavior<T>(string behaviorName = null) where T : CameraBehavior, new() 
            => AddBehavior(CameraBehavior.Create<T>(this, behaviorName)) as T;
        protected CameraBehavior AddBehavior(CameraBehavior behavior)
        {
            m_cameraBehaviors.Add(behavior);
            return behavior;
        }

        protected virtual void LateUpdate()
        {
            UpdateLookInput();
            
            RunCameraBehaviors();
            
            // Execute orbit vector fx before we apply them to the camera state, then afterwards execute camera state fx
            ExecuteCameraFX(CameraFX.Mode.OrbitVector);
            
            // Finally, update camera state based on computed camera vector
            CameraState.ApplyOrbitVector(m_cameraVector, m_basis);
            
            // Im mixed if we wanna run these on the orbit vectors or the camera states, some effects are difficult to perform
            // with the orbit vector but in other cases its convenient. The way things are ordered means theres no transfer between the two
            ExecuteCameraFX(CameraFX.Mode.CameraState);
            
            // Check for obstructions and adjust camera state accordingly
            EvaluateCameraObstructions();
        }

        protected void UpdateLookInput()
        {
            //LookInputDelta = Quaternion.identity;
            
            Vector2 sCurveInput = m_input.IsController
                ? m_input.LookVector.normalized *
                  Easing.Evaluate(Mathf.Clamp01(m_input.LookVector.magnitude), Ease.InOutQuad)
                : m_input.LookVector;
            
            if (m_enableInputSmoothing)
            {
                sCurveInput = Vector2.Lerp(m_prevLookInputRaw, sCurveInput, Mathf.Clamp01(m_inputSmoothingSpeed * Time.deltaTime));
            }

            m_prevLookInputRaw = sCurveInput;
            
            Vector2 lookInputRaw = sCurveInput * new Vector2(m_sensitivityX, m_sensitivityY) * Time.deltaTime * 5f;
            if (lookInputRaw.sqrMagnitude > 0f) TimeSinceLastInput = 0f;
            var targetLookInput = new EulerAngles(-lookInputRaw.y, lookInputRaw.x, 0f);

            // TODO: This causes weird feeling shit, only actual issue we're facing with euler angles so far rn is this lerp
            LookInputDelta = targetLookInput;//EulerAngles.Lerp(LookInputDelta, targetLookInput, m_enableInputSmoothing ? m_inputSmoothingSpeed * Time.deltaTime : 1f);
        }

        protected void RunCameraBehaviors()
        {
            // We reset offsets to zero
            m_cameraVector.ViewOffset = Vector2.zero;
            m_cameraVector.RotationOffset = Quaternion.identity;
            m_cameraVector.Distance = m_cameraDistance;
            m_cameraVector.FOV = m_fov;
            m_cameraVector.PitchLimits = m_pitchLimits;

            Vector2 pitchLimits = m_pitchLimits;

            // Handle camera activation first
            foreach (var behavior in m_cameraBehaviors)
            {
                if (behavior.ShouldActivate())  behavior.ActivateCamera();
                else                            behavior.DeactivateCamera();
                
                // Any core camera settings should be set here (such as pitch limits)
                if (!behavior.IsActive) continue;
                
                // Pitch limits are used in the rotation setter, so we need to set them here early on & finalize it before the behaviors run
                if (!behavior.IsInheritingCameraValue(CameraBehaviorInheritance.PitchLimits))
                    pitchLimits = Vector2.Lerp(pitchLimits, behavior.BlendTarget.PitchLimits, behavior.GetBlendAlpha());
                
            }
            
            // NOTE: An easy prioritization scheme is via what values are set.
            m_cameraVector.PitchLimits = pitchLimits;
            //Draw.ingame.Label2D(PlayerPivot, $"Pitch Limits: {m_cameraVector.PitchLimits.x} - {m_cameraVector.PitchLimits.y}", Color.yellow);

            
            // Update the cameras
            foreach (var behavior in m_cameraBehaviors)
            {
                behavior.InheritParentCameraValues();
                behavior.UpdateBlendState();

                if (!behavior.IsActive) continue;
                    
                behavior.UpdateCamera(m_cameraVector);

                // TODO: Is the jitter we experience actually a thing or is it due to frame-time hitches?
                m_cameraVector = CameraOrbitVector.Blend(m_cameraVector, behavior.BlendTarget, behavior.GetBlendAlpha());
                    
                // TODO: The pitch limits on the camera vector is being properly set, but its being set too late. If our SkateCam was at the start, it properly sets it
                // for the stuff after. Tho, given that it gets applied at the end (with the intention of overriding), behaviors running before it setting the rotation
                // get their rotations auto clamped to the old pitch limits and that rotation carries over. basically RETHINK HOW TO BEST APPLY PITCH LIMITS!
            }
            m_prevCameraVector = m_cameraVector;
        }
        
        protected void EvaluateCameraObstructions()
        {
            m_cameraObstruction.EvaluateCameraObstruction(m_cameraVector.Pivot, gameObject);
            return;

            if (!m_cameraObstruction.m_isObstructed)
            {
                m_cameraObstruction.m_distanceOnObstruction 
                    = Mathf.Lerp(m_cameraObstruction.m_distanceOnObstruction, 0f, m_obstructionDistanceInterpSpeed * Time.deltaTime);
            }
            else
            {
                m_cameraObstruction.m_distanceOnObstruction 
                    = Mathf.Lerp(m_cameraObstruction.m_lastDistanceOnObstruction, m_cameraObstruction.m_distanceOnObstruction, 2f * m_obstructionDistanceInterpSpeed * Time.deltaTime);
                m_cameraObstruction.m_lastDistanceOnObstruction = m_cameraObstruction.m_distanceOnObstruction;
            }
            
            CameraState.m_position += (CameraState.m_rotation * Vector3.forward) * m_cameraObstruction.m_distanceOnObstruction;
        }
    }
}