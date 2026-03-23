using System;
using System.Collections.Generic;
using FMOD.Studio;
using FS.Audio;
using FS.GameplayActions;
using FS.Rendering;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlayerAudioEffects : MonoBehaviour
{
    public AudioElement LandingSFX;
    public AudioElement EffortVocalization;
    public AudioElement JumpSFX;
    public AudioElement RailGrindSFX;

    [SerializeField] private VFXController m_railGrindVFX_LeftFoot;
    [SerializeField] private VFXController m_railGrindVFX_RightFoot;

    private AudioHandle m_railGrindSFXHandle;
    
    private void Awake()
    {
        
        var ac = GetComponentInParent<ActionController>();
        if (ac != null)
        {
            BindActionEvents(ac);
        }

        var physics = GetComponentInParent<PhysicsController>();
        if (physics != null)
        {
            BindPhysicsEvents(physics);
        }
    }

    private void BindActionEvents(ActionController ac)
    {
        var locomotionSet = ac.GetActionSet<LocomotionActionSet>();
        if (locomotionSet != null)
        {
            locomotionSet.Jump.OnActionStarted += OnJumped;
        }
        
        var skatingSet = ac.GetActionSet<SkatingActionSet>();
        if (skatingSet != null)
        {
            skatingSet.RailGrind.OnActionStarted += OnRailGrindStart;
            skatingSet.RailGrind.OnActionEnded += OnRailGrindEnded;
            skatingSet.RailGrind.OnRailStateChanged += OnRailGrindStateChange;
            
            m_railGrind = skatingSet.RailGrind;

            // m_railGrindSFXHandle = AudioManager.CreateAudioInstance(RailGrindSFX, false);
            // AudioManager.AttachAudioInstance(m_railGrindSFXHandle, gameObject);
        }
    }

    private RailGrindAction m_railGrind;
    private AudioParameter m_railSpeedParam = new AudioParameter("PlayerNormalizedSpeed", 0f);
    private void LateUpdate()
    {
        if (m_railGrindSFXHandle is {IsPlaying: true})
        {
            if (!m_railGrind.IsActive)
                m_railGrindSFXHandle.Stop(STOP_MODE.ALLOWFADEOUT); // BUG: audio can not stop on rail exit with the events below so this is a quick slap-hack
            else
            {
                var railSpeed = Mathf.Abs(m_railGrind.Speed);
                m_railSpeedParam.Value = Mathf.InverseLerp(12f, 35f, railSpeed);
                m_railGrindSFXHandle.SetParameter(m_railSpeedParam);
            }
        }

        if (m_railGrindVFX_LeftFoot.IsActive || m_railGrindVFX_RightFoot.IsActive)
        {
            if (!m_railGrind.IsActive) StopRailGrindVFX();
            else
            {
                // Update rail vfx rotation
                var rot = Quaternion.LookRotation(-transform.forward, transform.up);
                m_railGrindVFX_LeftFoot.transform.rotation = rot;
                m_railGrindVFX_RightFoot.transform.rotation = rot;
            }
        }
    }

    private void BindPhysicsEvents(PhysicsController physics)
    {
        physics.OnLandedEvt += OnLanded;
    }

    private void OnLanded()
    {
        AudioManager.PlayAudio_Attached(LandingSFX, gameObject);
    }

    private void OnJumped(GameplayAction obj)
    {
        AudioManager.PlayAudio_Attached(JumpSFX, gameObject);
    }
    
    private void OnRailGrindStart(GameplayAction obj)
    {
        AudioManager.PlayAudio_Attached(EffortVocalization, gameObject);
        // m_railGrindSFXHandle = AudioManager.CreateAudioInstance(RailGrindSFX, true);
        // AudioManager.AttachAudioInstance(m_railGrindSFXHandle, gameObject);
        //if (m_railGrindSFXHandle.IsStopped) m_railGrindSFXHandle.Play();
    }
    
    private void OnRailGrindEnded(GameplayAction obj)
    {
        StopRailGrindVFX();
        if (m_railGrindSFXHandle is {IsPlaying: true}) m_railGrindSFXHandle.Stop(STOP_MODE.ALLOWFADEOUT);
    }
    
    private void OnRailGrindStateChange(RailGrindAction.GrindState newState, RailGrindAction.GrindState prevState)
    {
        if (newState == RailGrindAction.GrindState.Default)// && prevState != RailGrindAction.GrindState.Trick)
        {
            if (m_railGrindSFXHandle is { IsPlaying: true }) m_railGrindSFXHandle.Stop();
            m_railGrindSFXHandle = AudioManager.CreateAudioInstance(RailGrindSFX, true);
            AudioManager.AttachAudioInstance(m_railGrindSFXHandle, gameObject);
            PlayRailGrindVFX();
        }
        else if (m_railGrindSFXHandle is { IsValid: true })
        {
            m_railGrindSFXHandle.Stop(STOP_MODE.ALLOWFADEOUT);
            StopRailGrindVFX();
        }
    }

    private void PlayRailGrindVFX()
    {
        m_railGrindVFX_LeftFoot.Play();
        m_railGrindVFX_RightFoot.Play();
    }

    private void StopRailGrindVFX()
    {
        m_railGrindVFX_LeftFoot.Stop();
        m_railGrindVFX_RightFoot.Stop();
    }
}