using System.Collections.Generic;
using UnityEngine;

namespace FS.Animation
{
    // TODO: I dont think we handle blend-trees well at all, not sure if its possible?
    public class MecanimAnimationEvents : StateMachineBehaviour
    {
        private enum EventExecutionState
        {
            NotStarted,
            Executing,
            Completed
        }
        
        [SerializeField] private AnimationEventHolder m_animEvents;

        private Dictionary<FSAnimationEvent, EventExecutionState> m_eventTriggerState = new();
        private GameObject m_ownerObject;
        private bool m_isLooping;

        // TODO: We cant reliably know when the mecanim state enters/exits (the controller state sure, but if we've got state machines and shit we cant i dont think?)
        // public void Initialize(GameObject owner, ControllerState state)
        // {
        //     m_ownerObject = owner;
        //     
        //     state.BindPlaybackEvent(InitializeEventState, AnimationPlaybackEventManager.Type.BeginFadeIn);
        //     state.BindPlaybackEvent(TryTriggerEndEvents, AnimationPlaybackEventManager.Type.BeginFadeOut);
        //     
        //     InitializeEventState();
        // }
        //
        private void InitializeEventState()
        {
            m_eventTriggerState.Clear();
            foreach (var animEvent in m_animEvents.Events) m_eventTriggerState.Add(animEvent, EventExecutionState.NotStarted);
        }
        //
        // private void TryTriggerEndEvents() => OnStateExit(null, default, 0);

        private float GetNormalizedTime(AnimatorStateInfo stateInfo)
        {
            if (stateInfo.loop) return stateInfo.normalizedTime % 1f;
            return Mathf.Clamp01(stateInfo.normalizedTime);
        }
        
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            m_ownerObject = animator.gameObject;
            m_prevNormTime = GetNormalizedTime(stateInfo); // Initialize previous normalized time

            InitializeEventState();
            
            // Trigger all 'start' events
            foreach (var animEvent in m_animEvents.Events) TryExecuteEvent(animEvent, 0);
        }

        private float m_prevNormTime = 0f;
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            float normalizedTime = GetNormalizedTime(stateInfo);
            // Reset triger states if we looped
            if (normalizedTime < m_prevNormTime && stateInfo.loop)
            {
                CompleteCurrentCycle(); // Call exit events
                foreach (var animEvent in m_animEvents.Events) TryExecuteEvent(animEvent, 0); // call start events
            }
            else
            {
                foreach (var animEvent in m_animEvents.Events) TryExecuteEvent(animEvent, normalizedTime);
            }

            m_prevNormTime = normalizedTime;
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            CompleteCurrentCycle();
        }

        /// <summary>
        /// Fires any remaining events at time=1 and resets state.
        /// Used both for loop boundaries and actual state exit.
        ///
        /// Wanna figure out a way to call this when the MecanimAnimation fades out for any active states
        /// </summary>
        private void CompleteCurrentCycle()
        {
            // NOTE: This doesn't properly trigger events if we exit the MecanimAnimation
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
            var eventState = m_eventTriggerState[animEvent];
            float start = animEvent.TimeRange.x;
            float end = animEvent.TimeRange.y;
            float triggerTime = animEvent.IsRangedEvent ? start : animEvent.Time;
            
            float normalizedEventTime = Mathf.Repeat(normalizedTime - triggerTime, 1f);
            float normalizedTimeDelta = normalizedEventTime;

            switch (eventState)
            {
                case EventExecutionState.NotStarted:
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
                    //if (normalizedTime < end) // NOTE: We don't execute "Excecute" continuously for ranged events
                    //    animEvent.Execute(m_ownerObject, normalizedEventTime);
                    //else
                    {
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