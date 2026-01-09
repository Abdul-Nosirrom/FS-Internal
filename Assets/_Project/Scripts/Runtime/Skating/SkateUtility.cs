using System;
using Drawing;
using FS.Math;
using FS.MeshProcessing;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using ISplineProvider = FS.MeshProcessing.ISplineProvider;

public static class SkateUtility
{
    public struct VertInfo
    {
        public float time;
        public Vector3 constraintNormal;
        public Vector3 tangent;
        public Vector3 lipPosition;
        public float normalSign;
    }
    
    /// <summary>
    /// Finds the proper orientation of a Vert suface given the lipPosition & the normal/up defaulted by the spline.
    /// Will choose the direction best aligned w/ alignmentTarget 
    /// </summary>
    public static float GetVertNormalDirection(Vector3 lipPosition, Vector3 normal, Vector3 up, Vector3 alignmentTarget)
    {
        normal = normal.normalized;
        float normalOffset = 0.05f;
        float verticalOffset = 0.01f;
        float castDist = 0.1f;
        
        Vector3 leftQueryStartPos = lipPosition + normal * normalOffset - up * verticalOffset;
        Vector3 rightQueryStartPos = lipPosition - normal * normalOffset - up * verticalOffset;

        bool skipLeft = false && Vector3.Dot(normal, alignmentTarget) < 0;
        bool skipRight = false && Vector3.Dot(normal, alignmentTarget) > 0;
        
        // Left raycast check
        //Physics.queriesHitBackfaces = true;
        bool leftHit = !skipLeft && Physics.Raycast(leftQueryStartPos, -normal, out var leftHitInfo, castDist, 1 << PhysicsLayers.Vert);
        bool rightHit = !skipRight && Physics.Raycast(rightQueryStartPos, normal, out var rightHitInfo, castDist, 1 << PhysicsLayers.Vert);
        
        // Draw debug
        //using var duration = Draw.ingame.WithDuration(5);
        //Draw.ingame.Arrow(leftQueryStartPos, leftQueryStartPos - normal * castDist, leftHit ? Color.green : Color.red);
        //Draw.ingame.Arrow(rightQueryStartPos, rightQueryStartPos + normal * castDist, rightHit ? Color.green : Color.red);
        //// Draw hit points & normal
        //if (leftHit)
        //{
        //    Draw.ingame.WireSphere(leftHitInfo.point, 0.1f, Color.yellow);
        //    Draw.ingame.Arrow(leftHitInfo.point, leftHitInfo.point + leftHitInfo.normal * 0.2f, Color.cyan);
        //}
        //if (rightHit)
        //{
        //    Draw.ingame.WireSphere(rightHitInfo.point, 0.1f, Color.yellow);
        //    Draw.ingame.Arrow(rightHitInfo.point, rightHitInfo.point + rightHitInfo.normal * 0.2f, Color.cyan);
        //}
        
        // TODO: We'd wanna compare w/ an "alignment" vector if a vert is two-sided, but for now we assume all verts are one-sided
        if (leftHit) return 1f;
        if (rightHit) return -1f;
        
        throw new Exception("[Vert] Failed to figure out proper vert direction");
    }

    public static void UpdateVertInfoFromSpline(ISplineProvider spline, Vector3 queryPos, Vector3 upVector, ref VertInfo vertInfo)
    {
        spline.GetNearestPoint(queryPos, out var closestPoint, out var t);

        if (t is > 0 and < 1)
        {
            vertInfo.tangent = Vector3.Normalize(spline.EvaluateTangent(t));
            vertInfo.constraintNormal = Vector3.Normalize(Vector3.Cross(vertInfo.tangent, upVector));
            
            // First time we init its zero - only when we update constraint normal to a new value otherwise we flip-flop
            if (vertInfo.normalSign != 0) vertInfo.constraintNormal *= vertInfo.normalSign;
        }
        
        vertInfo.lipPosition = (Vector3)closestPoint + (vertInfo.normalSign == 0 ? Vector3.zero : vertInfo.constraintNormal * 0.05f); // Offset a bit so we're not inside the vert
        vertInfo.time = t;
    }
    
    public static VertInfo GetVertInfoFromSpline(ISplineProvider spline, Vector3 queryPos, Vector3 upVector, Vector3 normalAlignmentTarget)
    {
        var vertInfo = new VertInfo();
        UpdateVertInfoFromSpline(spline, queryPos, upVector, ref vertInfo);
        
        // Figure out normal direction
        vertInfo.normalSign = GetVertNormalDirection(vertInfo.lipPosition, vertInfo.constraintNormal, upVector, normalAlignmentTarget);
        vertInfo.constraintNormal *= vertInfo.normalSign;

        return vertInfo;
    }

    public static ISplineProvider MaybeGetQuarterPipeBelowUs(Vector3 queryPos, Vector3 downDir, Vector3 normal, float maxDist = 10f)
    {
        //using var duration = Draw.ingame.WithDuration(5);
        // TODO: This is super broken rn
        // Simple check for 1 vert object
        //Draw.ingame.Line(queryPos, queryPos + downDir * maxDist);
        if (Physics.SphereCast(queryPos, 0.25f, downDir, out var hit, maxDist, 1 << PhysicsLayers.Vert))
        {
            //if (!hit.collider.CompareTag(GameTags.Vert)) return null;
            hit.collider.TryGetComponent(out ISplineProvider splineProvider);
            if (splineProvider != null)
            {
                //Draw.ingame.WireSphere(hit.point, 0.2f, Color.red);

                // Is it comparable to our current normal?
                var vertInfo = GetVertInfoFromSpline(splineProvider, hit.point, -downDir, normal);

                if (vertInfo.constraintNormal.Dot(normal) < 0.1) return null;
                
                return splineProvider;
            }
        }

        return null;
    }
}