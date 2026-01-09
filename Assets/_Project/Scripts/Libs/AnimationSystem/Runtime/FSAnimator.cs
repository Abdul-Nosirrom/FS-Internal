using System;
using System.Collections.Generic;
using Animancer;
using FS.GameplayActions;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
#endif

namespace FS.Animation
{
    public interface IActionAnimationFlagReciever
    {
        public void OnAnimationFlag(AnimationFlags flags);
    }
    
    /// <summary>
    /// Simple struct to hold references to animation sets and their owning controller
    /// </summary>
    public struct AnimationSetContainer
    {
        public AnimationController Owner { get; private set; }
        public AnimationSet Set { get; private set; }
        
        public AnimationSetContainer(AnimationController owner, AnimationSet set)
        {
            Owner = owner;
            Set = set;
        }
    }
    
    // Component for testing at the moment, not final
    public class FSAnimator : AnimancerComponent
    {
        /// <summary>
        /// Gameobject on the hierarchy that contains the animation controllers for this animator.
        /// </summary>
        public GameObject m_animationControllerObject;
        private Dictionary<Type, AnimationSetContainer> m_animationSets = new();

        // private void Awake()
        // {
        //     var controllers = m_animationControllerObject?.GetComponents<AnimationController>();
        //     if (controllers == null || controllers.Length == 0)
        //     {
        //         Debug.LogWarning($"[AnimationSystem] No AnimationControllers found on {m_animationControllerObject.name}. Please assign a valid GameObject with AnimationControllers to the FSAnimator.");
        //         return;
        //     }
        //     foreach (var controller in controllers)
        //     {
        //         if (controller == null) continue;
        //         var animationSets = controller.AnimationSets;
        //         if (animationSets == null || animationSets.Length == 0) continue;
        //
        //         PushAnimationSets(controller, animationSets);
        //     }
        // }

        private void LateUpdate()
        {
            FadeOutEmptyLayers();
        }

        public void PushAnimationSets(AnimationController controller, AnimationSet[] animationSets)
        {
            foreach (var set in animationSets)
            {
                if (set == null) continue;

                if (m_animationSets.ContainsKey(set.GetType()))
                {
                    Debug.LogError($"[AnimationSystem] AnimationSet of type {set.GetType().Name} already exists. Overwriting the existing set.");
                }
                
                m_animationSets[set.GetType()] = new AnimationSetContainer(controller, set);
            }
        }
        
        public AnimationSet GetAnimationSet(Type type)
        {
            if (m_animationSets.TryGetValue(type, out var setContainer))
            {
                return setContainer.Set;
            }
            
            Debug.LogWarning($"[AnimationSystem] AnimationSet of type {type.Name} not found.");
            return null;
        }

        
        public void FadeLayer(FSAnimationLayer layer, float targetWeight, float duration = 0.2f)
        {
            int layerIdx = (int)layer;
            if (layerIdx < 0 || layerIdx >= Layers.Count)
            {
                Debug.LogError($"[AnimationSystem] Invalid layer index {layerIdx} for layer {layer}. Cannot fade.");
                return;
            }
            
            var animLayer = Layers[layerIdx];
            if (Mathf.Abs(animLayer.TargetWeight - targetWeight) > Mathf.Epsilon)
                animLayer.StartFade(targetWeight, duration);
        }
        
        private void FadeOutEmptyLayers()
        {
            if (Layers.Count <= 1) return; // Only layer 0 exists
    
            for (int layerIdx = 1; layerIdx < Layers.Count; layerIdx++)
            {
                var layer = Layers[layerIdx];
        
                if (!layer.IsValid()) continue;
        
                // Skip if already fading out
                if (layer.TargetWeight == 0f) continue;
        
                // Check if non-looping animation has finished
                if (!layer.CurrentState.IsLooping && 
                    layer.CurrentState.NormalizedTime >= layer.CurrentState.NormalizedEndTime)
                {
                    FadeLayer((FSAnimationLayer)layerIdx, 0f);
                }
            }
        }
        
        public T GetAnimationSet<T>() where T : AnimationSet => GetAnimationSet(typeof(T)) as T;

        private ActionController m_actionController;
        private void Awake()
        {
            m_actionController = GetComponentInParent<ActionController>();
        }

        public event Action<AnimationFlags> OnAnimationFlagBroadcast;
        public void BroadcastAnimationFlag(AnimationFlags flags)
        {
            OnAnimationFlagBroadcast?.Invoke(flags);
            if (m_actionController)
            {
                foreach (var action in m_actionController.IterateActiveActions<IActionAnimationFlagReciever>())
                {
                    action.OnAnimationFlag(flags);
                }
            }
        }
    }
    
#if UNITY_EDITOR 
    [CustomEditor(typeof(FSAnimator))]
    public class FSAnimatorEditor : OdinEditor
    {
    }
#endif
}