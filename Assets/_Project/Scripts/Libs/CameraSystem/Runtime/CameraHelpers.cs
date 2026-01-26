using System;
using Drawing;
using UnityEngine;
using PrimeTween;
using Sirenix.OdinInspector;
using TimeUtils;

namespace FS.CameraSystem
{
    #region Blending
    [Serializable]
    public struct CameraBlendParams
    {
        public float m_duration;
        public Ease m_blendFunction;
        public bool m_shouldLockOutgoing;

        public static readonly CameraBlendParams Default 
            = Create(0.25f, Ease.InOutQuad, false);
        public static readonly CameraBlendParams None 
            = Create(0f, Ease.Linear, false);
        
        public static CameraBlendParams Create(float duration, Ease blendFunction, bool shouldLockOutgoing) 
            => new CameraBlendParams(duration, blendFunction, shouldLockOutgoing);

        private CameraBlendParams(float duration, Ease blendFunction, bool shouldLockOutgoing)
        {
            m_duration = duration;
            m_blendFunction = blendFunction;
            m_shouldLockOutgoing = shouldLockOutgoing;
        }

        public float Evaluate(float time)
        {
            if (m_duration == 0f) return 1f;
            var percent = Mathf.Clamp01(time / m_duration);
            return Easing.Evaluate(percent, m_blendFunction);
        }
    }
    #endregion
    
    #region Obstruction
    public static class CameraObstructionSettings
    {
        public static readonly LayerMask k_layerMask = PhysicsLayers.GetLayerCollisionMask(PhysicsLayers.CameraObstruction);
        public static readonly int k_maxObstructions = 32;
    }

    public struct WhiskerCameraHit
    {
//        public RaycastHit[] m_obstructionHits;
        public RaycastHit m_closestHit;
        public bool IsObstructed => m_closestHit.collider != null;
        
        // public void Init()
        // {
        //     m_obstructionHits = new RaycastHit[CameraObstructionSettings.k_maxObstructions];
        //     m_closestHit = new RaycastHit();
        // }
    }
    
    public struct CameraObstruction
    {
        public bool m_isObstructed;
        public float m_distanceOnObstruction;
        private RaycastHit m_validObstruction;
        private RaycastHit[] m_obstructionHits;
        private Vector3 m_focusPoint;
        private CameraState m_camera;

        private const int k_whiskerCount = 8; // whiskers on either side, so total raycast count is 2 * k_whiskerCount + 1
        private const float k_whiskerOffsetAngle = 25f;
        private WhiskerCameraHit[] m_whiskerHits;
        private float[] m_whiskerWeights;
        private TimeSince m_lastObstructionTime;
        public float SinceLastObstruction => m_lastObstructionTime;
        public float m_lastDistanceOnObstruction;
        
        public void Init(CameraState camera)
        {
            m_camera = camera;
            m_isObstructed = false;
            m_distanceOnObstruction = 0f;
            m_obstructionHits = new RaycastHit[CameraObstructionSettings.k_maxObstructions];
            m_validObstruction = new RaycastHit();
            
            m_whiskerHits = new WhiskerCameraHit[2 * k_whiskerCount + 1];
            m_whiskerWeights = new float[2 * k_whiskerCount + 1];

            for (int i = 0; i < m_whiskerWeights.Length; i++)
            {
                m_whiskerWeights[i] = GaussianWeights(WhiskerWeight(i - k_whiskerCount));

                //m_whiskerHits[i].Init();
            }
            // for (int i = 0; i < m_whiskerHits.Length; i++)
            // {
            //     m_whiskerHits[i].Init();
            // }
        }

        private float GaussianWeights(float idx)
        {
            // Centered at 0
            float std = 0.5f;
            return 1 / (std * Mathf.Sqrt(2 * Mathf.PI)) * Mathf.Exp(-0.5f * Mathf.Pow(idx / std, 2));
        }

        // TODO: Camera obstruction evaluation can be improved, we get pretty frequently clipping and occasional bugging out
        // at times
        public void EvaluateObstruction_OLD_WHISKERS(Vector3 focusPoint, GameObject ignoreCollider)
        {
            if (m_camera == null)
            {
                Debug.LogError("[CameraSystem] Can't evaluate obstruction data, null camera.");
                return;
            }
            
            m_focusPoint = focusPoint;
            Vector3 to = m_camera.m_position;
            float distance = Vector3.Distance(m_focusPoint, to);
            Vector3 direction = (to - m_focusPoint).normalized;
            float castRadius = 0.5f;

            m_isObstructed = false;

            var rotAxis = m_camera.m_rotation * Vector3.up;
            int numWhiskers = m_whiskerHits.Length;

            bool skip = false;
            bool centerHit = false;
            //for (int w = 0; w < m_whiskerHits.Length; w++)
            for (int w = -k_whiskerCount; w <= k_whiskerCount; w++)
            {
                int wIdx = w + k_whiskerCount; // offset to get positive index
                float alpha = 0.5f + wIdx * 1f / (numWhiskers - 1);
                float angle = Mathf.Lerp(-k_whiskerOffsetAngle, k_whiskerOffsetAngle, alpha);
                
                // Rotate direction vector around the camera's up axis
                Vector3 whiskerDirection = Quaternion.AngleAxis(angle, rotAxis) * direction;

                bool obstructed = Physics.Raycast(m_focusPoint, whiskerDirection,
                    out m_whiskerHits[wIdx].m_closestHit, distance, CameraObstructionSettings.k_layerMask,
                    QueryTriggerInteraction.Ignore);
                //bool obstructed = Physics.Raycast(m_focusPoint, whiskerDirection, out m_whiskerHits[w].m_closestHit, distance,
                //    CameraObstructionSettings.k_layerMask, QueryTriggerInteraction.Ignore);

                if (w == 0 && obstructed)
                {
                    centerHit = true;
                }
                else if (!obstructed)
                {
                    m_whiskerHits[wIdx].m_closestHit.point = m_focusPoint + whiskerDirection * distance;
                    skip = true;
                }
            }

            //if (skip) return;

            m_isObstructed = true;

            var color = Color.green;
            m_isObstructed = Physics.SphereCast(m_focusPoint - direction * castRadius, castRadius, direction,
                out var hit, distance + castRadius,
                CameraObstructionSettings.k_layerMask, QueryTriggerInteraction.Ignore);
            var clipOffset = m_camera.m_nearClipPlane * 1.1f;
            if (m_isObstructed)
            {
                m_validObstruction = hit;
                m_distanceOnObstruction = Mathf.Max(clipOffset, distance - hit.distance - clipOffset);
                
                Draw.ingame.Label2D(m_focusPoint, $"Distance: {m_distanceOnObstruction:F2}", Color.white);
            }
            else if (centerHit && Physics.CheckSphere(m_focusPoint - direction * castRadius, castRadius, CameraObstructionSettings.k_layerMask))
            {
                m_isObstructed = true;
                m_distanceOnObstruction = distance - clipOffset;
                color = Color.red;
            }
            
            Draw.ingame.WireSphere(m_focusPoint - direction * castRadius, castRadius, color);

            if (m_isObstructed) m_lastObstructionTime = 0;

            return;

            // Radius must be near clip plane (slightly expanded) to avoid bad looking clipping when bringing the camera in
            // Can't avoid some clipping on the ignore collider though
            int numHits = Physics.SphereCastNonAlloc(m_focusPoint, m_camera.m_nearClipPlane * 1.1f, direction, m_obstructionHits, distance,
                CameraObstructionSettings.k_layerMask, QueryTriggerInteraction.Ignore);
            
            m_validObstruction.distance = Mathf.Infinity;
            for (int i = 0; i < numHits; i++)
            {
                var obstruction = m_obstructionHits[i];
                if (obstruction.collider.gameObject == ignoreCollider) continue;
                
                if (obstruction.distance < m_validObstruction.distance && obstruction.distance > 0)
                {
                    m_validObstruction = obstruction;
                    m_distanceOnObstruction = distance - obstruction.distance;//Mathf.Max(0, distance - Mathf.Max(0, obstruction.distance - m_camera.m_nearClipPlane * 1.1f));
                    m_isObstructed = true;
                }
            }

            if (!m_isObstructed && Physics.Raycast(m_focusPoint, direction, distance, CameraObstructionSettings.k_layerMask))
            {
                m_isObstructed = Physics.CheckSphere(m_focusPoint, m_camera.m_nearClipPlane * 1.1f,
                    CameraObstructionSettings.k_layerMask);
                if (m_isObstructed) m_distanceOnObstruction = distance;
            }
        }

        private float WhiskerWeight(int idx)
        {
            // We assume index is [-k_whiskerCount, k_whiskerCount]
            float weight = idx * 1f / (m_whiskerHits.Length - 1);
            weight = Mathf.Abs(4f * (weight - 0.5f) - 1f); // remap to [1, 0, 1] range
            weight = Mathf.Lerp(1, 0.1f, weight);
            return weight;
        }
        
        public void EvaluateCameraObstruction(Vector3 focusPoint, GameObject ignoreCollider)
        {
            if (m_camera == null)
            {
                Debug.LogError("[CameraSystem] Can't evaluate obstruction data, null camera.");
                return;
            }

            float obstructionDistance = 0f;
            
            m_focusPoint = focusPoint;
            Vector3 to = m_camera.m_position;
            float distance = Vector3.Distance(m_focusPoint, to);
            Vector3 direction = (to - m_focusPoint).normalized;
            float castRadius = 0.5f;

            m_isObstructed = false;
            
            bool didRayHit = Physics.Raycast(m_focusPoint, direction, out var rayHit, distance + castRadius,
                CameraObstructionSettings.k_layerMask, QueryTriggerInteraction.Ignore);
            
            bool didSphereHit = Physics.SphereCast(m_focusPoint - direction * castRadius, castRadius, direction,
                out var sphereHit, distance + castRadius,
                CameraObstructionSettings.k_layerMask, QueryTriggerInteraction.Ignore);
            
            var clipOffset = m_camera.m_nearClipPlane * 1.1f;
            if (didSphereHit)
            {
                m_validObstruction = sphereHit;
                obstructionDistance = Mathf.Max(clipOffset, distance - sphereHit.distance - clipOffset);
            }
            else if (didRayHit && Physics.CheckSphere(m_focusPoint - direction * castRadius, castRadius, CameraObstructionSettings.k_layerMask))
            {
                m_validObstruction = rayHit;
                obstructionDistance = distance - clipOffset;
            }

            m_isObstructed = (didSphereHit && didRayHit) || didRayHit;

            if (m_isObstructed) m_lastObstructionTime = 0;
            else obstructionDistance = 0f;

            
            m_distanceOnObstruction =
                Mathf.Lerp(m_distanceOnObstruction, obstructionDistance, (m_isObstructed ? 15f : 10f) * Time.deltaTime);

           m_camera.m_position += (m_camera.m_rotation * Vector3.forward) * m_distanceOnObstruction;
        }
        
        public void DrawObstructionGizmos()
        {
            if (m_camera == null) return;

            Draw.ingame.WireSphere(m_focusPoint, 0.1f, Color.yellow);

            using var width = Draw.ingame.WithLineWidth(2f);
            int idx = 0;
            foreach (var whisker in m_whiskerHits)
            {
                float weight = idx * 1f / (m_whiskerHits.Length - 1);
                weight = Mathf.Abs(4f * (weight - 0.5f) - 1f); // remap to [1, 0, 1] range
                weight = m_whiskerWeights[idx];//Mathf.Lerp(1, 0.1f, weight);
                var hit = whisker.m_closestHit;
                var color = whisker.IsObstructed ? Color.green : Color.red;
                color = Color.white * weight;
                color.a = 1;
                Draw.ingame.Line(m_focusPoint, hit.point, color);
                Draw.ingame.Label2D(hit.point, $"{weight:F2}");
                idx++;
            }

            return;
            
            if (m_isObstructed)
            {
                using var duration = Draw.ingame.WithDuration(5f);

                Vector3 to = m_camera.m_position;

                Draw.ingame.Line(m_focusPoint, to, Color.red);
                
                if (m_isObstructed)
                {
                    Draw.ingame.WireSphere(m_validObstruction.point, 0.1f, Color.red);
                }
            }
        }
    }
    #endregion
    
}