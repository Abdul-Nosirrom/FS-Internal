using System;
using System.Collections;
using System.Collections.Generic;
using Drawing;
using FS.CombatSystem;
using FS.Math;
using FS.Player;
using FS.RuntimeDebug;
using Pathfinding;
using Pathfinding.Util;
using TimeUtils;
using UnityEngine;
using UnityEngine.Jobs;

namespace FS.AI
{
    [RequireComponent(typeof(PhysicsController), typeof(Seeker))]
    public class AINavigation : RichAI, IDebugProvider//MonoBehaviour, IEntityIndex, IDebugProvider
    {
	    protected override void Awake()
	    {
		    //if (!m_physics)
		    
		    base.Awake();
		    onTraverseOffMeshLink = TraverseLink;
	    }

	    protected override void Start()
	    {
		    base.Start();
		    {
			    m_physics = GetComponent<PhysicsController>();
			    if (m_physics) m_physics.RegisterVelocityPostProcessModifier((controller) => !enabled, SlowDownUponReachingTarget, "Pathfinding Velocity");
			    
			    m_hitStop = GetComponent<HitStopController>();
		    }
	    }

	    private HitStopController m_hitStop;
	    private PhysicsController m_physics;
	    public override void FindComponents()
	    {
		    base.FindComponents();
		    // if (!m_physics)
		    // {
			   //  m_physics = GetComponent<PhysicsController>();
			   //  if (m_physics) m_physics.RegisterVelocityPostProcessModifier((controller) => !enabled, SlowDownUponReachingTarget, "Pathfinding Velocity");
		    // }
	    }

	    private void SlowDownUponReachingTarget(ref Vector3 currentVelocity)
	    {
		    if (m_hitStop && m_hitStop.HitStopActive) return;
		    if (!hasPath) return;

		    var vel3D = new Vector3(velocity2D.x, 0, velocity2D.y);
		    var planeRot = Quaternion.FromToRotation(Vector3.up, m_physics.UpDirection);
		    if (!m_physics.IsGrounded) vel3D += currentVelocity.Dot(m_physics.UpDirection) * m_physics.UpDirection;
		    vel3D = planeRot * vel3D;
		    if (!traversingOffMeshLink) currentVelocity = vel3D;
		    
		    //m_physics.VerticalPhysics(ref currentVelocity, deltaTime);

		    //if (reachedEndOfPath) currentVelocity = Vector3.zero;
		    //if (distanceToSteeringTarget < 5f)
		    //if (!traversingOffMeshLink && lastCorner)
		    //{
		    //    currentVelocity -= currentVelocity.ProjectOnPlane(m_physics.UpDirection) * Mathf.Clamp01(1f - distanceToSteeringTarget / 5f) * 5f *  deltaTime;
		    //}
	    }

	    private IEnumerator TraverseLink(RichSpecial linkPart)
	    {
	        var comp = linkPart.nodeLink.component as BaseNavLink;
	        
	        if (comp == null)
	        {
	            if (m_physics) m_physics.SetFootPosition(linkPart.second.position);
	        }
	        else
	        {
	            yield return comp.TraverseLink(m_physics, linkPart.first.position, linkPart.second.position);
	        }
	    }

	    public Vector3 MoveDirection;// => reachedEndOfPath || !hasPath ? Vector3.zero : desiredVelocity;
	    private Vector3 vel;
	    private void LateUpdate()
	    {
	        if (reachedEndOfPath || !hasPath) MoveDirection = Vector3.zero;
	        else
	        {
		        vel = desiredVelocity;
		        //MoveDirection = desiredVelocity;//simulatedPosition - m_physics.FootPosition;
		       // float mag = Mathf.Clamp01(desiredVelocity.magnitude / maxSpeed);
		        //MoveDirection = Vector3.ClampMagnitude(MoveDirection.ProjectOnPlane(m_physics.UpDirection) * mag, 1);
	        }
	    }
	    
	    #region Debug
	    
	    public string DebugName => "AI Navigation";
	    
	    public void OnDebugGUI()
	    {
	        GUILayout.Label($"Has Path: {hasPath}");
	        if (hasPath)
	        {
	            //GUILayout.Label($"Path Length: {m_activePath.vectorPath.Count}");
	            GUILayout.Label($"Remaining Distance: {remainingDistance}");
	            GUILayout.Label($"Has Reached End of Path: {reachedEndOfPath}");
	            
	            GUILayout.Label($"Off Mesh Link Traversing: {traversingOffMeshLink}");
	    
	            if (traversingOffMeshLink)
	            {
	                var link = richPath.GetCurrentPart() as RichSpecial;
	                if (link != null)
	                {
	                    var linkInfo = link.nodeLink.gameObject.name;
	                    GUILayout.Label($"Off Mesh Link: {linkInfo}");
	                }
	            }
	        }
	        else
	        {
	            GUILayout.Label("No active path.");
	        }
	    }
	    
	    public void OnDebugDraw()
	    {
	        Draw.ingame.WireSphere(destination, 0.25f, Color.crimson);
	        
	        if (hasPath)
	        {
	            var bufferPos = new List<Vector3>();
	            var partsBuffer = new List<PathPartWithLinkInfo>();
	            
	            using var thickness = Draw.ingame.WithLineWidth(2f);
	            Draw.ingame.WireSphere(steeringTarget, 0.25f, Color.green);
	            
	            richPath.GetRemainingPath(bufferPos, partsBuffer, m_physics.FootPosition, out var rep);
	            
	            for (int i = 0; i < bufferPos.Count - 1; i++)
	            {
	                Draw.ingame.Line(bufferPos[i], bufferPos[i + 1], Color.red);
	            }
	        }
	    }
	    
	    #endregion
    }
}