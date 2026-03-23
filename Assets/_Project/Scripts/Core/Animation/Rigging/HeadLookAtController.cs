using FS.Math;
using PrimeTween;
using TimeUtils;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FS.Animation.Rigging
{
    [AddComponentMenu("Free Skies/Animation/Rigging/HeadLookAtController")]
    public class HeadLookAtController : MonoBehaviour
    {
        public Transform m_lookAtEffector;
        public MultiAimConstraint m_headConstraint;
        public MultiAimConstraint m_spineConstraint;

        private Transform m_lookTarget;
        
        private readonly Collider[] m_potentialTargets = new Collider[4];
        private Sequence m_weightBlendBackTween;
        private Sequence m_weightBlendForwardTween;

        private Vector3 m_effectorDefaultPosition;

        private const float MAX_DIST = 15f;
        private const float MAX_YAW = 75f; // Yaw is symmetric
        private const float MIN_PITCH = -15f; // Pitch is assymetric
        private const float MAX_PITCH = 40f;
        
        private const float DIST_WEIGHT = 0.75f;
        private const float YAW_WEIGHT = 1f;
        private const float PITCH_WEIGHT = 0.5f;

        private const float HEAD_CONSTRAINT_WEIGHT = 0.35f;
        private const float CHEST_CONSTRAINT_WEIGHT = 0.65f;
        
        private const float TARGET_SWITCH_COOLDOWN = 1f;

        private float CONSTRAINT_MIN_ANGLE => m_headConstraint.data.limits.x;
        private float CONSTRAINT_MAX_ANGLE => m_headConstraint.data.limits.y;

        private TimeUntil m_coolDown;
        
        private void Awake()
        {
            if (m_lookAtEffector == null) return;
            m_effectorDefaultPosition = m_lookAtEffector.position;

            if (m_headConstraint != null)
            {
                // Max/min is kinda unnecessary but for completion sake, making this knowing that yaw limits > pitch limits
                m_headConstraint.data.limits = new Vector2(Mathf.Min(-MAX_YAW, MIN_PITCH), Mathf.Max(MAX_YAW, MAX_PITCH));
            }
        }

        private void LateUpdate()
        {
            if (m_lookAtEffector == null || m_headConstraint == null || m_spineConstraint == null) return;

            if (m_coolDown <= 0f)
            {
                var prevTarget = m_lookTarget;
                UpdateLookAtTarget();
                if (prevTarget != null && prevTarget != m_lookTarget) m_coolDown = TARGET_SWITCH_COOLDOWN;
            }
            else if (m_lookTarget != null && !IsTargetValid(m_lookTarget, out var _)) m_lookTarget = null;
            
            if (m_lookTarget == null && m_headConstraint.weight > 0 && !m_weightBlendBackTween.isAlive)
            {
                if (m_weightBlendForwardTween.isAlive) m_weightBlendForwardTween.Stop();
                if (!m_weightBlendBackTween.isAlive) 
                    m_weightBlendBackTween = RigConstraintTweens.Weight(m_headConstraint, 0f, 0.5f, Ease.OutQuad)
                        .Group(RigConstraintTweens.Weight(m_spineConstraint, 0f, 0.75f, Ease.OutQuad))
                        .Group(Tween.PositionAtSpeed(m_lookAtEffector, m_effectorDefaultPosition, 1f, Ease.OutQuad));
            }
            else if (m_lookTarget != null)
            {
                if (m_weightBlendBackTween.isAlive) m_weightBlendBackTween.Stop();
                if (!m_weightBlendForwardTween.isAlive)
                {
                    m_weightBlendForwardTween = RigConstraintTweens.Weight(m_headConstraint, HEAD_CONSTRAINT_WEIGHT, 0.5f, Ease.OutQuad)
                        .Group(RigConstraintTweens.Weight(m_spineConstraint, CHEST_CONSTRAINT_WEIGHT, 0.75f, Ease.OutQuad));
                }

                Vector3 targetPos = GetTargetPosition(m_lookTarget.position);
                // Interp effector to target
                m_lookAtEffector.position = Vector3.Lerp(m_lookAtEffector.position, targetPos, 10f * Time.deltaTime);
            }
        }

        private Vector3 GetTargetPosition(Vector3 position)
        {
            // toTarget vector but we clamp the yaw
            var toTarget = position - transform.position;
            var planeToTarget = toTarget.ProjectOnPlane(transform.up);
            var pitchAngle = planeToTarget.SignedAngle(toTarget); // signed angle axis = planeToTarget X toTarget

            var pitchAdjustmentToApply = 0f;
            if (pitchAngle < MIN_PITCH) pitchAdjustmentToApply = MIN_PITCH - pitchAngle;
            else if (pitchAngle > MAX_PITCH) pitchAdjustmentToApply = MAX_PITCH - pitchAngle;
            else return position;

            var rotAxis = planeToTarget.Cross(toTarget);
            return transform.position + Quaternion.AngleAxis(pitchAdjustmentToApply, rotAxis) * toTarget;
        }

        private void UpdateLookAtTarget()
        {
            // Character layer test for now
            int numTargets = Physics.OverlapSphereNonAlloc(transform.position, MAX_DIST, m_potentialTargets, 1 << PhysicsLayers.HeadLookAt);
            float bestScore = -float.MaxValue;
            m_lookTarget = null;
            if (numTargets > 0)
            {
                // Find the closest one
                for (int n = 0; n < numTargets; n++)
                {
                    var target = m_potentialTargets[n].transform;
                    if (!IsTargetValid(target, out float score)) continue;

                    if (score > bestScore)
                    {
                        m_lookTarget = target;
                        bestScore = score;
                    }
                }
            }
            else m_lookTarget = null;
        }

        private bool IsTargetValid(Transform target, out float score)
        {
            score = -1;
            
            var toTarget = target.position - transform.position;
            float distance = toTarget.magnitude;
            var toTargetDir = toTarget / distance;
            var forwardDotToTarget = transform.forward.Dot(toTargetDir);
            if (forwardDotToTarget < 0) return false; // is behind
            
            var targetDirPlanar = toTargetDir.ProjectOnPlane(transform.up).normalized;
            float yawAngle = targetDirPlanar.Angle(transform.forward);
            float pitchAngle = targetDirPlanar.SignedAngle(toTargetDir); // Signed as asymmetric
            
            // Outside of constraint limits, ignore
            if (yawAngle > CONSTRAINT_MAX_ANGLE || yawAngle < CONSTRAINT_MIN_ANGLE) return false;
            if (pitchAngle > CONSTRAINT_MAX_ANGLE || pitchAngle < CONSTRAINT_MIN_ANGLE) return false;
            
            // Score
            float yawScore = 1f - (yawAngle / MAX_YAW); // lower yaw = higher score
            float pitchScore = (Mathf.InverseLerp(MIN_PITCH, 0f, pitchAngle) +
                               Mathf.InverseLerp(MAX_PITCH, 0f, pitchAngle)) / 2f; // Lower pitch = higher score
            float distScore = 1f - distance / MAX_DIST; // Closer the better

            // Normalized weighted score
            score = (distScore * DIST_WEIGHT + pitchScore * PITCH_WEIGHT + yawScore * YAW_WEIGHT) / (DIST_WEIGHT + PITCH_WEIGHT + YAW_WEIGHT);
            
            return true; // OverlapSphere covers distance max check
        }
    }
}