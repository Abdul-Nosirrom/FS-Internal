using System;
using FluffyUnderware.Curvy;
using FS.MeshProcessing;
using UnityEngine;

/// <summary>
/// Simple component that just provides an ExplicitSplineProvider to the correct railgrind gameobject in its hierarchy
/// </summary>
[SelectionBase]
public class RailGrindSplineProvider : MonoBehaviour
{
    public CurvySpline RailSpline;

    private void Awake()
    {
        if (RailSpline == null) RailSpline = GetComponentInChildren<CurvySpline>();

        if (RailSpline)
        {
            // Get child w/ layer 'RailGrind' (i think usually the trigger is a mesh collider)
            foreach (var col in GetComponentsInChildren<MeshCollider>())
            {
                if (col.CompareLayer(PhysicsLayers.RailGrind))
                {
                    col.gameObject.AddComponent<ExplicitSplineProvider>().Spline = RailSpline;
                    break;
                }
            }
        }
    }
}