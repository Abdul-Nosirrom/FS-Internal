using FS.Math;
using UnityEngine;

[RequireComponent(typeof(GroundEventBroadcaster))]
public class BounceFloor : MonoBehaviour
{
    [Range(1, 10)] private float m_bounceHeight = 5f;
    private Material m_bounceMaterial;
    
    private void OnEnable()
    {
        GetComponent<GroundEventBroadcaster>().OnBecomeGroundForGameObject += BouncePhysics;
        m_bounceMaterial = GetComponent<Renderer>()?.material;
    }
    private void OnDisable()
    {
        GetComponent<GroundEventBroadcaster>().OnBecomeGroundForGameObject -= BouncePhysics;
    }

    private async void BouncePhysics(PhysicsController physics)
    {
        m_bounceMaterial?.SetVector("_BouncePoint", physics.Position);
        m_bounceMaterial?.SetFloat("_BounceTime", Time.time);

        var landingSpeed = -physics.VerticalSpeed;
        await Awaitable.NextFrameAsync();

        physics.UnGround();
        
        await Awaitable.NextFrameAsync();

        var bounceVelocity = Mathf.Sqrt(2f * m_bounceHeight * Mathf.Abs(physics.VerticalPhysicsParams.m_upGravity));
        physics.VerticalSpeed = Mathf.Max(landingSpeed, Mathf.Max(0, physics.VerticalSpeed) + bounceVelocity);
        Debug.LogError($"Vertical Speed: Landing = {landingSpeed} -> Bounced = {physics.VerticalSpeed}");
        //float verticalSpeed = Mathf.Max(0, physics.Velocity.Dot(-physics.GravityDir)) + bounceVelocity;
        //physics.VerticalVelocity = -physics.GravityDir * verticalSpeed;
    }
}