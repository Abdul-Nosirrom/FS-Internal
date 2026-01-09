using System;
using System.Collections;
using System.Collections.Generic;
using FS.Animation;
using FS.Extensions;
using FS.Player;
using Sirenix.OdinInspector;
using TimeUtils;
using UnityEngine;


namespace FS.GameplayActions
{
    #region ACTION INTERFACES
    public interface IActionUpdateReciever
    {
        public void OnUpdate()      {}
        public void OnFixedUpdate() {}
        public void OnLateUpdate()  {}
    }
    
    public interface IActionCollisionReciever
    {
        public void OnCollisionEnter(Collision other)   {}
        public void OnCollisionStay(Collision other)    {}
        public void OnCollisionExit(Collision other)    {}
        public void OnTriggerEnter(Collider other)      {}
        public void OnTriggerStay(Collider other)       {}
        public void OnTriggerExit(Collider other)       {}
    }

    public interface IActionGizmoReciever
    {
        public void OnDrawActionGizmos(bool isSelected) {}
    }
    #endregion
    
    /// <summary>
    /// Small organized isolated class for gameplay actions that can be granted to an owner of any type (usually an ActionController,
    /// but that is not enforced).
    /// </summary>
    public abstract class GameplayAction : MonoBehaviour
    {
        /// <summary>
        /// Internal name used to identify the action within an action-set, set to the field name of the action in the set
        /// </summary>
        [HideInInspector] public string actionName = $"[Unassigned]";
        
        protected ActionController m_actionController;
        protected PhysicsController m_physics;
        protected FSAnimator m_animator;
        protected PlayerInputSystem m_input;
        
        private void Awake()
        {
            m_actionController = GetComponentInParent<ActionController>();
            m_physics = GetComponentInParent<PhysicsController>();
            m_animator = m_actionController.GetComponentInChildren<FSAnimator>(); // NOTE: Hierarchy is wack because of where skin is and all that shit
        }

        private void Start()
        {
            // Cache player systems in Start instead of awake as they havent been initialized yet in awake
            var player = GetComponentInParent<Player.Player>();
            if (player)
            {
                m_input = PlayerManager.GetSystem<PlayerInputSystem>(player.gameObject);
            }
        }
        
        
        // TODO: There's more shit we can end up doing here to better utilize our new monobehavior conversion
        private void OnDisable() => ClearCoroutines();
        
        #region Coroutine Utility

        private readonly HashSet<IEnumerator> m_coroutineCache = new();

        private void ClearCoroutines()
        {
            // Stop and dispose of corotuines (they get stopped when disabled so we need to make sure we dispose in case try/finally cleanups)
            foreach (var coroutine in m_coroutineCache)
            {
                if (coroutine != null) this.StopAndDisposeCoroutine(coroutine);
            }
            m_coroutineCache.Clear();
        }
        
        /// <summary>
        /// Start a coroutine that gets cached, to ensure we properly dispose of the IEnumerator in the event that the coroutine
        /// would end suddenly allowing for cleanup in try/finally blocks (onDisable/onDestroy)
        /// </summary>
        /// <param name="coroutine"></param>
        protected void StartActionCoroutine(IEnumerator coroutine)
        {
            m_coroutineCache.Add(coroutine);
            StartCoroutine(coroutine);
        }

        /// <summary>
        /// Stops and disposes of a cached action coroutine. Finally block cleanup will run after if declared in the IEnumerator
        /// </summary>
        protected void EndActionCoroutine(IEnumerator coroutine)
        {
            this.StopAndDisposeCoroutine(coroutine);
            m_coroutineCache.Remove(coroutine);
        }
        
        #endregion

#if UNITY_EDITOR
        private void Reset()
        {
            actionName = $"[UnAssigned] {GetType().Name}";
        }
#endif         

        public override string ToString() => actionName;

        #region ACTION API
        /// <summary>
        /// The channels this action will be registered to. Each channel can only have 1 action registered to it
        /// that is active at a time. For example, if an action occupies Channel.Movement | Channel.Rotation, that
        /// means that any existing action that occupied either of those channels will be deactivated.
        /// </summary>
        public abstract ActionChannel Channels { get; }

        // NOTE: Maybe we dont have this here?
        /// <summary>
        /// Optional. The default constraint for this action. If provided, this constraint will be activated
        /// when the action is started, and deactivated when the action is stopped. 
        /// </summary>
        public virtual ActionConstraintBase DefaultConstraint => null;

        /// <summary>
        /// Whether this action can be retriggered while it is active. This would cause it to cleanup
        /// then restart the action.
        /// </summary>
        public virtual bool IsRetriggerable => false;

        /// <summary>
        /// The parent gameplay action that this started as a follow-up to (Can be null)
        /// Actions may have multiple parents/transition points, during the active period of an action,
        /// the parent action will be just the one that led to this.
        /// </summary>
        public GameplayAction m_parentAction { get; private set; }

        /// <summary>
        /// The gameplay action that interrupted this action, if any. Set by the <see cref="ActionChannelContainer"/>
        /// when an action is started that shares similar channels and results in its interruption. Gets set before
        /// <see cref="OnEnd"/> is called on this action.
        /// </summary>
        public GameplayAction m_interruptorAction { get; private set; }
        
        /// <summary>
        /// Called when the action is initially granted to the owner
        /// </summary>
        public virtual void OnInitialize(GameObject owner)         {}

        public virtual void OnStart()                           {}
        
        public virtual void OnEnd()                             {}

        protected virtual bool StartCondition() => false;

        #endregion
        
        #region ACTION STATE & EVENTS

        public Action<GameplayAction> OnActionStarted;
        public Action<GameplayAction> OnActionEnded;
        public Func<GameplayAction, bool> OnActionConditionEvaluation;
        
        public TimeSince m_timeSinceStarted { get; private set; }
        public TimeSince m_timeSinceEnded { get; private set; }
        
        public bool IsActive { get; private set; }

        public bool CanCancel { protected get; set; } = true;

        // TODO: Perhaps review the CanStart/TryStart sort of functionality. CanStart doesn't check constraints, might be nice to add some default return failure messages (e.g StartCondition Failed/Constraint Blocked/Not Retriggerable)
        public bool CanStartAction() => isActiveAndEnabled && !(IsActive && !IsRetriggerable) && StartCondition();
        public bool CanEndAction() => IsActive && CanCancel;
        
        public bool TryStartAction(GameplayAction parent = null)
        {
            //if (!isActiveAndEnabled) return false;
            if (!CanStartAction()) return false;
            if (!OnActionConditionEvaluation?.Invoke(this) ?? false) return false;

            // Possibility to restart the action if retriggerable?
            if (IsRetriggerable && IsActive && !TryEndAction()) return false;
            
            m_parentAction = parent;
            m_interruptorAction = null; // Reset interruptor
            
            IsActive = true;
            m_timeSinceStarted = 0;

            OnStart();
            OnActionStarted?.Invoke(this);

            return true;
        }

        // TODO: May be good to have 2 seperate "End Action" APIs:
        // 1. TryEndAction: Call if the intent is to "gracefully" 'complete' the action
        // 2. TryInterruptAction: Call if the intent is to end the action before it was meant to end
        public bool TryEndAction(GameplayAction interruptor = null)
        {
            if (!CanEndAction()) return false;
            m_interruptorAction = interruptor;
            EndAction();
            return true;
        }

        protected void EndAction()
        {
            // Already in-active, no use in trying to end it
            if (!IsActive) return;
            
            IsActive = false;
            m_timeSinceEnded = 0;

            OnEnd();
            OnActionEnded?.Invoke(this);
        }
        
        #endregion
    }
}