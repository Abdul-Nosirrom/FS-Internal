using System;
using FluffyUnderware.Curvy;
using FS.Math;
using FS.MeshProcessing;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using ISplineProvider = FS.MeshProcessing.ISplineProvider;

public class SplineFollower
{
    public ISplineProvider m_spline;
    public float m_speed;
    
    public bool IsValid => m_spline != null;
    
    public float Distance
    {
        get => m_distance;
        set
        {
            float length = m_spline.GetLength();
            m_distance = value;
            if (m_distance < 0f || m_distance > length)
            {
                if (m_spline.IsClosed())
                {
                    m_distance = Mathf.Repeat(m_distance, length);
                    OnLoopedAround?.Invoke();
                }
                else
                {
                    m_distance = Mathf.Clamp(m_distance, 0f, length);
                    OnReachedEnd?.Invoke();
                }
            }
        }
    }
    
    public Vector3 Position => m_pos;
    public Vector3 Direction => Vector3.Normalize(m_tangent * DirectionSign);
    public Vector3 Normal => m_normal;
    
    public float DirectionSign => Math.Sign(m_speed);
    public Quaternion Rotation => Quaternion.LookRotation(Direction, Normal);
    
    public Vector3 Velocity => Direction * Mathf.Abs(m_speed);

    protected float m_distance;
    protected Vector3 m_pos;
    protected Vector3 m_tangent;
    protected Vector3 m_normal;

    public Action OnReachedEnd;
    public Action OnLoopedAround;

    public void Init(ISplineProvider spline, PhysicsController physics, Vector3? queryPos = null, float minimumStartSpeed = 5f)
    {
        m_spline = spline;
        
        // Get the closest point on the spline, we need to know the tangent now before figuring out whether our direction is +- 1
        Distance = m_spline.GetNearestPoint(queryPos ?? physics.transform.position, out m_pos, out _);
        UpdateGrindData();
        
        // Figure out the appropriate "forward" direction on the spline based on our current velocity, or input, or forward
        int dir = GetGrindDirection(physics.Velocity.ProjectOnPlane(physics.UpDirection));
        if (dir == 0) dir = GetGrindDirection(physics.MoveInput());
        if (dir == 0) dir = GetGrindDirection(physics.transform.forward);
        if (dir == 0) dir = 1; // Just pick a direction if we have no other way to figure it out

        m_speed = physics.Velocity.magnitude * dir;
        if (Mathf.Abs(m_speed) < minimumStartSpeed) m_speed = minimumStartSpeed * dir;
    }
    
    public void Init(ISplineProvider spline, Vector3 queryPos, float startSpeed)
    {
        m_spline = spline;
        
        // Get the closest point on the spline, we need to know the tangent now before figuring out whether our direction is +- 1
        Distance = m_spline.GetNearestPoint(queryPos, out m_pos, out _);
        UpdateGrindData();
        
        m_speed = startSpeed;
    }

    public void UpdateFollower()
    {
        Distance += m_speed * Time.deltaTime;
        UpdateGrindData();
    }
    
    protected void UpdateGrindData()
    {
        m_spline.Evaluate(m_spline.GetSpline().DistanceToTF(Distance), out m_pos, out m_tangent, out m_normal);
        m_tangent = Vector3.Normalize(m_tangent);
    }
    
    protected int GetGrindDirection(Vector3 velocity) =>
        Math.Sign(Vector3.Dot(m_tangent, velocity));
}