using System.Collections.Generic;
using UnityEngine;

namespace FS.Animation
{
    // TODO: I dont think we handle blend-trees well at all, not sure if its possible?
    // TODO: We cant reliably know when the mecanim state enters/exits (the controller state sure, but if we've got state machines and shit we cant i dont think?)
    // TODO: Can we use the overload for the state functions that takes in a PlayableAnimatorController? Would it allow us to fetch a ref to the RuntimeAnimatorController to avoid the OnValidate serialization here?
    public class MecanimAnimationEvents : StateMachineBehaviour
    {
        private enum EventExecutionState
        {
            NotStarted,
            Executing,
            Completed
        }
        
        [SerializeField] private AnimationEventHolder m_animEvents;
      
        /// <summary>
        /// Runtime animation controller reference needed for ControllerState registration to invoke end events if a fade out happens early
        /// </summary>
        [SerializeField, HideInInspector] private RuntimeAnimatorController m_ownerController;

#if UNITY_EDITOR
        private void OnValidate()
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(this);
            var controller = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path) as RuntimeAnimatorController;
            if (controller != null && m_ownerController != controller)
            {
                m_ownerController = controller;
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif

        private Dictionary<FSAnimationEvent, EventExecutionState> m_eventTriggerState = new();
        private Animator m_animator;
        private GameObject m_ownerObject;
        private bool m_isLooping;

        
        private void InitializeEventState()
        {
            m_eventTriggerState.Clear();
            foreach (var animEvent in m_animEvents.Events) m_eventTriggerState.Add(animEvent, EventExecutionState.NotStarted);
        }

        private bool m_wasFadeOutConsumed = true;

        private void BindToControllerStateFadeOut(Animator animator)
        {
            // Register to a global/static buffer so that the underlying AnimancerState knows to call OnStateEnd
            if (MecanimAnimation.TryGetRegisteredControllerState(animator, m_ownerController, out var controllerState))
            {
                if (m_wasFadeOutConsumed) // Only bind once, given that this is "Whole ass controller is fading out" so state re-entry can rebind
                {
                    m_wasFadeOutConsumed = false;
                    controllerState.OnFadedOut(() => // TODO: IDEALLY WOULD USE ONFADEOUT BUT ITS BROKEN CHECK @AnimationPlaybackEvents.cs
                    {
                        CompleteCurrentCycle();
                        m_wasFadeOutConsumed = true;
                    });
                }
            }
        }

        private float GetNormalizedTime(AnimatorStateInfo stateInfo)
        {
            if (stateInfo.loop) return stateInfo.normalizedTime % 1f;
            return stateInfo.normalizedTime;//Mathf.Clamp01(stateInfo.normalizedTime);
        }

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            m_animator = animator;
            m_ownerObject = animator.gameObject;
            m_prevNormTime = GetNormalizedTime(stateInfo); // Initialize previous normalized time

            InitializeEventState();
            BindToControllerStateFadeOut(animator);

            m_isLooping = stateInfo.loop;
            
            // Trigger all 'start' events
            foreach (var animEvent in m_animEvents.Events) TryExecuteEvent(animEvent, 0);
        }

        private float m_prevNormTime = 0f;
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            float normalizedTime = GetNormalizedTime(stateInfo);
            
            // Fetch controller-state and check if it's fading out, just a safety check as the FadedOut binding wont invoke
            // if we fade out same frame we bind, it'll wait till the next invocation
            if (MecanimAnimation.TryGetRegisteredControllerState(m_animator, m_ownerController, out var cState))
            {
                float targetWeight = cState.TargetWeight * cState.Layer.TargetWeight;
                if (targetWeight < 1)
                {
                    CompleteCurrentCycle();
                    m_prevNormTime = normalizedTime;
                    return;
                }
            }
            
            // Reset triger states if we looped
            if (normalizedTime < m_prevNormTime && stateInfo.loop)
            {
                CompleteCurrentCycle(); // Call exit events
                foreach (var animEvent in m_animEvents.Events) TryExecuteEvent(animEvent, 0); // call start events
            }
            // Can be cross-fading out but normalized time is still updating: Happens in a case of a state w/ no explicit transitions so it lingers. End is called, resetting it
            else if (normalizedTime <= 1 && m_prevNormTime <= 1) // For loops, this'll always be true. Just for non-looping during cross-fades we don't clamp to detect this (FadeOut can call CompleteCycle, resetting states, then same frame instantly invoked)
            {
                foreach (var animEvent in m_animEvents.Events) TryExecuteEvent(animEvent, normalizedTime);
            }

            m_prevNormTime = normalizedTime;
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            CompleteCurrentCycle(); // True here causes no events to fire for some reason, this is same as old behavior, true is fine for fade out call
        }

        /// <summary>
        /// Fires any remaining events at time=1 and resets state.
        /// Used both for loop boundaries and actual state exit.
        ///
        /// We handle MecanimAnimation fadeout cleaning up events by having a static registry for controller state references,
        /// and fetching them early on to bind to their fade out events so all internal AnimationController states get cleaned up properly.
        /// </summary>
        private void CompleteCurrentCycle()
        {
            foreach (var animEvent in m_animEvents.Events)
            {
                if (m_eventTriggerState[animEvent] == EventExecutionState.Executing)
                    TryExecuteEvent(animEvent, 1);

                // Clear event status as we're done and want it clean for next time
                m_eventTriggerState[animEvent] = EventExecutionState.NotStarted;
            }
        }
        
        private void TryExecuteEvent(FSAnimationEvent animEvent, float normalizedTime)
        {
            if (!m_eventTriggerState.TryGetValue(animEvent, out var eventState) && normalizedTime < 1)
            {
                // OnStateUpdate fired without OnStateEnter — reinitialize
                InitializeEventState();
                eventState = m_eventTriggerState[animEvent];
            }
            
            float start = animEvent.TimeRange.x;
            float end = animEvent.TimeRange.y;
            float triggerTime = animEvent.IsRangedEvent ? start : animEvent.Time;
            
            float normalizedEventTime = Mathf.Repeat(normalizedTime - triggerTime, 1f);
            float normalizedTimeDelta = normalizedEventTime;

            switch (eventState)
            {
                case EventExecutionState.NotStarted:
                    //if (animEvent.Event.Name.Contains("vfx_Wall"))
                    //    Debug.Log($"[Mecanim] Starting VFX! t_cur={normalizedTime:F2} | t_prev={m_prevNormTime:F2}");
                    if (normalizedTime >= triggerTime)
                    {
                        if (animEvent.IsRangedEvent)
                        {
                            animEvent.Start(m_ownerObject);
                            m_eventTriggerState[animEvent] = EventExecutionState.Executing;
                        }
                        else
                        {
                            // Running into an issue with either triggering multiple times or triggering if fade-out duration exceeds state length so i think the repeat of time lets an event at the start play again if the fade out duration goes over the anim length
                            // Solving it by just ensuring we're not super far off from the suggested trigger time
                            if (normalizedTimeDelta <= 0.05f) // TODO: Framerate issues could cause skipping events, temp until a more robust solution can be figured out
                            {
                                animEvent.Execute(m_ownerObject, normalizedEventTime);
                                m_eventTriggerState[animEvent] = EventExecutionState.Completed;
                            }
                        }
                    }
                    break;
                case EventExecutionState.Executing: // Guaranteed range event
                    if (normalizedTime >= end)
                    {
                        //if (animEvent.Event.Name.Contains("vfx_Wall"))
                        //    Debug.Log($"[Mecanim] Ending VFX! t_cur={normalizedTime:F2} | t_prev={m_prevNormTime:F2}");
                        animEvent.End(m_ownerObject);
                        m_eventTriggerState[animEvent] = EventExecutionState.Completed;
                    }
                    break;
                case EventExecutionState.Completed:
                    break;
            }
        }
    }
}