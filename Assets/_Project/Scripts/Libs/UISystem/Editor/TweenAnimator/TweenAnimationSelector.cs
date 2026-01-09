using System;
using System.Collections.Generic;
using System.Linq;
using FS.Extensions;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FS.UI.Editor
{
    public static class TweenAnimationPathUtility
    {
        public struct TweenNameAndCategory
        {
            public Type TweenType;
            public string TargetType;
            public string Name;
            
            public string FullPath => string.IsNullOrEmpty(TargetType) ? Name : $"{TargetType}/{Name}";
            
            public TweenNameAndCategory(Type tweenType)
            {
                TweenType = tweenType;
                TargetType = tweenType.GetField("Target")?.FieldType.Name;
                Name = TweenType.Name.Replace("Tween", "");
            }
        }

        public static List<TweenNameAndCategory> s_tweenAnimationData;
        
        [InitializeOnLoadMethod]
        public static void InitEventTypesAndPaths()
        {
            s_tweenAnimationData = new();

            var allTweens = ReflectionUtility.GetAllDerivedTypes<TweenAnimation>();

            foreach (var tweenType in allTweens)
            {
                s_tweenAnimationData.Add(new TweenNameAndCategory(tweenType));
            }
        }
    }
    
    /// <summary>
    /// Drop dawn drawer organized by EventPath for EventBase serialize reference types.
    /// High DrawPriority to override the basic SerializeReference drawer and provide a custom selector for EventBase types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [DrawerPriority(value: 3000)]
    public class TweenInstanceSelector<T> : OdinAttributeDrawer<SerializeReference, T> where T : TweenAnimation
    {
        protected override bool CanDrawAttributeValueProperty(InspectorProperty property)
        {
            return typeof(TweenAnimation).IsAssignableFrom(property.ValueEntry.TypeOfValue);
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var value = this.ValueEntry.SmartValue;
            Rect dropDownRect = EditorGUILayout.GetControlRect();

            string buttonLabel = value == null ? "Select Tween Animation" : value.GetType().Name;

            //GenericSelector<Type>.DrawSelectorDropdown(dropDownRect, buttonLabel, ShowSelector);

            OdinSelector<Type>.DrawSelectorDropdown(dropDownRect, new GUIContent(buttonLabel), ShowSelector, false,
                valueIcon: SdfIconType.Lightning);
            
            if (this.ValueEntry.SmartValue != null)
            {
                //EditorGUI.indentLevel++;
                for (int c = 0; c < this.ValueEntry.Property.Children.Count; c++)
                {
                    var child = this.ValueEntry.Property.Children[c];
                    child.Draw();
                }
                //EditorGUI.indentLevel--;
            }
        }

        private OdinSelector<Type> ShowSelector(Rect rect)
        {
            // Initialize Selector
            var selector = new GenericSelector<Type>("", false, 
                TweenAnimationPathUtility.s_tweenAnimationData
                    .Select(x => new GenericSelectorItem<Type>(x.FullPath, x.TweenType)));

            //selector.CheckboxToggle = false;
            selector.DrawConfirmSelectionButton = true;
            selector.SelectionTree.Config.DrawScrollView = true;
            //selector.EnableSingleClickToSelect();

            foreach (var menuItem in selector.SelectionTree.EnumerateTree())
            {
                // Assign folder items to drop down menu itmes
                if (menuItem.ChildMenuItems.Count > 0 && menuItem.Value == null)
                {
                    menuItem.Icon = EditorIcons.Folder.Raw;
                }
            }

            selector.SelectionConfirmed += (selection) =>
            {
                Type type = selection.Count() > 0 ? selection.First() : null;
                if (type != null && type != this.ValueEntry.TypeOfValue)
                {
                    // Create a new instance of the selected event type
                    var newEvent = Activator.CreateInstance(type);
                    this.ValueEntry.SmartValue = newEvent as T;
                }
            };

            selector.SetSelection(this.ValueEntry.TypeOfValue);
            selector.ShowInPopup(rect, Vector2.zero);// new Vector2(rect.width, 240));

            return selector;
        }
    }
}