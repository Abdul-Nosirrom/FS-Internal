using System;
using System.Collections.Generic;
using UnityEngine;
using PrimeTween;

// TODO: Tweak strength/frequency/duration maps, hard to gauge good params rn so not very good
namespace FS.CameraSystem
{
    public enum CameraImpulseStrength
    {
        Light, Medium, Heavy
    }

    public enum CameraImpulseDuration
    {
        Short, Medium, Long
    }

    [Serializable]
    public class CameraImpulse
    {
        public static readonly Dictionary<CameraImpulseStrength, float> k_shakeStrengthFactorMap = new()
        {
            {CameraImpulseStrength.Light, 1.5f},
            {CameraImpulseStrength.Medium, 3.5f},
            {CameraImpulseStrength.Heavy, 4.5f}
        };
        public static readonly Dictionary<CameraImpulseStrength, float> k_shakeFrequencyMap = new()
        {
            {CameraImpulseStrength.Light, 15f},
            {CameraImpulseStrength.Medium, 25f},
            {CameraImpulseStrength.Heavy, 50f}
        };
        public static readonly Dictionary<CameraImpulseDuration, float> k_shakeDurationFactorMap = new()
        {
            {CameraImpulseDuration.Short, 0.25f},
            {CameraImpulseDuration.Medium, 0.5f},
            {CameraImpulseDuration.Long, 0.75f},
        };
        
        private readonly ShakeSettings m_impulseSettings;
        private Tween m_impulseTween;
        private Vector3 m_impulsePositionDelta;

        public bool IsActive => m_impulseTween.isAlive;

        public static CameraImpulse Create(
            Vector3 impulseDirection,
            CameraImpulseStrength strength = CameraImpulseStrength.Medium, 
            CameraImpulseDuration duration = CameraImpulseDuration.Short, 
            CameraImpulseStrength frequency = CameraImpulseStrength.Medium,
            Ease fallOffEase = Ease.OutSine)
        {
            CameraImpulse shake = new CameraImpulse
            (
                new ShakeSettings
                {
                    strength = k_shakeStrengthFactorMap[strength] * impulseDirection,
                    duration = k_shakeDurationFactorMap[duration],
                    frequency = k_shakeFrequencyMap[frequency],
                    falloffEase = fallOffEase == Ease.Custom ? Ease.Linear : fallOffEase,
                    enableFalloff = fallOffEase != Ease.Custom,
                    easeBetweenShakes = Ease.OutQuad
                }
            );
            
            return shake;
        }
        
        private CameraImpulse(ShakeSettings impulseSettings) => m_impulseSettings = impulseSettings;
        
        public void StartImpulse()
        {
            if (IsActive) StopImpulse();
            m_impulseTween = Tween.PunchCustom(this, Vector3.zero, m_impulseSettings, UpdateTween);
        }
        
        public void StopImpulse()
        {
            if (!IsActive) return;
            m_impulseTween.Complete();
        }

        public void ApplyImpulse(CameraState m_cameraState, float strength = 1)
        {
            var impulseDelta = Vector3.Lerp(Vector3.zero, m_impulsePositionDelta, strength);
            m_cameraState.m_position += impulseDelta;
        }

        private void UpdateTween(CameraImpulse shakeInstance, Vector3 impulseDelta)
        {
            shakeInstance.m_impulsePositionDelta = impulseDelta;
        }

        public override string ToString()
        {
            string durationStr = m_impulseTween.duration >= float.MaxValue ? "Infinite" : (m_impulseTween.elapsedTime/m_impulseTween.duration).ToString("0.00");
            return $"[{durationStr}] (Strength: {m_impulseSettings.strength}) | (Frequency: {m_impulseSettings.frequency})";
        }

        public override int GetHashCode()
        {
            return m_impulseSettings.duration.GetHashCode() ^ m_impulseSettings.strength.GetHashCode() ^ m_impulseSettings.frequency.GetHashCode()
                ^ m_impulseSettings.falloffEase.GetHashCode() ^ m_impulseSettings.easeBetweenShakes.GetHashCode();
        }
    }
}