using System;
using UnityEngine;

namespace TimeUtils
{
    /// <summary>
    /// A convenience struct to easily manage a time countdown, based on <see cref="P:Unity.Time.realtimeSinceStartup" />.<br />
    /// <br />
    /// Typical usage would see you assigning to a variable of this type a necessary amount of seconds.
    /// Then the struct would return the time countdown, or can be used as a bool i.e.:
    /// <code>
    /// RealTimeUntil nextAttack = 10;
    /// if ( nextAttack ) { /*Do something*/ }
    /// </code>
    /// </summary>
    public struct RealTimeUntil : IEquatable<RealTimeUntil>
    {
        private double time;
        private double startTime;

        public static implicit operator bool(RealTimeUntil ts) => Time.realtimeSinceStartup >= ts.time;

        public static implicit operator float(RealTimeUntil ts)
        {
            return (float) (ts.time - Time.realtimeSinceStartup);
        }

        public static implicit operator RealTimeUntil(float ts)
        {
            return new RealTimeUntil()
            {
                time = Time.realtimeSinceStartup + (double) ts,
                startTime = Time.realtimeSinceStartup
            };
        }

        public static bool operator <(in RealTimeUntil ts, float f) => ts.Relative < (double) f;

        public static bool operator >(in RealTimeUntil ts, float f) => ts.Relative > (double) f;

        public static bool operator <=(in RealTimeUntil ts, float f) => ts.Relative <= (double) f;

        public static bool operator >=(in RealTimeUntil ts, float f) => ts.Relative >= (double) f;

        public static bool operator <(in RealTimeUntil ts, int f) => ts.Relative < (double) f;

        public static bool operator >(in RealTimeUntil ts, int f) => ts.Relative > (double) f;

        public static bool operator <=(in RealTimeUntil ts, int f) => ts.Relative <= (double) f;

        public static bool operator >=(in RealTimeUntil ts, int f) => ts.Relative >= (double) f;

        /// <summary>
        /// Time to which we are counting down to, based on <see cref="P:Unity.Time.realtimeSinceStartup" />.
        /// </summary>
        public double Absolute => this.time;

        /// <summary>The actual countdown, in seconds.</summary>
        public double Relative => (double) (float) this;

        /// <summary>Amount of seconds passed since the countdown started.</summary>
        public double Passed => Time.realtimeSinceStartup - this.startTime;

        /// <summary>
        /// The countdown, but as a fraction, i.e. a value from 0 (start of countdown) to 1 (end of countdown)
        /// </summary>
        public double Fraction
        {
            get
            {
                return Math.Clamp((Time.realtimeSinceStartup - this.startTime) / (this.time - this.startTime), 0.0, 1.0);
            }
        }

        public override string ToString()
        {
            return Relative.ToString();
        }

        public static bool operator ==(RealTimeUntil left, RealTimeUntil right) => left.Equals(right);

        public static bool operator !=(RealTimeUntil left, RealTimeUntil right) => !(left == right);

        public override readonly bool Equals(object obj) => obj is RealTimeUntil o && this.Equals(o);

        public readonly bool Equals(RealTimeUntil o) => this.time == o.time;

        public override readonly int GetHashCode() => HashCode.Combine<double>(this.time);
    }
}