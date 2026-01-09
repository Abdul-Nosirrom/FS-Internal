using System;
using System.Collections.Generic;
using Drawing;
using FS.CameraSystem;
using FS.Attributes;
using FS.Math;
using FS.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FS.CombatSystem
{
    public enum TargetingReferenceFrame
    {
        Local,
        World,
        Camera,
        Input
    }
    
    public struct TargetingResult
    {
        public TargetingQuery Target;
        public float DistanceTo;
        public float AngleTo;
    }

    // public struct TargetingAngleFilter
    // {
    //     public TargetingReferenceFrame ReferenceFrame;
    //     public Vector3 HeadingVector;
    //     public Vector2 AngleRange; // In degrees, e.g. (-45, 45) for a 90-degree cone
    // }
    //
    
    [Serializable]
    public class TargetingSettings
    {
        [NonSerialized] public GameObject Instigator;

                
        /// <summary>
        /// Target must have all these tags in their targeting query component identity tags
        /// </summary>
        [BoxGroup("Core Targeting Settings")]
        [EnumFlag]
        public TargetingTags FlagsRequiredOnTarget;

        /// <summary>
        /// If true, we do a shape sweep to the possible target and discard it if we encounter any hits along the way that isnt the target
        /// </summary>
        [BoxGroup("Core Targeting Settings")]
        public bool RequiresLoS;

        /// <summary>
        /// If true, we discard any potential targets that are not in camera view
        /// </summary>
        [BoxGroup("Core Targeting Settings")]
        public bool RequiresOnScreen;
        
        /// <summary>
        /// Reference frame for angle calculations (e.g angle from "what" forward vector?)
        /// </summary>
        [BoxGroup("Target Selection")]
        [HorizontalGroup("Target Selection/Split")]
        [VerticalGroup("Target Selection/Split/Angle")]
        public TargetingReferenceFrame AngleReferenceFrame;
        private bool IsInputReferenceFrame => AngleReferenceFrame == TargetingReferenceFrame.Input;
        
        /// <summary>
        /// Backup reference frame in case input is zero
        /// </summary>
        [VerticalGroup("Target Selection/Split/Angle")]
        [ShowIf("IsInputReferenceFrame")]
        public TargetingReferenceFrame FallbackAngleReferenceFrame;
        
        /// <summary>
        /// Discards any target found to be outside of this angle range
        /// </summary>
        [VerticalGroup("Target Selection/Split/Angle")]
        [MinMaxSlider(-180, 180)]
        public Vector2 AngleThreshold;
        
        /// <summary>
        /// Reference frame for applying distance offsets. Becomes Center = ActorLocation + RefFrame.Rotate(DistanceCenterOffset);
        /// </summary>
        [VerticalGroup("Target Selection/Split/Distance")]
        public TargetingReferenceFrame DistanceReferenceFrame;
        
        /// <summary>
        /// Discards any target found to be outside of this distance range. We use Y (max) as the overlap radius, then X (min)
        ///	to "make a cavity in the sphere"
        /// </summary>
        [VerticalGroup("Target Selection/Split/Distance")]
        [MinMaxSlider(0, 50)]
        public Vector2 DistanceThreshold;
        
        /// <summary>
        /// Offset where the "Sphere Center" is for the initial overlap check, converted to the appropriate reference frame based on DistanceCheckReferenceFrame. 
        /// </summary>
        [VerticalGroup("Target Selection/Split/Distance")]
        public Vector3 DistanceCenterOffset;

        /// <summary>
        /// Offset of where the "Best Distance" is. For example, if its 5m, then 5m away from the center is considered the best distance score, but the actual overlap isn't offset.
        /// Useful in cases of "ranged attack" targeting, where you still wanna target close by targets, but prefer ones who are some distance away.
        /// </summary>
        [VerticalGroup("Target Selection/Split/Distance")]
        public float BestDistanceOffset;
        
        /// <summary>
        /// Weight of normalized distance. Comparison result [DistWeight * NormDist + AngleWeight * NormAngle], highest wins.
        /// </summary>
        [BoxGroup("Weights")]
        [Range(0, 1)]
        public float DistanceWeight;
        
        /// <summary>
        /// Weight of normalized angles. Comparison result [DistWeight * NormDist + AngleWeight * NormAngle], highest wins.
        /// </summary>
        [BoxGroup("Weights")]
        [Range(0, 1)]
        public float AngleWeight;
        
        
        private RaycastHit[] m_hitBuffer = new RaycastHit[4];
        private Collider[] m_overlapBuffer = new Collider[32];

        public bool PeformTargeting(GameObject instigator, out TargetingResult result, GameObject ignoreObject = null, Vector3? screenDirBias = null, bool debug = false)
        {
            if (!instigator)
                throw new ArgumentNullException($"Cant perform targeting with null instigator.");
            
            Instigator = instigator;
            result = default;
            
            using var debugDuration = Draw.ingame.WithDuration(5f);
            using var debugLineWidth = Draw.ingame.WithLineWidth(2f);

            int numOverlaps = Physics.OverlapSphereNonAlloc(OverlapCenterPosition, OverlapSphereRadius, m_overlapBuffer,
                1 << PhysicsLayers.TargetingQuery, QueryTriggerInteraction.Ignore);

            if (debug)
                Draw.ingame.WireSphere(OverlapCenterPosition, OverlapSphereRadius, Color.blue);

            if (numOverlaps == 0) return false;
            
            if (!ProcessTargets(numOverlaps, out result, ignoreObject, screenDirBias, debug))
                return false;

            return true;
        }

        public bool ProcessTargets(int numTargets, out TargetingResult outTarget,
            GameObject ignoreObject = null, Vector3? screenDirBias = null, bool debug = false)
        {
            using var debugDuration = Draw.ingame.WithDuration(5f);
            using var debugLineWidth = Draw.ingame.WithLineWidth(2f);
            
            screenDirBias = screenDirBias ?? Vector3.zero;
            
            outTarget = default;
            outTarget.DistanceTo = -float.MaxValue;
            outTarget.AngleTo = -float.MaxValue;
            float bestWeight = -float.MaxValue;
            bool foundTarget = false;

            var camera = Instigator.GetComponent<CameraBehaviorController>()?.m_camera;
            Vector2 ignoreTargetScreenPos = Vector2.zero;
            if (camera)
                ignoreTargetScreenPos = camera.WorldToViewportPoint(ignoreObject?.transform.position ?? Vector3.zero);
            
            for (int t = 0; t < numTargets; t++)
            {
                var target = m_overlapBuffer[t];
                
                if (target.gameObject == ignoreObject) continue;
                
                var targetComp = target.GetComponent<TargetingQuery>();
                if (!IsTargetValid(targetComp, out var dist, out var angle, out var normalizedDist, out var normalizedAngle, debug))
                {
                    if (debug)
                        Draw.ingame.WireSphere(target.transform.position, 1f, Color.red);
                    continue; // Skip invalid targets
                }

                float currentWeight = DistanceWeight * normalizedDist + AngleWeight * normalizedAngle;

                if (screenDirBias.Value.sqrMagnitude > 0 && camera)
                {
                    // World space direction check is OK
                    var toTarget = target.transform.position - Instigator.transform.position;
                    if (toTarget.Dot(screenDirBias.Value) < 0f) continue; // Not valid based on our input

                    // If we have a current target, we wanna find the next best one thats closest to the current target in screen space. We only ever call this function if we are currently locked on
                    Vector3 targetScreenPos = camera.WorldToViewportPoint(target.transform.position);
                    if (targetScreenPos.z < 0f || targetScreenPos.x < 0f || targetScreenPos.x > 1f ||
                        targetScreenPos.y < 0f || targetScreenPos.y > 1f) continue; // Off screen

                    float DistFromCurrentTarget = Mathf.Abs(targetScreenPos.x - ignoreTargetScreenPos.x);
                    if (DistFromCurrentTarget >= outTarget.DistanceTo) continue;
                }

                if (currentWeight > bestWeight)
                {
                    bestWeight = currentWeight;
                    outTarget.Target = targetComp;
                    outTarget.DistanceTo = dist;
                    outTarget.AngleTo = angle;
                    foundTarget = true;

                    if (debug)
                    {
                        Draw.ingame.WireSphere(target.transform.position, 1f, Color.yellow);
                        Draw.ingame.Label2D(target.transform.position, $"Possible Target: {target.name} (Dist: {dist:F2}, Angle: {angle:F2})");
                    }
                }
                else if (debug)
                {
                    Draw.ingame.WireSphere(target.transform.position, 1f, Color.red);
                    Draw.ingame.Label2D(target.transform.position, $"Target: {target.name} (Dist: {dist:F2}, Angle: {angle:F2})");
                }
            }

            if (foundTarget && debug)
                Draw.ingame.WireSphere(outTarget.Target.transform.position, 1f, Color.green);

            return foundTarget;
        }

        public bool IsTargetValid(TargetingQuery checkTarget, out float dist, out float angle, out float normalizedDist,
            out float normalizedAngle, bool debug = false)
        {
            dist = angle = normalizedDist = normalizedAngle = 0f;
            if (checkTarget == null) return false;
            if (!checkTarget.EvaluateTargetingTags(this)) return false;
            
            var angleHeadingVector = GetBasis(AngleReferenceFrame) * Vector3.forward;
            var distanceCheckCenter = OverlapCenterPosition;
            var toTargetVector = checkTarget.transform.position - distanceCheckCenter;
            
            // Evaluate angle conditions
            if (AngleWeight > 0f)
            {
                angle = Vector3.Angle(angleHeadingVector, toTargetVector);
                if (angle < AngleThreshold.x || angle > AngleThreshold.y) return false;
                normalizedAngle = 1f - Mathf.InverseLerp(AngleThreshold.x, AngleThreshold.y, angle); // Favor smaller angles
            }
            
            // Evaluate distance conditions
            if (DistanceWeight > 0f)
            {
                dist = Mathf.Abs(BestDistanceOffset - toTargetVector.magnitude);
                if (dist < DistanceThreshold.x || dist > DistanceThreshold.y)
                {
                    if (debug)
                    {
                        Draw.ingame.Line(distanceCheckCenter, toTargetVector, Color.red);
                        Draw.ingame.Label2D(checkTarget.transform.position, $"Target {checkTarget.name} out of distance bounds: {dist:F2} (Threshold: {DistanceThreshold.x:F2} - {DistanceThreshold.y:F2})");
                    }
                    return false;
                }
                normalizedDist = 1f - Mathf.InverseLerp(DistanceThreshold.x, DistanceThreshold.y, dist); // Favor smaller distances
            }

            if (RequiresOnScreen)
            {
                var cam = Instigator.GetComponent<CameraBehaviorController>();
                if (cam == null) throw new NullReferenceException("Instigator is not a player, can't retrieve camera for on-screen check.");

                var viewportPos = cam.m_camera.WorldToViewportPoint(checkTarget.transform.position);
                if (viewportPos.z < 0 || viewportPos.z < 0 || viewportPos.x < 0 || viewportPos.x > 1 ||
                    viewportPos.y < 0 || viewportPos.y > 1)
                {
                    if (debug)
                    {
                        Draw.ingame.Label2D(checkTarget.transform.position, $"Target {checkTarget.name} off-screen: {viewportPos}");
                    }
                    return false; // Target is off-screen
                }
            }

            if (RequiresLoS)
            {
                int numHits = Physics.RaycastNonAlloc(Instigator.transform.position, toTargetVector, m_hitBuffer, dist,
                    1 << PhysicsLayers.Default, QueryTriggerInteraction.Ignore);
                if (numHits > 0)
                {
                    for (int i = 0; i < numHits; i++)
                    {
                        if (m_hitBuffer[i].collider.gameObject == checkTarget.gameObject) continue; // Ignore self
                        if (m_hitBuffer[i].collider.gameObject == checkTarget.gameObject) continue; // Ignore specified object
                        if (debug)
                        {
                            Draw.ingame.Line(Instigator.transform.position, m_hitBuffer[i].point, Color.red);
                            Draw.ingame.Label2D(m_hitBuffer[i].point, $"Hit {m_hitBuffer[i].collider.name} blocking line of sight to {checkTarget.name}");
                        }
                        return false; // Hit something else, no line of sight
                    }
                }
            }
            
            return true;
        }

        protected float OverlapSphereRadius => DistanceThreshold.y;

        protected Vector3 OverlapCenterPosition =>
            Instigator.transform.position + GetBasis(DistanceReferenceFrame) * DistanceCenterOffset;

        protected Quaternion GetBasis(TargetingReferenceFrame refFrame)
        {
            if (refFrame != TargetingReferenceFrame.World && Instigator == null)
                throw new NullReferenceException("Instigator is not set for TargetingSettings. Required For non-World reference frames.");
            
            switch (refFrame)
            {
                case TargetingReferenceFrame.Local:
                    return Instigator.transform.rotation;
                case TargetingReferenceFrame.World:
                    return Quaternion.identity;
                case TargetingReferenceFrame.Camera:
                    var cam = Instigator.GetComponent<CameraBehaviorController>();
                    if (cam == null) throw new NullReferenceException("Instigator is not a player, can't retrieve camera for reference frame.");
                    return cam.CameraState.m_rotation;
                case TargetingReferenceFrame.Input:
                    var physController = Instigator.GetComponent<PhysicsController>();
                    if (physController == null) throw new NullReferenceException("Instigator has no input processor (PhysicsController), can't retrieve input for reference frame.");
                    var inputDir = physController.MoveInput(Vector3.up); // TODO: Rn forced up basis
                    if (inputDir.sqrMagnitude <= 0f) return GetBasis(FallbackAngleReferenceFrame);
                    return Quaternion.LookRotation(inputDir);
                default:
                    throw new ArgumentOutOfRangeException(nameof(refFrame), refFrame, null);
            }
        }
    }
}