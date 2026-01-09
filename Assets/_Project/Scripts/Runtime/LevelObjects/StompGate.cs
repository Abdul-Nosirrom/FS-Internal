
using FS.Attributes;
using FS.GameplayActions;
using PrimeTween;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;

[Experimental]
[LevelDesignCategory("Level Objects/Gates")]
public class StompGate : LevelObjectBase
{
    public UltEvent OnGateOpened;

    public float m_maxHeight = 0f;
    public float m_minHeight = -2f;
    [Required] public Transform m_gateTransform;

    private float m_currentPercent = 0;
    private Tween m_sinkTween;
    
    private const int k_stompCountToOpen = 3;
    private int m_currentStompCount = 0;
    
    
    protected override void OnPhysicsActorEnter(Context context)
    {
        if (m_sinkTween.isAlive) return; // Mid-sink animation, ignore need buffer time
        if (m_currentStompCount >= k_stompCountToOpen) return; // Already done
        if (context.actionController == null) return;
        
        // Is stomp active?
        if (!context.actionController.HasActiveAction<SpringStompBounce>()) return;
        
        // Cancel stomp
        context.actionController.ActiveActions[ActionChannel.PhysicsVertical].TryEndAction();
        
        // Force grounding
        //context.physics.TrySnapToGround();
        
        // Trigger gate sink & maybe finish
        IncrementGateSink();
    }

    private void IncrementGateSink()
    {
        m_currentStompCount++;
        m_currentPercent = Mathf.Clamp01((float)m_currentStompCount / k_stompCountToOpen);
        float nextHeight = Mathf.Lerp(m_maxHeight, m_minHeight, m_currentPercent);
        
        // Tween gate down
        float currentY = m_gateTransform.localPosition.y;
        m_sinkTween = Tween.LocalPositionY(m_gateTransform, currentY, nextHeight, 0.3f, Ease.OutQuad);
        
        if (m_currentStompCount >= k_stompCountToOpen)
            m_sinkTween.OnComplete(() => OnGateOpened?.Invoke());
    }
    
#if UNITY_EDITOR
    public override void DrawGizmos()
    {
        using var thickness = Drawing.Draw.WithLineWidth(2);
        
        var fromPos = m_gateTransform == null ? transform.position : m_gateTransform.position;
        
        // Find all events we'll be calling
        foreach (var evt in OnGateOpened.PersistentCallsList)
        {
            if (evt.Target is Component comp) Drawing.Draw.Line(fromPos, comp.transform.position, Color.darkOrange);
            if (evt.Target is GameObject go) Drawing.Draw.Line(fromPos, go.transform.position, Color.darkOrange);
        }
    }
#endif    
}