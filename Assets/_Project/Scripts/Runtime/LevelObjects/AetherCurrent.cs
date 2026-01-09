using System.Collections;
using System.Collections.Generic;
using FluffyUnderware.Curvy;
using FS.Attributes;
using FS.CameraSystem;
using FS.Math;
using FS.Rendering;
using FS.Utility;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

[Experimental]
[LevelDesignCategory("Level Objects")]
public class AetherCurrent : LevelObjectBase
{
    [SerializeField, Required] private CurvySpline m_aetherSpline;
    [SerializeField] private VFXController m_followerVFX;

    private const float k_travelSpeed = 40;
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (m_aetherSpline == null)
        {
            m_aetherSpline = GetComponentInChildren<CurvySpline>();
            if (m_aetherSpline) return;
            
            m_aetherSpline = CurvySpline.Create();
            m_aetherSpline.gameObject.name = "AetherCurrentSpline";
            m_aetherSpline.transform.SetParent(transform, false);

            m_aetherSpline.Add(Vector3.zero, Vector3.forward * 10f);
        }
    }
#endif
    
    protected override void OnPhysicsActorEnter(Context context)
    {
        if (m_aetherSpline == null) return;
        
        // Dont even need to keep track of multiple actors for this level object, already handled by "HasLevelObject" in PhysicsController checked before we execute this
        // Project context to spline and figure out direction
        var nearestTF = m_aetherSpline.GetNearestPointTF(context.physics.Position, Space.World);
        var nearestDist = m_aetherSpline.TFToDistance(nearestTF, CurvyClamping.Clamp);
        var nearestTangent = m_aetherSpline.GetTangentFast(nearestTF, Space.World);
        float dir = nearestTangent.Dot(context.physics.transform.forward) >= 0 ? 1f : -1f;
        StartInteractionCoroutine(context, AetherCurrentFollow(context, nearestDist, dir));
    }

    private IEnumerator AetherCurrentFollow(Context context, float closestDist, float dir)
    {
        BeginInteraction(context);

        float pathLength = m_aetherSpline.Length;
        float endDist = dir > 0 ? pathLength : 0f;
        
        // Hide meshes (would be good to have a better way to do this globally, like an API thing instead of this dumb loop)
        if (context.animator && context.animator.TryGetComponent<RenderVisibilityController>(out var rvc))
        {
            //rvc.Hide(); would be nice to force hidden everything and reset afterwards, but that class doesnt have that functionality yet
        }
        HashSet<int> visibleMeshIDs = new HashSet<int>();
        foreach (var rend in context.physics.GetComponentsInChildren<Renderer>())
        {
            if (rend.enabled)
            {
                visibleMeshIDs.Add(rend.GetInstanceID());
                rend.enabled = false;
            }
        }
        
        VFXParams playParams = new VFXParams
        {
            m_shouldParent = true,
            m_parent = context.physics.transform,
        };
        var fxInst = VFXManager.Instance.PlayVFX(m_followerVFX, playParams);

        if (context.cameraController)
        {
            var camFX = new CameraFX_AetherCurrent();
            float distanceToTravel = dir > 0 ? pathLength - closestDist : closestDist;
            camFX.duration = Mathf.Abs(distanceToTravel) / k_travelSpeed;
            context.cameraController.AddCameraFX(camFX);
        }
        
        if (context.actionController)
            context.actionController.DisableActions();

        try
        {
            while (!Mathf.Approximately(closestDist, endDist))
            {
                closestDist = Mathf.Clamp(closestDist + k_travelSpeed * Time.deltaTime * dir, 0f, pathLength);
                float tf = m_aetherSpline.DistanceToTF(closestDist);
                context.physics.Position = m_aetherSpline.InterpolateFast(tf, Space.World);
                context.physics.Rotation = m_aetherSpline.GetOrientationFast(tf, dir <= 0f, Space.World);
                //context.physics.Teleport(sample.position, sample.rotation);
                yield return Yields.WaitForFixedUpdate;
            }
        }
        finally // w/ Coroutines, if we stop it externally we still want to run this cleanup and the try/finally ensures that
        {
            // Close up VFX
            fxInst.Stop();
        
            // Reenable actions
            if (context.actionController)
                context.actionController.EnableActions();
        
            // Reenable meshes
            foreach (var rend in context.physics.GetComponentsInChildren<Renderer>())
            {
                if (visibleMeshIDs.Contains(rend.GetInstanceID()))
                {
                    rend.enabled = true;
                }
            }
            
            context.physics.Velocity = m_aetherSpline.GetTangentFast(m_aetherSpline.DistanceToTF(closestDist), Space.World).normalized * (k_travelSpeed * dir);
            EndInteraction(context); // This calls OnInteractionCleanup, similarly it can be called externally - (ClearLevelObject on physics controller calls it too)
        }
    }

    private class CameraFX_AetherCurrent : CameraFX
    {
        public override Mode FXMode => Mode.OrbitVector;
        public float duration;
        
        protected override void OnStartFX()
        {
            Tween.Custom(m_cameraVector.Distance, 7, 0.4f, DistanceTween, Ease.OutQuad)
                .Chain(Tween.Delay(duration - 0.2f)).
                Chain(Tween.Custom(7, m_cameraVector.Distance, 0.4f, DistanceTween, Ease.OutQuad));
            
            Tween.Custom(m_cameraVector.FOV, 80, 0.4f, FOVTween, Ease.OutQuad)
                .Chain(Tween.Delay(duration - 0.8f).Chain(Tween.Custom(80, m_cameraVector.FOV, 0.4f, FOVTween, Ease.OutQuad)));

            Tween.Delay(duration).OnComplete(StopFX);
        }

        private float targetDist, targetFOV;

        private void DistanceTween(float newDist)
        {
            targetDist = newDist;
        }

        private void FOVTween(float newFOV)
        {
            targetFOV = newFOV;
        }

        public override void OnUpdateFX()
        {
            m_cameraVector.FOV = targetFOV;
            m_cameraVector.Distance = targetDist;
        }
    }
}