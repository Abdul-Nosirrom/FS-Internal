using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FS.Extensions;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.Validation;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

[assembly: RegisterValidator(typeof(FS.GameplayActions.Editor.ActionReferenceValidator))]

namespace FS.GameplayActions.Editor 
{
    /// <summary>
    /// Drop down selector for ActionReference, shows sets as Foldouts and animations in sets as selectables
    /// </summary>
    public class ActionReferenceEditor : OdinValueDrawer<ActionReference>
    {
        private struct ActionReferenceItem
        {
            public Type SetType;
            public FieldInfo Field;
        }
        
        private static List<ActionReferenceItem> s_actionReferenceItems;
        
        [InitializeOnLoadMethod]
        private static void InitializeReferences()
        {
            s_actionReferenceItems = new();
            
            var allSets = ReflectionUtility.GetAllDerivedTypes<GameplayActionSet>();
            foreach (var set in allSets)
            {
                var fields = set.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    var isGameplayAction = typeof(GameplayAction).IsAssignableFrom(field.FieldType);
                    if (isGameplayAction)
                    {
                        s_actionReferenceItems.Add(new ActionReferenceItem
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
            
            string actionLabel = ValueEntry.SmartValue.SetName == null ? "Select Action" : 
                $"{ValueEntry.SmartValue.SetName.Split(',').First().Split('.').Last()} / {ValueEntry.SmartValue.ActionName}";

            OdinSelector<ActionReferenceItem>.DrawSelectorDropdown(rect, actionLabel, ShowSelector);
        }

        private OdinSelector<ActionReferenceItem> ShowSelector(Rect rect)
        {
            // Initialize Selector
            var selector = new GenericSelector<ActionReferenceItem>("", false, 
                s_actionReferenceItems
                    .Select(x => new GenericSelectorItem<ActionReferenceItem>($"{x.SetType.Name} / {x.Field.Name}", x)));

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
                    menuItem.Icon = EditorGUIUtility.IconContent("NavMeshAgent Icon").image;
                }  
            }

            selector.SelectionConfirmed += (selection) =>
            {
                if (selection == null) return;
                
                ActionReferenceItem anim = selection.FirstOrDefault();
                
                var animFinal = this.ValueEntry.SmartValue;
                animFinal.SetName = anim.SetType.AssemblyQualifiedName; // NOTE: use this instead of FullName for compatibility w/ AoT or Il2cpp compiling
                animFinal.ActionName = anim.Field.Name;
                // Update the smart value with the new animation reference
                this.ValueEntry.SmartValue = animFinal;
            };

            // This is kind of expensive but its ok, we only do it once when the buttom is clicked
            selector.SetSelection(s_actionReferenceItems.FirstOrDefault(x => 
                x.SetType.Name == ValueEntry.SmartValue.SetName && 
                x.Field.Name == ValueEntry.SmartValue.ActionName));
            
            selector.ShowInPopup(rect, Vector2.zero);

            return selector;
        }
    }

    /// <summary>
    /// Custom validator for ActionReference, ensures that the reference is valid and points to an existing animation set and animation
    /// </summary>
    public class ActionReferenceValidator : ValueValidator<ActionReference>
    {
        protected override void Validate(ValidationResult result)
        {
            if (!ActionReference.Validate(Value, out var setType, out var field, out var errorMsg))
            {
                result.AddError(errorMsg).WithFix(Value.Reset);
            }
        }
    }
}