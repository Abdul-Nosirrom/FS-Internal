using System;
using FS.Animation;
using UnityEngine;

[Serializable]
public class ActionsAnimationSet : AnimationSet
{
    // Define properties for actions animations here
    public SequenceAnimation RailGrindBase;
    public OneShotAnimation RailSwap;
    public MecanimAnimationAsset LedgeClimb;
    public OneShotAnimation FingerGunBlastJump;
    public OneShotAnimation WhipArmHoverJump;
    public FSAnimation BoxingGloveMeteorDash;

    public MecanimAnimationAsset Skid;
    public OneShotAnimation FrontFlip;

    // Spring legs dash
    public SequenceAnimation SpringLegsDashLoop;
    public OneShotAnimation HomingAttackImpact;
    public OneShotAnimation HomingAttackEnemyBounce;
    public OneShotAnimation SpringKickWallEject;
}
    
public class PrototypeActionsAnimationController : AnimationController
{
    [SerializeField] private ActionsAnimationSet m_actionsSet;
    
    protected override AnimationSet[] InitializeAnimationSets() => new AnimationSet[] { m_actionsSet };

    private PhysicsController m_physics;
    
    protected override void Awake()
    {
        base.Awake();
        
        m_physics = GetComponentInParent<PhysicsController>();
    }

    protected override void AnimationUpdate()
    {
        // no-op
        // Cancel certain anims if not in the air, etc (ideally we'd do this in a different way but for prototyping this is fine)
        if (m_physics.State != PhysicsState.Air)
        {
            m_actionsSet.FrontFlip.Stop(Animator);
            m_actionsSet.HomingAttackImpact.Stop(Animator);
            m_actionsSet.HomingAttackEnemyBounce.Stop(Animator);
            m_actionsSet.SpringKickWallEject.Stop(Animator);
            m_actionsSet.SpringLegsDashLoop.Stop(Animator);
        }
    }
}