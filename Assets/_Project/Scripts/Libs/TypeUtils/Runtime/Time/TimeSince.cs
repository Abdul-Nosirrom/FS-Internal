using System;
using UnityEngine;

namespace TimeUtils
{
    /// <summary>
    /// A convenience struct to easily measure time since an event last happened, based on <see cref="P:Unity.Time.time" />.<br />
    /// <br />
    /// Typical usage would see you assigning 0 to a variable of this type to reset the timer.
    /// Then the struct would return time since the last reset. i.e.:
    /// <code>
    /// TimeSince lastUsed = 0;
    /// if ( lastUsed &gt; 10 ) { /*Do something*/ }
    /// </code>
    /// </summary>
    public struct TimeSince : IEquatable<TimeSince>
    {
        private float time;

        public static implicit operator float(TimeSince ts) => Time.time - ts.time;

        public static implicit operator TimeSince(float ts)
        {
            return new TimeSince() { time = Time.time - ts };
        }

        public static bool operator <(in TimeSince ts, float f) => (double) ts.Relative < (double) f;

        public static bool operator >(in TimeSince ts, float f) => (double) ts.Relative > (double) f;

        public static bool operator <=(in TimeSince ts, float f) => (double) ts.Relative <= (double) f;

        public static bool operator >=(in TimeSince ts, float f) => (double) ts.Relative >= (double) f;

        public static bool operator <(in TimeSince ts, int f) => (double) ts.Relative < (double) f;

        public static bool operator >(in TimeSince ts, int f) => (double) ts.Relative > (double) f;

        public static bool operator <=(in TimeSince ts, int f) => (double) ts.Relative <= (double) f;

        public static bool operator >=(in TimeSince ts, int f) => (double) ts.Relative >= (double) f;

        /// <summary>
        /// Time at which the timer reset happened, based on <see cref="P:Unity.Time.time" />.
        /// </summary>
        public float Absolute => this.time;

        /// <summary>Time passed since last reset, in seconds.</summary>
        public float Relative => (float) this;

        public override string ToString()
        {
            return Relative.ToString("0.00");
        }

        public static bool operator ==(TimeSince left, TimeSince right) => left.Equals(right);

        public static bool operator !=(TimeSince left, TimeSince right) => !(left == right);

        public override bool Equals(object obj) => obj is TimeSince o && this.Equals(o);

        public readonly bool Equals(TimeSince o) => (double) this.time == (double) o.time;

        public override readonly int GetHashCode() => this.time.GetHashCode();
    }
}