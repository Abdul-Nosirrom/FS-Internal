using Sirenix.OdinInspector;
using UnityEngine;

namespace FS.CameraSystem
{
    public struct CameraOrbitVector
    {
        public Vector3 Pivot;
        [Min(0)]
        public float Distance;
        [Range(0, 140)]
        public float FOV;
        
        // TODO: I'm not a fan of pitch limits existing on the orbit vector and having each behavior setup its own limits. But like, Vert Cam needs different pitch limits?
        [MinMaxSlider(-90, 90)]
        public Vector2 PitchLimits;

        public EulerAngles Rotation
        {
            get => m_rotation;
            set => m_rotation = ApplyPitchLimits(value);
        }
        private EulerAngles m_rotation;
        
        // Some helpers to directly get & set rotation
        public float Yaw
        {
            get => Rotation.yaw;
            set => Rotation = new EulerAngles(Rotation.pitch, value, Rotation.roll);
        }
        public float Pitch
        {
            get => Rotation.pitch;
            set => Rotation = new EulerAngles(value, Rotation.yaw, Rotation.roll);
        }
        public float Roll
        {
            get => Rotation.roll;
            set => Rotation = new EulerAngles(Rotation.pitch, Rotation.yaw, value);
        }
        
        public Vector2 ViewOffset;
        public EulerAngles RotationOffset;

        public static CameraOrbitVector Blend(CameraOrbitVector a, CameraOrbitVector b, float t)
        {
            return new CameraOrbitVector()
            {
                // TODO: Rn this is only used in one place during behavior execution, where (a) is the base cameraVector and (b) is the behavior's cameraVector, so we just use the base cameraVector's limits that we set early
                PitchLimits = a.PitchLimits, 
                //PitchLimits = Vector2.Lerp(a.PitchLimits, b.PitchLimits, t), // NOTE: Does this make sense? We dont do this here instead we blend it earlier before behaviors are executed (so result is properly propagated)
                Pivot = Vector3.Lerp(a.Pivot, b.Pivot, t),
                Distance = Mathf.Lerp(a.Distance, b.Distance, t),
                FOV = Mathf.Lerp(a.FOV, b.FOV, t),
                Rotation = EulerAngles.Lerp(a.Rotation, b.Rotation, t),
                ViewOffset = Vector2.Lerp(a.ViewOffset, b.ViewOffset, t),
                RotationOffset = EulerAngles.Lerp(a.RotationOffset, b.RotationOffset, t)
            };
        }

        public Quaternion ToRotation(Quaternion? basis = null)
        {
            basis ??= Quaternion.identity;
            return basis.Value * (Rotation + RotationOffset).ToQuaternion();
        }
        
        public Vector3 ToPosition(Quaternion? basis = null)
        {
            basis ??= Quaternion.identity;
            var rotation = ToRotation(basis);
            var pos = Pivot - rotation * (Vector3.forward * Distance)  // main pos
                      + rotation * ViewOffset; // with view offset
            return pos;
        }

        public void SetFromWorldPosition(Vector3 position, Quaternion? rotation = null)
        {
            Rotation = rotation ?? Quaternion.LookRotation(Pivot - position);
            Distance = Vector3.Distance(position, Pivot);
        }

        private EulerAngles ApplyPitchLimits(EulerAngles rotation)
        {
            //if (rotationEuler.x > 180) rotationEuler.x -= 360;
            rotation.pitch = Mathf.Clamp(rotation.pitch, PitchLimits.x, PitchLimits.y);
            rotation.roll = 0;
            return rotation;
        }
    }
}