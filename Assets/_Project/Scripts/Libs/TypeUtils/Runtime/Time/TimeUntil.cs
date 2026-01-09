using System;
using UnityEngine;

namespace TimeUtils
{
    /// <summary>
    /// A convenience struct to easily manage a time countdown, based on <see cref="P:Unity.Time.time" />.<br />
    /// <br />
    /// Typical usage would see you assigning to a variable of this type a necessary amount of seconds.
    /// Then the struct would return the time countdown, or can be used as a bool i.e.:
    /// <code>
    /// TimeUntil nextAttack = 10;
    /// if ( nextAttack ) { /*Do something*/ }
    /// </code>
    /// </summary>
    public struct TimeUntil : IEquatable<TimeUntil>
    { 
        private float time;
        private float startTime;

        public static implicit operator bool(TimeUntil ts) => (double) Time.time >= (double) ts.time;

        public static implicit operator float(TimeUntil ts) => ts.time - Time.time;

        public static bool operator <(in TimeUntil ts, float f) => (double) ts.Relative < (double) f;

        public static bool operator >(in TimeUntil ts, float f) => (double) ts.Relative > (double) f;

        public static bool operator <=(in TimeUntil ts, float f) => (double) ts.Relative <= (double) f;

        public static bool operator >=(in TimeUntil ts, float f) => (double) ts.Relative >= (double) f;

        public static bool operator <(in TimeUntil ts, int f) => (double) ts.Relative < (double) f;

        public static bool operator >(in TimeUntil ts, int f) => (double) ts.Relative > (double) f;

        public static bool operator <=(in TimeUntil ts, int f) => (double) ts.Relative <= (double) f;

        public static bool operator >=(in TimeUntil ts, int f) => (double) ts.Relative >= (double) f;

        public static implicit operator TimeUntil(float ts)
        {
            return new TimeUntil()
            {
                time = Time.time + ts,
                startTime = Time.time
            };
        }

        /// <summary>
        /// Time to which we are counting down to, based on <see cref="P:Unity.Time.time" />.
        /// </summary>
        public float Absolute => this.time;

        /// <summary>The actual countdown, in seconds.</summary>
        public float Relative => (float) this;

        /// <summary>Amount of seconds passed since the countdown started.</summary>
        public float Passed => Time.time - this.startTime;

        /// <summary>
        /// The countdown, but as a fraction, i.e. a value from 0 (start of countdown) to 1 (end of countdown)
        /// </summary>
        public float Fraction
        {
            get
            {
                return Math.Clamp((float) (((double) Time.time - (double) this.startTime) / ((double) this.time - (double) this.startTime)), 0.0f, 1f);
            }
        }

        public override string ToString()
        {
            return Relative.ToString("0.00");
        }

        public static bool operator ==(TimeUntil left, TimeUntil right) => left.Equals(right);

        public static bool operator !=(TimeUntil left, TimeUntil right) => !(left == right);

        public override readonly bool Equals(object obj) => obj is TimeUntil o && this.Equals(o);
        
        public readonly bool Equals(TimeUntil o) => (double) this.time == (double) o.time;

        public override readonly int GetHashCode() => HashCode.Combine<float>(this.time);
    }
}