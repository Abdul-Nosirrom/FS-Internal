using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FS.Collections;
using UnityEngine;

namespace FS.DataStructures.Graphs
{
    [Serializable]
    public class GraphNode<T> : IEquatable<GraphNode<T>>
    {
        [SerializeField] private T m_node;
        [SerializeField] private List<T> m_edges = new();
        
        public T Node => m_node;
        public List<T> Edges => m_edges;
        public int EdgeCount => m_edges.Count;
        
        public GraphNode(T node)
        {
            m_node = node;
        }
        
        public void AddEdge(T edge)
        {
            if (m_edges.Contains(edge)) return;
            m_edges.Add(edge);
        }
        
        public void RemoveEdge(T edge)
        {
            if (!m_edges.Contains(edge)) return;
            m_edges.Remove(edge);
        }
        
        public bool HasEdge(T edge) => m_edges.Contains(edge);
        public bool HasAnyEdges() => m_edges.Count > 0;
        
        public bool Equals(GraphNode<T> other) => other != null && m_node.Equals(other.m_node);

        public override bool Equals(object obj) => obj is T node && node.Equals(m_node);
        public override int GetHashCode() => m_node.GetHashCode();
    }
    
    [Serializable]
    public class DirectedCyclicGraph<T>
    {
        [SerializeField] private SerializableDictionary<T, GraphNode<T>> m_nodes = new();
        [SerializeField] private List<T> m_rootNodes = new();
        
        
        public IReadOnlyList<GraphNode<T>> Nodes => m_nodes.Values.ToList();
        public IReadOnlyList<T> RootNodes => m_rootNodes;
        public IReadOnlyList<T> Edges => m_nodes.Values.SelectMany(node => node.Edges).ToList();
        public List<T> NodeEdges(T node) => HasNode(node) ? m_nodes[node].Edges : new();
        
        public bool HasNode(T node) => m_nodes.ContainsKey(node);
        public bool IsRootNode(T node) => m_rootNodes.Contains(node);

        /// <summary>
        /// Updates the root node array by checking all nodes and seeing which ones have no incoming edges.
        /// Returns true if the root nodes were changed.
        /// </summary>
        public bool ValidateRootNodes()
        {
            bool isDirty = false;
            
            // Start by removing any invalid root nodes, either not in the nodes array or is actually not a root
            for (int r = m_rootNodes.Count - 1; r >= 0; r--)
            {
                var node = m_rootNodes[r];
                if (node == null || !m_nodes.ContainsKey(node)) 
                {
                    m_rootNodes.RemoveAt(r); // ghost node
                    isDirty = true;
                }
                else if (m_nodes.Values.ToList().FindAll(n => n.HasEdge(node)).Count > 0) 
                {
                    m_rootNodes.RemoveAt(r); // not actually a root
                    isDirty = true;
                }
            }
            
            // Add in any nodes that SHOULD be roots but arent in the root nodes array
            foreach (var graphNode in m_nodes.Values)
            {
                if (m_rootNodes.Contains(graphNode.Node)) continue;

                bool shouldBeRoot = m_nodes.Values.ToList().FindAll(n => n.HasEdge(graphNode.Node)).Count == 0;
                if (!shouldBeRoot) continue;
                
                m_rootNodes.Add(graphNode.Node);
                isDirty = true;
            }
            
            return isDirty;
        }
        
        /// <summary>
        /// Sorts the root nodes using the provided comparison function. Returns true if the order was changed.
        /// </summary>
        public bool SortRootNodes(Comparison<T> comparison)
        {
            var originalOrder = m_rootNodes.ToList();
            m_rootNodes.Sort(comparison);
            return !m_rootNodes.SequenceEqual(originalOrder);
        }
        
        public void AddNode(T node)
        {
            if (HasNode(node)) return;

            m_nodes.Add(node, new GraphNode<T>(node));
            m_rootNodes.Add(node);
        }
        
        public void RemoveNode(T node)
        {
            if (!HasNode(node)) return;

            m_nodes.Remove(node);
            
            if (IsRootNode(node)) m_rootNodes.Remove(node);
            
            // Remove all edges that contain this node
            foreach (var graphNode in m_nodes.Values) graphNode.RemoveEdge(node);
        }
        
        public void AddEdge(T from, T to)
        {
            if (!HasNode(from) || !HasNode(to)) return;

            m_nodes[from].AddEdge(to);
            
            // Remove 'to' from root nodes
            if (m_rootNodes.Contains(to)) m_rootNodes.Remove(to);
        }
        
        public void RemoveEdge(T from, T to)
        {
            if (!HasNode(from) || !HasNode(to)) return;
            
            m_nodes[from].RemoveEdge(to);
            
            // Maybe add 'to' node to root nodes
            if (m_nodes.Values.ToList().FindAll(node => node.HasEdge(to)).Count == 0) m_rootNodes.Add(to);
        }
        
        public bool HasEdge(T from, T to)
        {
            if (!HasNode(from) || !HasNode(to)) return false;

            return m_nodes[from].HasEdge(to);
        }

        public bool HasAnyEdges(T node)
        {
            return HasNode(node) && m_nodes[node].HasAnyEdges();
        }
    }
}