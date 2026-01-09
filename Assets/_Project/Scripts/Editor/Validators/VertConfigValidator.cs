using FS.MeshProcessing;
using Sirenix.OdinInspector.Editor.Validation;
using UnityEngine;

[assembly: RegisterValidator(typeof(FS.Editor.Validators.VertConfigValidator))]

namespace FS.Editor.Validators
{
    public class VertConfigValidator : SceneValidator
    {
        protected override void Validate(ValidationResult result)
        {
            // Validation of all "Vert" objects
            var vertObjects = PhysicsLayers.FindGameObjectsWithLayer(PhysicsLayers.Vert);

            foreach (var vert in vertObjects)
            {
                var splineProvider = vert.GetComponent<ISplineProvider>();
                if (splineProvider == null)
                {
                    result.AddError($"No Spline Provider Found On Vert: {vert.name}")
                        .SetSelectionObject(vert);
                }
            }
        }
    }
}