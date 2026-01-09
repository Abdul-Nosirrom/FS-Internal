using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Animancer;
using FS.Extensions;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.Validation;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

[assembly: RegisterValidator(typeof(FS.Animation.Editor.AnimationReferenceValidator))]

namespace FS.Animation.Editor 
{
    /// <summary>
    /// Drop down selector for AnimationReference, shows sets as Foldouts and animations in sets as selectables
    /// </summary>
    public class AnimationReferenceEditor : OdinValueDrawer<AnimationReference>
    {
        private struct AnimationReferenceItem
        {
            public Type SetType;
            public FieldInfo Field;
        }
        
        private static List<AnimationReferenceItem> s_animationReferenceItems;
        
        [InitializeOnLoadMethod]
        private static void InitializeReferences()
        {
            s_animationReferenceItems = new();
            
            var allSets = ReflectionUtility.GetAllDerivedTypes<AnimationSet>();
            foreach (var set in allSets)
            {
                var fields = set.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    var doesImplementIAnimation = field.FieldType.GetInterface("IAnimation") != null;
                    if (doesImplementIAnimation) //field.FieldType.IsAssignableFrom(typeof(ITransition)) || field.FieldType.IsAssignableFrom(typeof(IAnimation))) // TODO: Conslidate the two interfaces
                    {
                        s_animationReferenceItems.Add(new AnimationReferenceItem
                        {
                            SetType = set,
                            Field = field
                        });
                    }
                }
            }
        }
        
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect();
            
            string animLabel = ValueEntry.SmartValue.SetName == null ? "Select Animation" : 
                $"{ValueEntry.SmartValue.SetName.Split(',').First().Split('.').Last()} / {ValueEntry.SmartValue.AnimationName}";

            OdinSelector<AnimationReferenceItem>.DrawSelectorDropdown(rect, animLabel, ShowSelector);
        }

        private OdinSelector<AnimationReferenceItem> ShowSelector(Rect rect)
        {
                        // Initialize Selector
            var selector = new GenericSelector<AnimationReferenceItem>("", false, 
                s_animationReferenceItems
                    .Select(x => new GenericSelectorItem<AnimationReferenceItem>($"{x.SetType.Name} / {x.Field.Name}", x)));

            //selector.CheckboxToggle = false;
            selector.SelectionTree.Config.DrawScrollView = true;
            selector.EnableSingleClickToSelect();

            foreach (var menuItem in selector.SelectionTree.EnumerateTree())
            {
                // Assign folder items to drop down menu itmes
                if (menuItem.ChildMenuItems.Count > 0 && menuItem.Value == null)
                {
                    menuItem.Icon = EditorIcons.Folder.Raw;
                }
                else if (menuItem.Value  != null)
                {
                    menuItem.Icon = EditorGUIUtility.IconContent("AnimationClip Icon").image;
                }  
            }

            selector.SelectionConfirmed += (selection) =>
            {
                AnimationReferenceItem anim = selection.FirstOrDefault();
                
                var animFinal = this.ValueEntry.SmartValue;
                animFinal.SetName = anim.SetType.AssemblyQualifiedName; // NOTE: use this instead of FullName for compatibility w/ AoT or Il2cpp compiling
                animFinal.AnimationName = anim.Field.Name;
                // Update the smart value with the new animation reference
                this.ValueEntry.SmartValue = animFinal;
                
            };

            // This is kind of expensive but its ok, we only do it once when the buttom is clicked
            selector.SetSelection(s_animationReferenceItems.FirstOrDefault(x => 
                x.SetType.Name == ValueEntry.SmartValue.SetName && 
                x.Field.Name == ValueEntry.SmartValue.AnimationName));
            
            selector.ShowInPopup(rect, Vector2.zero);

            return selector;
        }
    }

    /// <summary>
    /// Custom validator for AnimationReference, ensures that the reference is valid and points to an existing animation set and animation
    /// </summary>
    public class AnimationReferenceValidator : ValueValidator<AnimationReference>
    {
        protected override void Validate(ValidationResult result)
        {
            if (!AnimationReference.Validate(Value, out var setType, out var field, out var errorMsg))
            {
                result.AddError(errorMsg).WithFix(Value.Reset);
            }
        }
    }
}