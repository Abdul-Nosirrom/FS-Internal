using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using AnimatorController = UnityEditor.Animations.AnimatorController;
#endif

namespace FS.Animation
{
    /// <summary>
    /// Provides scoped "AnyState" functionality for sub-state machines.
    /// <para>
    /// Attach to a sub-state machine to propagate its parent-level exit routing transitions
    /// to every state inside it. States no longer need to connect to the Exit node —
    /// the SMB evaluates the routing conditions every frame and crossfades directly.
    /// </para>
    /// </summary>
    public class ScopedAnyStateTransition : StateMachineBehaviour
    {
        #region Serialized Data

        [SerializeField, HideInInspector] private BakedTransition[] m_bakedTransitions = Array.Empty<BakedTransition>();

        #endregion

        #region Runtime State

        [NonSerialized] private bool m_bInTransition;
        [NonSerialized] private AnimatorControllerPlayable m_controllerPlayable;
        [NonSerialized] private bool m_bPlayableResolved;

        #endregion

        #region StateMachineBehaviour
        
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            m_bInTransition = false;

            if (!m_bPlayableResolved)
            {
                var animancer = animator.GetComponent<Animancer.AnimancerComponent>();
                if (animancer != null)
                {
                    // Find the active ControllerState playing this controller
                    foreach (var state in animancer.States)
                    {
                        if (state is Animancer.ControllerState controllerState)
                        {
                            m_controllerPlayable = controllerState.Playable;
                            m_bPlayableResolved = true;
                            break;
                        }
                    }
                }
            }
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (!m_bPlayableResolved || !m_controllerPlayable.IsValid())
            {
                m_bPlayableResolved = false; // Allow re-resolution next OnStateEnter
                return;
            }
            
            if (m_bInTransition)
                return;

            if (m_bakedTransitions == null || m_bakedTransitions.Length == 0)
                return;
            
            for (int i = 0; i < m_bakedTransitions.Length; i++)
            {
                ref var transition = ref m_bakedTransitions[i];

                if (stateInfo.fullPathHash == transition.DestinationStateHash)
                    continue;

                if (!transition.EvaluateConditions(m_controllerPlayable))
                    continue;
                
                transition.ConsumeConditions(m_controllerPlayable);
                m_controllerPlayable.CrossFadeInFixedTime(transition.DestinationStateHash, transition.CrossfadeDuration, layerIndex, 0f);
                
                m_bInTransition = true;
                return;
            }
        }

        #endregion

        #region Baked Transition

        [Serializable]
        public struct BakedTransition
        {
            [SerializeField] private string m_name;
            [SerializeField] private string m_fullPath;
            [SerializeField] private int m_destinationStateHash;
            [SerializeField] private float m_crossfadeDuration;
            [SerializeField] private BakedCondition[] m_conditions;

            public string Name => m_name;
            public string FullPath => m_fullPath;
            public int DestinationStateHash => m_destinationStateHash;

            public float CrossfadeDuration
            {
                get => m_crossfadeDuration;
                set => m_crossfadeDuration = value;
            }

            public BakedCondition[] Conditions => m_conditions;

            public bool EvaluateConditions(AnimatorControllerPlayable animator)
            {
                if (m_conditions == null || m_conditions.Length == 0)
                    return false;

                for (int i = 0; i < m_conditions.Length; i++)
                {
                    if (!m_conditions[i].Evaluate(animator))
                        return false;
                }

                return true;
            }

            public void ConsumeConditions(AnimatorControllerPlayable animator)
            {
                if (m_conditions == null) return;
                for (int i = 0; i < m_conditions.Length; i++)
                    m_conditions[i].Consume(animator);
            }

#if UNITY_EDITOR
            public static BakedTransition Create(
                AnimatorTransition source,
                AnimatorController controller,
                int layerIndex,
                AnimatorStateMachine rootSM,
                string layerName)
            {
                var targetState = ResolveDestinationState(source, controller, layerIndex);

                string fullPath = "";
                int fullPathHash = 0;
                string displayName = "???";

                if (targetState != null)
                {
                    fullPath = BuildFullStatePath(rootSM, targetState, layerName);
                    fullPathHash = Animator.StringToHash(fullPath);
                    displayName = targetState.name;
                }

                var conditions = source.conditions
                    .Select(c => BakedCondition.Create(c, controller))
                    .ToArray();

                return new BakedTransition
                {
                    m_name = displayName,
                    m_fullPath = fullPath,
                    m_destinationStateHash = fullPathHash,
                    m_crossfadeDuration = 0.25f,
                    m_conditions = conditions
                };
            }

            private static AnimatorState ResolveDestinationState(
                AnimatorTransition transition,
                AnimatorController controller,
                int layerIndex)
            {
                if (transition.destinationState != null)
                    return transition.destinationState;

                if (transition.destinationStateMachine != null)
                    return ResolveStateMachineEntry(transition.destinationStateMachine);

                return controller.layers[layerIndex].stateMachine.defaultState;
            }

            private static AnimatorState ResolveStateMachineEntry(AnimatorStateMachine sm)
            {
                if (sm.entryTransitions != null)
                {
                    foreach (var entry in sm.entryTransitions)
                    {
                        if (entry.destinationState != null)
                            return entry.destinationState;
                        if (entry.destinationStateMachine != null)
                            return ResolveStateMachineEntry(entry.destinationStateMachine);
                    }
                }

                return sm.defaultState;
            }

            private static string BuildFullStatePath(AnimatorStateMachine rootSM, AnimatorState targetState, string layerName)
            {
                var segments = new List<string> { layerName };
                if (FindStateRecursive(rootSM, targetState, segments))
                    return string.Join(".", segments);

                return layerName + "." + targetState.name;
            }

            private static bool FindStateRecursive(AnimatorStateMachine sm, AnimatorState target, List<string> path)
            {
                foreach (var childState in sm.states)
                {
                    if (childState.state == target)
                    {
                        path.Add(target.name);
                        return true;
                    }
                }

                foreach (var childSM in sm.stateMachines)
                {
                    path.Add(childSM.stateMachine.name);
                    if (FindStateRecursive(childSM.stateMachine, target, path))
                        return true;
                    path.RemoveAt(path.Count - 1);
                }

                return false;
            }
#endif
        }

        #endregion

        #region Baked Condition

        [Serializable]
        public struct BakedCondition
        {
            private const float k_floatEpsilon = 0.0001f;

            public enum ConditionMode
            {
                If = 1,
                IfNot = 2,
                Greater = 3,
                Less = 4,
                Equals = 6,
                NotEqual = 7
            }

            public enum ParamType
            {
                Float = 1,
                Int = 3,
                Bool = 4,
                Trigger = 9
            }

            [SerializeField] private string m_paramName;
            [SerializeField] private ParamType m_paramType;
            [SerializeField] private int m_paramHash;
            [SerializeField] private ConditionMode m_mode;
            [SerializeField] private float m_threshold;

            public string ParamName => m_paramName;
            public ConditionMode Mode => m_mode;
            public float Threshold => m_threshold;

#if UNITY_EDITOR
            public static BakedCondition Create(AnimatorCondition source, AnimatorController controller)
            {
                var param = controller.parameters.FirstOrDefault(
                    p => p.nameHash == Animator.StringToHash(source.parameter));

                return new BakedCondition
                {
                    m_paramName = source.parameter,
                    m_paramType = (ParamType)param.type,
                    m_paramHash = param.nameHash,
                    m_mode = (ConditionMode)source.mode,
                    m_threshold = source.threshold
                };
            }
#endif

            public bool Evaluate(AnimatorControllerPlayable animator)
            {
                switch (m_paramType)
                {
                    case ParamType.Float:
                    {
                        float value = animator.GetFloat(m_paramHash);
                        return m_mode switch
                        {
                            ConditionMode.Greater  => value > m_threshold,
                            ConditionMode.Less     => value < m_threshold,
                            ConditionMode.Equals   => Mathf.Abs(value - m_threshold) < k_floatEpsilon,
                            ConditionMode.NotEqual => Mathf.Abs(value - m_threshold) >= k_floatEpsilon,
                            _ => false
                        };
                    }
                    case ParamType.Int:
                    {
                        int value = animator.GetInteger(m_paramHash);
                        return m_mode switch
                        {
                            ConditionMode.Greater  => value > (int)m_threshold,
                            ConditionMode.Less     => value < (int)m_threshold,
                            ConditionMode.Equals   => value == (int)m_threshold,
                            ConditionMode.NotEqual => value != (int)m_threshold,
                            _ => false
                        };
                    }
                    case ParamType.Bool:
                    {
                        bool value = animator.GetBool(m_paramHash);
                        return m_mode switch
                        {
                            ConditionMode.If    => value,
                            ConditionMode.IfNot => !value,
                            _ => false
                        };
                    }
                    case ParamType.Trigger:
                        return animator.GetBool(m_paramHash);
                    default:
                        return false;
                }
            }

            public void Consume(AnimatorControllerPlayable animator)
            {
                if (m_paramType == ParamType.Trigger)
                    animator.ResetTrigger(m_paramHash);
            }
        }

        #endregion

        #region Editor — Baking

#if UNITY_EDITOR
        [ContextMenu("Rebake Transitions")]
        private void ForceBake()
        {
            BakeTransitions();
        }

        private void OnValidate()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    BakeTransitions();
            };
        }

        public void BakeTransitions()
        {
            var contexts = AnimatorController.FindStateMachineBehaviourContext(this);
            if (contexts == null || contexts.Length == 0)
            {
                Debug.LogWarning("[ScopedAnyStateSMB] No context found. Is this attached to a sub-state machine?");
                return;
            }

            // Preserve user-tweaked crossfade durations keyed by full path
            var previousCrossfades = new Dictionary<string, float>();
            if (m_bakedTransitions != null)
            {
                foreach (var t in m_bakedTransitions)
                {
                    if (!string.IsNullOrEmpty(t.FullPath))
                        previousCrossfades[t.FullPath] = t.CrossfadeDuration;
                }
            }

            var bakedList = new List<BakedTransition>();

            foreach (var context in contexts)
            {
                if (context.animatorObject is not AnimatorStateMachine thisSM)
                    continue;

                var layer = context.animatorController.layers[context.layerIndex];
                var rootSM = layer.stateMachine;
                var layerName = layer.name;

                var parentSM = FindParentStateMachine(rootSM, thisSM);
                if (parentSM == null)
                {
                    Debug.LogWarning($"[ScopedAnyStateSMB] Could not find parent for '{thisSM.name}'. Don't attach this to the root state machine.");
                    continue;
                }

                var exitTransitions = parentSM.GetStateMachineTransitions(thisSM);
                if (exitTransitions == null || exitTransitions.Length == 0)
                    continue;

                foreach (var transition in exitTransitions)
                {
                    // Skip unconditional — those are for normal Mecanim exit routing
                    if (transition.conditions == null || transition.conditions.Length == 0)
                        continue;

                    var baked = BakedTransition.Create(
                        transition,
                        context.animatorController,
                        context.layerIndex,
                        rootSM,
                        layerName);

                    if (baked.DestinationStateHash == 0)
                    {
                        Debug.LogWarning($"[ScopedAnyStateSMB] Failed to resolve destination for transition on '{thisSM.name}'");
                        continue;
                    }

                    // Restore previous crossfade
                    if (previousCrossfades.TryGetValue(baked.FullPath, out float prev))
                        baked.CrossfadeDuration = prev;

                    bakedList.Add(baked);
                }
            }

            m_bakedTransitions = bakedList.ToArray();

            EditorUtility.SetDirty(this);
        }

        private static AnimatorStateMachine FindParentStateMachine(AnimatorStateMachine root, AnimatorStateMachine child)
        {
            if (root == child)
                return null;

            foreach (var childSM in root.stateMachines)
            {
                if (childSM.stateMachine == child)
                    return root;

                var found = FindParentStateMachine(childSM.stateMachine, child);
                if (found != null)
                    return found;
            }

            return null;
        }
#endif

        #endregion
    }

    // ═══════════════════════════════════════════════════════════════
    // Custom Inspector
    // ═══════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [CustomEditor(typeof(ScopedAnyStateTransition))]
    public class ScopedAnyStateTransitionEditor : UnityEditor.Editor
    {
        private SerializedProperty m_bakedTransitionsProp;

        private void OnEnable()
        {
            m_bakedTransitionsProp = serializedObject.FindProperty("m_bakedTransitions");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var smb = (ScopedAnyStateTransition)target;

            // Rebake button
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Rebake Transitions", GUILayout.Height(24)))
            {
                smb.BakeTransitions();
                serializedObject.Update();
            }

            EditorGUILayout.Space(4);

            if (m_bakedTransitionsProp == null || m_bakedTransitionsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No transitions baked.\n\n" +
                    "• Attach this to a sub-state machine (not a state)\n" +
                    "• Define routing transitions on this SM from its parent level\n" +
                    "• Transitions must have conditions (unconditional ones are skipped)\n\n" +
                    "Click 'Rebake Transitions' after setup.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Baked Transitions ({m_bakedTransitionsProp.arraySize})", EditorStyles.boldLabel);

                for (int i = 0; i < m_bakedTransitionsProp.arraySize; i++)
                {
                    var element = m_bakedTransitionsProp.GetArrayElementAtIndex(i);
                    var nameProp = element.FindPropertyRelative("m_name");
                    var fullPathProp = element.FindPropertyRelative("m_fullPath");
                    var hashProp = element.FindPropertyRelative("m_destinationStateHash");
                    var crossfadeProp = element.FindPropertyRelative("m_crossfadeDuration");
                    var conditionsProp = element.FindPropertyRelative("m_conditions");

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // Header with index
                    EditorGUILayout.LabelField($"[{i}] → {nameProp.stringValue}", EditorStyles.boldLabel);

                    // Full path (read-only)
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField("Path", fullPathProp.stringValue);
                    EditorGUI.EndDisabledGroup();

                    // Crossfade duration (editable)
                    crossfadeProp.floatValue = EditorGUILayout.Slider("Crossfade", crossfadeProp.floatValue, 0f, 1f);

                    // Conditions (read-only)
                    if (conditionsProp != null && conditionsProp.arraySize > 0)
                    {
                        EditorGUILayout.LabelField("Conditions:", EditorStyles.miniLabel);

                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUI.indentLevel++;
                        for (int j = 0; j < conditionsProp.arraySize; j++)
                        {
                            var cond = conditionsProp.GetArrayElementAtIndex(j);
                            var pName = cond.FindPropertyRelative("m_paramName");
                            var mode = cond.FindPropertyRelative("m_mode");
                            var threshold = cond.FindPropertyRelative("m_threshold");

                            string modeStr = ((ScopedAnyStateTransition.BakedCondition.ConditionMode)mode.intValue).ToString();
                            EditorGUILayout.LabelField($"{pName.stringValue} {modeStr} {threshold.floatValue}");
                        }
                        EditorGUI.indentLevel--;
                        EditorGUI.EndDisabledGroup();
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}