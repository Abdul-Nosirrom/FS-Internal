using System;
using System.Linq;
using FS.DataStructures.Graphs;
using FS.Extensions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FS.GameplayActions
{
    /// <summary>
    /// Interface for conditions of actions that are input evaluations. To seperate them out from regular
    /// conditionals (nothing stopping you from putting it there, but for organization & better debugging by
    /// seperating the two out)
    /// </summary>
    public interface IActionInputEvaluator
    {
        public bool InputEvaluate();
    }
    
    /// <summary>
    /// Action Selector for a PlayerActionGraph type of action set composer
    /// 
    /// Higher default execution order so we can create the runtime instance of the graph before the ActionController
    /// initializes itself. So long as our Awake executes before the ActionController Awake, we're good.
    /// </summary>
    [RequireComponent(typeof(ActionController))]
    public class PlayerStateSelector : MonoBehaviour
    {
        [SerializeField] public DirectedCyclicGraph<GameplayAction> m_actionGraph;
        //[SerializeField] private PlayerActionGraph m_actionGraph;
        private ActionController m_actionController;

        private void Awake()
        {
            m_actionController = GetComponent<ActionController>();
        }


        private void Update()
        {
            ActionChannel newlyOccupiedChannels = 0;
            
            // Foreach followup in active actions, try activating
            foreach (var activeAction in m_actionController.ActiveActions.Actions.ToHashSet())
            {
                foreach (var followUp in m_actionGraph.NodeEdges(activeAction))
                {
                    if ((followUp.Channels & newlyOccupiedChannels) != 0) continue;

                    if (followUp is IActionInputEvaluator inputAction && !inputAction.InputEvaluate()) continue;
                    if (followUp.TryStartAction(activeAction)) newlyOccupiedChannels |= followUp.Channels;
                }
            }
            
            // Foreach root action, try activating
            foreach (var rootAction in m_actionGraph.RootNodes)
            {
                if ((rootAction.Channels & newlyOccupiedChannels) != 0) continue;
                
                if (rootAction is IActionInputEvaluator inputAction && !inputAction.InputEvaluate()) continue;
                if (rootAction.TryStartAction()) newlyOccupiedChannels |= rootAction.Channels;
            }
        }
        
        public bool IsRootAction(GameplayAction action) => m_actionGraph.IsRootNode(action);
        
#if UNITY_EDITOR

        public void OnValidate()
        {
            // Ensure graph contains no null nodes in the event a component was deleted or something
            for (int n = m_actionGraph.Nodes.Count - 1; n >= 0; n--)
            {
                var node = m_actionGraph.Nodes[n];
                if (node.Node == null)
                {
                    m_actionGraph.RemoveNode(node.Node);
                    continue;
                }
                
                // Ensure followups are valid
                foreach (var followUp in node.Edges)
                {
                    if (!IsFollowupValid(node.Node, followUp, false))
                    {
                        Debug.LogWarning($"Deleting invalid followup {followUp} for {node.Node} during Validation!");
                        m_actionGraph.RemoveEdge(node.Node, followUp);
                    }
                }
            }
        }

        public bool IsFollowupValid(GameplayAction action, GameplayAction followUp, bool bCheckForAdd)
        {
            // Does it exist?
            if (followUp == null) return false;

            // Is it the same as the action?
            if (action == followUp) return false;
            
            // If we're looking to add a followup, ensure it doesnt already exist. Otherwise we good.
            return !bCheckForAdd || !m_actionGraph.HasEdge(action, followUp);
        }
#endif        
    }
}