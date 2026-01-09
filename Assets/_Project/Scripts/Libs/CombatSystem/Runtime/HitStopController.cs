using System.Collections;
using FS.Animation;
using FS.Utility;
using UnityEngine;

namespace FS.CombatSystem
{
    // TODO: Could handle more cases, like pausing particle systems, etc.
    public class HitStopController : MonoBehaviour
    {
        public bool HitStopActive => m_activeHitStop != null;
        private Animator m_animator;
        private Rigidbody m_rigidbody;
        private PhysicsController m_physics;

        private Coroutine m_activeHitStop;
        
        private void Start()
        {
            m_animator = GetComponentInChildren<Animator>();
            m_rigidbody = GetComponent<Rigidbody>();
            m_physics = GetComponent<PhysicsController>();
        }

        public void TriggerHitStop(HitStopStrength duration)
        {
            if (m_activeHitStop != null) StopCoroutine(m_activeHitStop);
            m_activeHitStop = StartCoroutine(HitStop(CombatService.HitStopDurations[duration]));
        }

        private void SetHitStopState(bool isOn)
        {
            // Temp: while i figure out how to do this properly
            Time.timeScale = !isOn ? 1f : 0f;
            //return;
            //if (m_animator != null)
            //    m_animator.enabled = !isOn;
            //    //m_animator.speed = isOn ? 0f : 1f;
            //
            //if (m_physics)
            //    m_physics.IsKinematic = isOn;
            //else if (m_rigidbody != null)
            //    m_rigidbody.isKinematic = isOn;
        }

        private IEnumerator HitStop(float duration)
        {
            var preHitstopVel = m_physics ? m_physics.Velocity : Vector3.zero;
            SetHitStopState(true);
            yield return Yields.WaitForSecondsRealtime(duration);
            SetHitStopState(false);
            if (m_physics) m_physics.Velocity = preHitstopVel;
            //if (false && m_rigidbody != null)
            //{
            //    if (m_physics) m_physics.Velocity = preHitstopVel;
            //    else m_rigidbody.linearVelocity = preHitstopVel;
            //}
            
            m_activeHitStop = null;
        }
    }
}