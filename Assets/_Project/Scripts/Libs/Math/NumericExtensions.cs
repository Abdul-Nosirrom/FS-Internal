using UnityEngine;

namespace FS.Math
{
    public static class NumericExtensions
    {
        #region Decay Methods
        private static int k_decayFrameRate = 60;
        
        public static float DecayHalfLife(this float value, float halfLifeInSeconds) => value * Mathf.Pow(0.5f, Time.deltaTime / halfLifeInSeconds);
        public static int DecayHalfLife(this int value, float halfLifeInSeconds) => Mathf.RoundToInt(value * Mathf.Pow(0.5f, Time.deltaTime / halfLifeInSeconds));
        public static Vector2 DecayHalfLife(this Vector2 value, float halfLifeInSeconds) => value * Mathf.Pow(0.5f, Time.deltaTime / halfLifeInSeconds);
        public static Vector3 DecayHalfLife(this Vector3 value, float halfLifeInSeconds) => value * Mathf.Pow(0.5f, Time.deltaTime / halfLifeInSeconds);
        
        public static float Decay(this float value, float retentionPercentPerFrame) => value * Mathf.Pow(retentionPercentPerFrame, Time.deltaTime * k_decayFrameRate);
        public static int Decay(this int value, float retentionPercentPerFrame) => Mathf.RoundToInt(value * Mathf.Pow(retentionPercentPerFrame, Time.deltaTime * k_decayFrameRate));
        public static Vector2 Decay(this Vector2 value, float retentionPercentPerFrame) => value * Mathf.Pow(retentionPercentPerFrame, Time.deltaTime * k_decayFrameRate);
        public static Vector3 Decay(this Vector3 value, float retentionPercentPerFrame) => value * Mathf.Pow(retentionPercentPerFrame, Time.deltaTime * k_decayFrameRate);
        #endregion
    }
}