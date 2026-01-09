using System;
using UnityEngine;

namespace TimeUtils
{
    /// <summary>
    /// A convenience struct to easily measure time since an event last happened, based on <see cref="P:Sandbox.Time.realtimeSinceStartup" />.<br />
    /// <br />
    /// Typical usage would see you assigning 0 to a variable of this type to reset the timer.
    /// Then the struct would return time since the last reset. i.e.:
    /// <code>
    /// RealTimeSince lastUsed = 0;
    /// if ( lastUsed &gt; 10 ) { /*Do something*/ }
    /// </code>
    /// </summary>
    public struct RealTimeSince : IEquatable<RealTimeSince>
    {
        private double time;

        public static implicit operator float(RealTimeSince ts)
        {
            return (float)(Time.realtimeSinceStartup - ts.time);
        }

        public static implicit operator RealTimeSince(float ts)
        {
            return new RealTimeSince()
            {
                time = Time.realtimeSinceStartup - (double)ts
            };
        }

        public static bool operator <(in RealTimeSince ts, float f)
        {
            return (double)ts.Relative < (double)f;
        }

        public static bool operator >(in RealTimeSince ts, float f)
        {
            return (double)ts.Relative > (double)f;
        }

        public static bool operator <=(in RealTimeSince ts, float f)
        {
            return (double)ts.Relative <= (double)f;
        }

        public static bool operator >=(in RealTimeSince ts, float f)
        {
            return (double)ts.Relative >= (double)f;
        }

        public static bool operator <(in RealTimeSince ts, int f)
        {
            return (double)ts.Relative < (double)f;
        }

        public static bool operator >(in RealTimeSince ts, int f)
        {
            return (double)ts.Relative > (double)f;
        }

        public static bool operator <=(in RealTimeSince ts, int f)
        {
            return (double)ts.Relative <= (double)f;
        }

        public static bool operator >=(in RealTimeSince ts, int f)
        {
            return (double)ts.Relative >= (double)f;
        }

        /// <summary>
        /// Time at which the timer reset happened, based on <see cref="P:Sandbox.Time.realtimeSinceStartup" />.
        /// </summary>
        public double Absolute => time;

        /// <summary>Time passed since last reset, in seconds.</summary>
        public float Relative => (float)this;

        public override string ToString()
        {
            return Relative.ToString();
        }

        public static bool operator ==(RealTimeSince left, RealTimeSince right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RealTimeSince left, RealTimeSince right)
        {
            return !(left == right);
        }

        public override bool Equals(object obj)
        {
            return obj is RealTimeSince o && Equals(o);
        }

        public bool Equals(RealTimeSince o)
        {
            return time == o.time;
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine<double>(time);
        }
    }
}