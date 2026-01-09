using FS.MeshProcessing;
using Sirenix.OdinInspector.Editor.Validation;
using UnityEngine;

[assembly: RegisterValidator(typeof(FS.Editor.Validators.RailConfigValidator))]

namespace FS.Editor.Validators
{
    public class RailConfigValidator : SceneValidator
    {
        protected override void Validate(ValidationResult result)
        {
            var railGrinds = PhysicsLayers.FindGameObjectsWithLayer(PhysicsLayers.RailGrind);

            foreach (var rail in railGrinds)
            {
                var splineProvider = rail.GetComponent<ISplineProvider>();
                if (splineProvider == null)
                {
                    result.AddError($"No Spline Provider Found On Rail Grind: {rail.name}")
                        .SetSelectionObject(rail);
                }
            }
        }
    }
}