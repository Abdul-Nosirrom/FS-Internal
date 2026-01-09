using System.Collections;
using FS.CombatSystem;
using FS.Player;
using FS.UI;
using FS.Utility;
using Sirenix.OdinInspector;
using UnityEngine;

// NOTE: this component can prolly be on the lock on reticle itself it controlling itself.
// Right now we put it on the player gameobject
public class LockOnReticleController : MonoBehaviour
{
    [TabGroup("Lock On Reticle")]
    [SerializeField] private HUDElement m_lockOnReticlePrefab;
    
    [TabGroup("Interaction Note")]
    [SerializeField] private HUDElement m_acidDropInteractionNotePrefab;
    [TabGroup("Interaction Note")]
    [SerializeField] private Vector3 m_acidDropNoteOffset = new Vector3(0f, 2f, 0f);
    [SerializeField, Range(1, 15)] private int m_acidDropQueryFrequency = 10;

    private HUDPanel m_hud;
    private HUDElement m_lockOnReticle;
    private HUDElement m_acidDropInteractionNote;
    private LockOnController m_lockOnController;
    private SpringKick m_springKick;
    private AcidDropAction m_acidDrop;
    
    private IEnumerator Start()
    {
        yield return Yields.WaitForNextFrame;
        
        m_hud = PlayerManager.GetSystem<PlayerUISystem>(gameObject).HUD;
        m_lockOnController = GetComponentInChildren<LockOnController>();
        
        m_springKick = GetComponentInChildren<SpringKick>();
        m_acidDrop = GetComponentInChildren<AcidDropAction>();
        
        if (m_hud == null)
        {
            Debug.LogError("[UI] LockOnReticleController could not find HUDPanel in PlayerUISystem.", this);
            enabled = false;
            yield break;
        }
        
        m_lockOnReticle = m_hud.Add(m_lockOnReticlePrefab, transform);
        _ = m_lockOnReticle.Hide(true);
        
        m_acidDropInteractionNote = m_hud.Add(m_acidDropInteractionNotePrefab, transform, m_acidDropNoteOffset);
        _ = m_acidDropInteractionNote.Hide(true);
        m_acidDrop = GetComponentInChildren<AcidDropAction>();
    }

    //private void OnEnable() => InvokeRepeating(nameof(InFrequentUpdate), 0f, 0.1f);
    //private void OnDisable() => CancelInvoke();

    private void LateUpdate()
    {
        TryUpdateAcidDropNote();
        TryUpdateLockOnReticle();
    }
    
    private void TryUpdateAcidDropNote()
    {
        if (m_acidDrop == null || m_acidDropInteractionNote == null) return;

        // Update only every 5/10 frames because acid drop check is exhaustive
        if (Time.frameCount % m_acidDropQueryFrequency != 0) return;
        
        // Update offset
        m_acidDropInteractionNote.TrackWorldTarget(transform, m_acidDropNoteOffset);
        
        // Early out with giant box cast (not rn but later)
        bool canPerform = m_acidDrop.CanStartAction();

        _ = canPerform ? m_acidDropInteractionNote.Show() : m_acidDropInteractionNote.Hide();
    }
    
    private void TryUpdateLockOnReticle()
    {
        if (m_lockOnReticle == null) return;
        
        bool targetFound = false;
        bool targetChanged = false;
        Transform target = null;
        if (m_lockOnController)
        {
            target = m_lockOnController.CurrentLockOnTarget.transform;
            targetFound = target != null;
            targetChanged = target != m_lockOnReticle.WorldTarget;
        }

        if (m_springKick && m_springKick.CanStartAction() && !targetFound)
        {
            targetFound = m_springKick.m_homingAttackTargetingSettings.PeformTargeting(gameObject, out var result);
            targetChanged = targetFound && result.Target.transform != m_lockOnReticle.WorldTarget;
            if (targetFound) target = result.Target.transform;
        }

        if (targetChanged || !targetFound) _=m_lockOnReticle.Hide();
        if (targetFound)
        {
            m_lockOnReticle.TrackWorldTarget(target);
            _=m_lockOnReticle.Show();
        }
    }
}