using System.Collections;
using FS.Math;
using UnityEngine;
using Pathfinding;
using Sirenix.OdinInspector;

namespace FS.AI
{
    /// <summary>
    /// Base class for all NavLinks. NavLinks are used to connect different parts of the navigation mesh and allow
    /// for more complex navigation behaviors.
    /// </summary>
    public abstract class BaseNavLink : NodeLink2
    {
        [Button("Add End Point")]
        private void AddEndPoint()
        {
            if (EndTransform) return;
            
            var endPoint = new GameObject($"{name}_EndPoint");
            endPoint.transform.SetParent(transform, false);
        }
        
        public abstract IEnumerator TraverseLink(PhysicsController physics, Vector3 startPoint, Vector3 endPoint);
    }
}