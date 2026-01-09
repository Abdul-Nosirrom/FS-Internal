using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.Validation;
using UnityEngine;

[assembly: RegisterValidator(typeof(FS.Player.Editor.PlayerOnlyAttributeValidator<>))]

namespace FS.Player.Editor
{
    public class PlayerOnlyAttributeValidator<T> : AttributeValidator<PlayerOnlyAttribute, T> where T : Component
    {
        public override bool CanValidateProperty(InspectorProperty property)
        {
            return property == property.Tree.RootProperty;
        }
        
        protected override void Validate(ValidationResult result)
        {
            // 'Value' is the MonoBehaviour component instance
            if (this.Value == null)
            {
                result.ResultType = ValidationResultType.IgnoreResult;
                return;
            }
            
            if (this.Value.GetComponentInParent<Player>() == null)
            {
                result.AddError($"[PlayerOnly] {this.Value.GetType().Name} can only be added to a GameObject under a Player root.").WithFix("Delete Component", () =>
                {
                    if (this.Value != null)
                    {
                        Object.DestroyImmediate(this.Value);
                    }
                });
            }
        }
    }
}