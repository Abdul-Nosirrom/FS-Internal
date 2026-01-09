using System;
using System.Linq;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR 
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
#endif

namespace FS.Animation
{
    /// <summary>
    /// Simple interface implemented by Animation types for both animancer compatibility and convenience in how we
    /// set up our AnimationReference type (needing to only search for this interface) with helpful Play functions
    /// </summary>
    public interface IAnimation : ITransition
    {
        public AnimancerState Play(AnimancerComponent animator);
        public AnimancerState Play(AnimancerComponent animator, FSAnimationLayer layer);

        public ITransition GetTransition();


        AnimancerState ITransition.CreateState() => GetTransition().CreateState();
        void ITransition.Apply(AnimancerState state) => GetTransition().Apply(state);

        object IHasKey.Key => GetTransition().Key;

        bool ITransition.IsValid => GetTransition().IsValid;

        float ITransition.Speed
        {
            get => GetTransition().Speed;
            set => GetTransition().Speed = value;
        }

        bool ITransition.IsLooping => GetTransition().IsLooping;
        
        float ITransition.FadeDuration => GetTransition().FadeDuration;
        FadeMode ITransition.FadeMode => GetTransition().FadeMode;

        float ITransition.NormalizedStartTime
        {
            get => GetTransition().NormalizedStartTime;
            set => GetTransition().NormalizedStartTime = value;
        }

        float ITransition.MaximumLength => GetTransition().MaximumLength;
        
        AnimancerEvent.Sequence IHasEvents.Events => GetTransition().Events;

        AnimancerEvent.Sequence.Serializable IHasEvents.SerializedEvents
        {
            get => GetTransition().SerializedEvents;
            set => GetTransition().SerializedEvents = value;
        }
    }

    public static class IAnimationExtensions
    {
        public static AnimancerState GetState(this IAnimation animation, FSAnimator animator, bool createIfDoesntExist = false)
        {
            var transition = animation.GetTransition();
            if (transition == null) return null;

            if (createIfDoesntExist)
                return animator.States.GetOrCreate(transition);
            
            animator.States.TryGet(transition, out var state);
            return state;
        }
        
        public static bool TryGetState(this IAnimation animation, FSAnimator animator, out AnimancerState state)
        {
            state = null;
            
            var transition = animation.GetTransition();
            if (transition == null) return false;

            return animator.States.TryGet(transition, out state);
        }

        public static bool FadeOutLayer(this IAnimation animation, FSAnimator animator, float fadeDuration = 0.1f, float targetWeight = 0f)
        {
            if (animation.TryGetState(animator, out var state))
            {
                state.Layer.StartFade(0f, fadeDuration);
                return true;
            }

            return false;
        }

        public static bool Stop(this IAnimation animation, FSAnimator animator, float fadeDuration = 0.1f)
        {
            if (animation.TryGetState(animator, out var state) && state.IsActive)
            {
                if (state.LayerIndex > 0) return animation.FadeOutLayer(animator, fadeDuration);
                state.StartFade(0, fadeDuration);
                return true;
            }
            return false;
        }

        public static bool IsActive(this IAnimation animation, FSAnimator animator)
        {
            if (animation.TryGetState(animator, out var state))
                return state.IsActive;
            return false;
        }
    }
    
    /// <summary>
    /// Simple wrapper to allow for interface serialization. Done in a very basic way by just validating that the assigned
    /// animation implements the interface.
    /// For example, SimpleLocomotionAnimation inherits from LinearMixer, Complex inherits from DirectionalMixer. We want to be
    /// able to say "We want a locomotion animation" in our sets without caring for what type it is. Now with this, the decalartion
    /// is simply:
    /// <code>
    /// public Animation[ILocomotionAnimation] LocomotionThroughInterface;
    ///
    /// public interface ILocomotionAnimation
    /// {
    ///     public void UpdateSpeedBlending(AnimancerState state, PhysicsController physics);
    /// }
    /// </code>
    /// </summary>
    /// <typeparam name="TInterface"></typeparam>
    [Serializable]
    public class Animation<TInterface> : IAnimation where TInterface : class
    {
        [SerializeField]
        public FSAnimation m_animation;

        public TInterface Value => m_animation as TInterface;
        
        public AnimancerState Play(AnimancerComponent animator) => m_animation?.Play(animator);
        public AnimancerState Play(AnimancerComponent animator, FSAnimationLayer layer) => m_animation?.Play(animator);
        
        public ITransition GetTransition() => m_animation?.GetTransition();
    }
    
#if UNITY_EDITOR 
    public class AnimationInterfaceEditor<TInterface> : OdinValueDrawer<Animation<TInterface>> where TInterface : class
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            // Check prefab change inside of ValueEntry.m_animation path
            var animProp = Property.Children["m_animation"];
            bool isPrefabDifferent = animProp.ValueEntry.ValueChangedFromPrefab;

            AnimationAssetSelector();

            if (isPrefabDifferent)
            {
                var color = new Color(0.0f, 0.5f, 1.0f, 1.0f); // Blue color
                var rect = GUILayoutUtility.GetLastRect();//new Rect(0, 0, 2, EditorGUIUtility.singleLineHeight);
                rect.width = 2;
                EditorGUI.DrawRect(rect, color);
            }
        }

        private void AnimationAssetSelector()
        {
            var value = ValueEntry.SmartValue;
            var animAsset = value.m_animation;

            string buttonLabel = animAsset == null ? "Select Animation" : animAsset.name;

            GUILayout.BeginHorizontal();
            OdinSelector<FSAnimation>.DrawSelectorDropdown(Property.Label, new GUIContent(buttonLabel), ShowSelector);

            // Button to ping the selected animation in the project window
            if (animAsset != null && GUILayout.Button(EditorIcons.MagnifyingGlass.Raw, GUILayout.Width(EditorGUIUtility.singleLineHeight), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                EditorGUIUtility.PingObject(animAsset);
            }
            
            GUILayout.EndHorizontal();
            
            var lastRect = GUILayoutUtility.GetLastRect();
            if (ValueEntry.SmartValue.m_animation == null)
                EditorGUI.DrawRect(lastRect, new Color(0.4f, 0f, 0f, 0.2f));
        }

        private OdinSelector<FSAnimation> ShowSelector(Rect rect)
        {
            var validAnims = AssetDatabase.FindAssets("t:FSAnimation")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<FSAnimation>(path))
                .Where(anim => anim is TInterface)
                .ToList();
            
            validAnims.Insert(0, null);
            
            // Initialize Selector
            var selector = new GenericSelector<FSAnimation>("", false, validAnims.Select(x => new GenericSelectorItem<FSAnimation>(x == null ? "None" : x.name, x)));
            selector.EnableSingleClickToSelect();
            
            foreach (var menuItem in selector.SelectionTree.EnumerateTree())
            {
                // Assign folder items to drop down menu itmes
                menuItem.Icon = EditorGUIUtility.IconContent("NavMeshAgent Icon").image;
            }

            selector.SelectionConfirmed += (selection) =>
            {
                if (selection == null) return;
                
                FSAnimation anim = selection.FirstOrDefault();
                
                var value = ValueEntry.SmartValue;
                
                Property.RecordForUndo("Animation Assignment");
                value.m_animation = anim;
                ValueEntry.SmartValue = value;
                ValueEntry.Property.MarkSerializationRootDirty();
            };

            // This is kind of expensive but its ok, we only do it once when the buttom is clicked
            selector.SetSelection(ValueEntry.SmartValue.m_animation);
            
            selector.ShowInPopup(rect, Vector2.zero);
            
            return selector;
        }
    }
#endif  
}