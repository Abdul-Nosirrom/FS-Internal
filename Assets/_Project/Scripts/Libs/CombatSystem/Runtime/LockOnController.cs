using System;
using Drawing;
using FS.Player;
using TimeUtils;
using UnityEngine;
using UnityEngine.Assertions;

namespace FS.CombatSystem
{
    public class LockOnController : MonoBehaviour
    {
        public event Action OnLockOnStarted;
        public event Action OnLockOnEnded;
        
        [SerializeField]
        protected TargetingSettings m_lockOnSettings;

        protected bool m_allowTargetSwapping;
        
        protected bool m_isLockOnActive;
        public bool IsLockOnActive => m_isLockOnActive;

        protected bool m_isLockOnAllowed;
        public bool IsLockOnAllowed => m_isLockOnAllowed;

        protected TargetingResult m_currentTarget;

        protected PlayerInputSystem m_input;
        
        private TargetingQuery m_lastTarget;
        private TimeSince m_sinceLastTargetSwap;
        public float TimeSinceLastTargetSwap => m_sinceLastTargetSwap;
        
        private void Start()
        {
            m_lockOnSettings.Instigator = gameObject;

            m_input = PlayerManager.GetSystem<PlayerInputSystem>(gameObject);
            Assert.IsNotNull(m_input, $"LockOnController on {gameObject.name} requires a PlayerInputSystem to function properly. Please ensure component is on a player.");
        }

        private void LateUpdate()
        {
            if (m_lastTarget != m_currentTarget.Target)
            {
                m_lastTarget = m_currentTarget.Target;
                m_sinceLastTargetSwap = 0f; // Reset the timer when we change targets
            }
            
            if (IsLockOnActive) 
            {
                UpdateLockOn();
            }

            if (m_input == null)
            {
                Debug.LogError("Input System is null! Please ensure PlayerInputSystem is set up correctly.");
                return;
            }

            if (m_input.GetButton(GameInput.LockOn))
            {
                if (IsLockOnActive) EndLockOn();
                else StartLockOn();
                m_input.ConsumeInput(GameInput.LockOn);
            }
        }

        public bool ValidateExistingTarget()
        {
            // Do we have a target set for us? Check if it's valid first
            if (!m_lockOnSettings.IsTargetValid(m_currentTarget.Target, out _, out _, out _, out _))
            {
                return false;// dont switch to a different target if the current one is invalid, just leave lock - on
                if (!m_lockOnSettings.PeformTargeting(gameObject, out m_currentTarget)) return false;
            }

            return true;
        }
        
        public bool StartLockOn()
        {
            // Already locked on
            if (m_isLockOnActive)
            {
                Debug.LogWarning($"Attempting to retrigger lock-on while already locked on. This is not allowed.");
                return false;
            }

            m_lockOnSettings.PeformTargeting(gameObject, out m_currentTarget);
            if (!ValidateExistingTarget()) return false;

            m_isLockOnActive = true;
            OnLockOnStarted?.Invoke();

            return true;
        }

        public void UpdateLockOn()
        {
            // Has our current target been invalidated? If so can we find a new one?
            if (!ValidateExistingTarget()) 
            {
                EndLockOn();
                return;
            }

            Draw.ingame.Circle(m_currentTarget.Target.transform.position,
                Camera.main.transform.rotation * Vector3.forward, 0.5f);

            TryCycleLockOnTargets();
        }

        public void EndLockOn()
        {
            if (!m_isLockOnActive)
            {
                Debug.LogWarning("Attempting to end lock-on while not locked on. This is not allowed.");
                return;
            }
            
            m_isLockOnActive = false;
            m_currentTarget = default; // Clear the current target
            OnLockOnEnded?.Invoke();
        }

        protected void TryCycleLockOnTargets()
        {
            //var dirBias = m_input.GetButton(GameInput.)
            //m_lockOnSettings.PeformTargeting(gameObject, out m_currentTarget, m_currentTarget.Target ? m_currentTarget.Target.gameObject : null);
        }
        
        public void AllowLockOn() => m_isLockOnAllowed = true;
        public void DisableLockOn() => m_isLockOnAllowed = false;
        public bool CanTargetSwap => m_allowTargetSwapping;
        
        public TargetingQuery CurrentLockOnTarget => m_currentTarget.Target;

        public void SetLockOnTarget(TargetingQuery target, bool shouldStartLockOn = false)
        {
            m_currentTarget.Target = target;
            if (shouldStartLockOn) StartLockOn();
        }
    }
}