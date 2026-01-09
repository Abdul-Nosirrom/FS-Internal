using System;
using UnityEngine;

namespace FS.CameraSystem
{
    // TODO: Significant issues with blending and the likes that can cause camera jitters
    [Serializable]
    public struct EulerAngles : IEquatable<EulerAngles>
    {
        [SerializeField] private Vector3 m_angles;

        public float pitch
        {
            get => m_angles.x;
            set => m_angles.x = WrapAngle(value);
        }
        
        public float yaw
        {
            get => m_angles.y;
            set => m_angles.y = WrapAngle(value);
        }
        
        public float roll
        {
            get => m_angles.z;
            set => m_angles.z = WrapAngle(value);
        }
        
        public EulerAngles(float pitch, float yaw, float roll)
        {
            m_angles = WrapAngles(new Vector3(pitch, yaw, roll));
        }
        
        // Lerp function
        public static EulerAngles Lerp(EulerAngles a, EulerAngles b, float t, bool shortestPath = true)
        {
            if (shortestPath) return Quaternion.Slerp(a.ToQuaternion(), b.ToQuaternion(), t);

            t = Mathf.Clamp01(t);
            EulerAngles deltaAngle = b - a;
            return a + deltaAngle * t;
        }
        
        // Conversion
        public Quaternion ToQuaternion()
        {
            //return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.right) * Quaternion.AngleAxis(roll, Vector3.forward);
            return Quaternion.Euler(m_angles);
        }
        
        // Implicit constructor for Vector 3 to Euler Angles
        public static implicit operator EulerAngles(Vector3 angles)
        {
            return new EulerAngles
            {
                m_angles = WrapAngles(angles)
            };
        }

        public static implicit operator EulerAngles(Quaternion q)
        {
            return new EulerAngles
            {
                m_angles = WrapAngles(q.eulerAngles)
            };
        }
        
        // Multiply with a float or divide operator
        public static EulerAngles operator *(EulerAngles a, float b)
        {
            return new EulerAngles
            {
                m_angles = WrapAngles(a.m_angles * b)
            };
        }
        
        public static EulerAngles operator *(float a, EulerAngles b)
        {
            return new EulerAngles
            {
                m_angles = WrapAngles(a * b.m_angles)
            };
        }
        
        public static EulerAngles operator /(EulerAngles a, float b)
        {
            return new EulerAngles
            {
                m_angles = WrapAngles(a.m_angles / b)
            };
        }
        
        public static EulerAngles operator /(float a, EulerAngles b)
        {
            return new EulerAngles
            {
                m_angles = WrapAngles(b.m_angles / a)
            };
        }
        
        // Addition & Subtraction operators
        public static EulerAngles operator +(EulerAngles a, EulerAngles b)
        {
            return new EulerAngles
            {
                m_angles = WrapAngles(a.m_angles + b.m_angles)
            };
        }
        
        public static EulerAngles operator -(EulerAngles a, EulerAngles b)
        {
            return new EulerAngles
            {
                m_angles = WrapAngles(a.m_angles - b.m_angles)
            };
        }

        public static Vector3 WrapAngles(Vector3 angles)
        {
            return new Vector3(WrapAngle(angles.x), WrapAngle(angles.y), WrapAngle(angles.z));
        }
        public static float WrapAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }

        public override bool Equals(object obj) => obj is EulerAngles other && Equals(other);

        public bool Equals(EulerAngles other) => m_angles.Equals(other.m_angles);

        public override int GetHashCode()
        {
            return m_angles.GetHashCode();
        }
        
        public override string ToString()
        {
            return
                $"{nameof(pitch)}: {pitch : 0}, {nameof(yaw)}: {yaw : 0}, {nameof(roll)}: {roll : 0}";
        }
    }
}