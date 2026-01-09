using System;
using System.Collections.Generic;
using FS.GameService;
using FS.GameService.Attributes;
using UnityEngine;
using PrimeTween;

namespace FS.CameraSystem
{
    public enum CameraShakeStrength
    {
        Light, Medium, Heavy
    }

    public enum CameraShakeDuration
    {
        Short, Medium, Long, Infinite
    }

    [Serializable]
    public class CameraShake
    {
        public static readonly Dictionary<CameraShakeStrength, float> k_shakeStrengthFactorMap = new()
        {
            {CameraShakeStrength.Light, 0.2f},
            {CameraShakeStrength.Medium, 0.5f},
            {CameraShakeStrength.Heavy, 1f}
        };
        public static readonly Dictionary<CameraShakeStrength, float> k_shakeFrequencyMap = new()
        {
            {CameraShakeStrength.Light, 10f},
            {CameraShakeStrength.Medium, 25f},
            {CameraShakeStrength.Heavy, 50f}
        };
        public static readonly Dictionary<CameraShakeDuration, float> k_shakeDurationFactorMap = new()
        {
            {CameraShakeDuration.Short, 0.5f},
            {CameraShakeDuration.Medium, 1.0f},
            {CameraShakeDuration.Long, 1.5f},
            {CameraShakeDuration.Infinite, float.MaxValue}
        };
        
        private readonly ShakeSettings m_shakeSettings;
        private Tween m_shakeTween;
        private Quaternion m_shakeRotationDelta;
        public bool IsActive => m_shakeTween.isAlive;

        public static CameraShake CreateShake(
            CameraShakeStrength strength = CameraShakeStrength.Medium, 
            CameraShakeDuration duration = CameraShakeDuration.Short, 
            CameraShakeStrength frequency = CameraShakeStrength.Medium,
            Ease? fallOffEase = Ease.Linear)
        {
            CameraShake shake = new CameraShake
            (
                new ShakeSettings
                {
                    strength = k_shakeStrengthFactorMap[strength] * Vector3.one,
                    duration = k_shakeDurationFactorMap[duration],
                    frequency = k_shakeFrequencyMap[frequency],
                    falloffEase = fallOffEase ?? Ease.Linear,
                    enableFalloff = fallOffEase.HasValue,
                    easeBetweenShakes = Ease.OutQuad
                }
            );
            
            return shake;
        }
        
        private CameraShake(ShakeSettings shakeSettings) => m_shakeSettings = shakeSettings;
        
        public void StartShake()
        {
            if (IsActive) StopShake();
            m_shakeTween = Tween.ShakeCustom(this, Vector3.zero, m_shakeSettings, UpdateTween);
        }
        
        public void StopShake()
        {
            if (!IsActive) return;
            m_shakeTween.Complete();
        }

        public void ApplyShake(CameraState m_cameraState, float strength = 1)
        {
            var shakeDelta = Quaternion.Slerp(Quaternion.identity, m_shakeRotationDelta, strength);
            m_cameraState.m_rotation *= shakeDelta;
        }

        private void UpdateTween(CameraShake shakeInstance, Vector3 shakeRot)
        {
            shakeInstance.m_shakeRotationDelta = Quaternion.Euler(shakeRot);
        }

        public override string ToString()
        {
            string durationStr = m_shakeTween.duration >= float.MaxValue ? "Infinite" : (m_shakeTween.elapsedTime/m_shakeTween.duration).ToString("0.00");
            return $"[{durationStr}] (Strength: {m_shakeSettings.strength}) | (Frequency: {m_shakeSettings.frequency})";
        }

        public override int GetHashCode()
        {
            return m_shakeSettings.duration.GetHashCode() ^ m_shakeSettings.strength.GetHashCode() ^ m_shakeSettings.frequency.GetHashCode()
                ^ m_shakeSettings.falloffEase.GetHashCode() ^ m_shakeSettings.easeBetweenShakes.GetHashCode();
        }
    }
}