using System;
using FS.GameplayActions;
using UnityEngine;

public partial class PhysicsController
{
    public SkatingActionSet m_skatingActionSet => m_actionController?.GetActionSet<SkatingActionSet>();
    public VertAction m_vert => m_skatingActionSet?.Vert;
    public AcidDropAction m_acidDrop => m_skatingActionSet?.AcidDrop;
    public AcidDropAction m_spineTransfer => m_skatingActionSet?.SpineTransfer;
    public RailGrindAction m_railGrind => m_skatingActionSet?.RailGrind;
    public WallSlide m_wallSlide => m_actionController?.GetActionSet<PrototypeActionSet>()?.WallSlide;

    public bool IsInSkateAction => (m_vert?.IsActive??false) || (m_acidDrop?.IsActive??false) || (m_spineTransfer?.IsActive??false);
    public float TimeSinceSkateActionEnded => Mathf.Min(m_vert?.m_timeSinceEnded??float.MaxValue, m_acidDrop?.m_timeSinceEnded??float.MaxValue, m_spineTransfer?.m_timeSinceEnded??float.MaxValue);

    public bool IsRailGrinding => m_railGrind is { IsActive: true };

    private class NoWallSlideInSkateActionConstraint : ActionConstraintBase
    {
        public override string Name => "Prevent WallSlide During Skate Action";
        private PhysicsController m_physics;
        
        public NoWallSlideInSkateActionConstraint(PhysicsController physics) => m_physics = physics;

        public override bool EvaluateConstraint(GameplayAction action) =>
            !((action is WallSlide) && m_physics.IsInSkateAction);
    }

    private NoWallSlideInSkateActionConstraint m_wallSlideEntryConstraint;
    
    private void FetchActions()
    {
        //var actionSet = m_actionController?.GetActionSet<SkatingActionSet>();
        //if (actionSet == null) return;
        //
        //m_vert = actionSet.Vert;
        //m_acidDrop = actionSet.AcidDrop;
        //m_spineTransfer = actionSet.SpineTransfer;
        //m_railGrind = actionSet.RailGrind;
        //m_wallSlide = m_actionController.GetActionSet<TestingActionSet>().WallSlide;
        
        if (m_vert) m_vert.OnActionEnded += OnSkateActionEnded;
        if (m_acidDrop) m_acidDrop.OnActionEnded += OnSkateActionEnded;

        m_wallSlideEntryConstraint = new NoWallSlideInSkateActionConstraint(this);
        m_actionController.ConstraintHandler.AddConstraint(m_wallSlideEntryConstraint, true);
    }

    private void OnSkateActionEnded(GameplayAction obj)
    {
        m_finalVertSpeed = Mathf.Max(m_finalVertSpeed, Speed);
    }
}