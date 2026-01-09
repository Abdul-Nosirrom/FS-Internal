
using PrimeTween;
using UnityEngine;

// TODO: Source gen would be nice
public static class PhysicsTweens
{
    public static Tween Velocity(PhysicsController physics, Vector3 start, Vector3 end, TweenSettings settings) => Tween.Custom(physics, start, end, settings, SetVelocity);
    public static Tween Velocity(PhysicsController physics, Vector3 start, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => Velocity(physics, start, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    public static Tween Velocity(PhysicsController physics, Vector3 end, TweenSettings settings) => Velocity(physics, physics.Velocity, end, settings);
    public static Tween Velocity(PhysicsController physics, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => Velocity(physics, physics.Velocity, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    
    public static Tween LateralVelocity(PhysicsController physics, Vector3 start, Vector3 end, TweenSettings settings) => Tween.Custom(physics, start, end, settings, SetLateralVelocity);
    public static Tween LateralVelocity(PhysicsController physics, Vector3 start, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => LateralVelocity(physics, start, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    public static Tween LateralVelocity(PhysicsController physics, Vector3 end, TweenSettings settings) => LateralVelocity(physics, physics.LateralVelocity, end, settings);
    public static Tween LateralVelocity(PhysicsController physics, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => LateralVelocity(physics, physics.LateralVelocity, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    
    public static Tween VerticalVelocity(PhysicsController physics, Vector3 start, Vector3 end, TweenSettings settings) => Tween.Custom(physics, start, end, settings, SetVerticalVelocity);
    public static Tween VerticalVelocity(PhysicsController physics, Vector3 start, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => VerticalVelocity(physics, start, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    public static Tween VerticalVelocity(PhysicsController physics, Vector3 end, TweenSettings settings) => VerticalVelocity(physics, physics.VerticalVelocity, end, settings);
    public static Tween VerticalVelocity(PhysicsController physics, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => VerticalVelocity(physics, physics.VerticalVelocity, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    
    public static Tween Speed(PhysicsController physics, float start, float end, TweenSettings settings) => Tween.Custom(physics, start, end, settings, SetSpeed);
    public static Tween Speed(PhysicsController physics, float start, float end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => Speed(physics, start, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    public static Tween Speed(PhysicsController physics, float end, TweenSettings settings) => Speed(physics, physics.Speed, end, settings);
    public static Tween Speed(PhysicsController physics, float end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => Speed(physics, physics.Speed, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    
    public static Tween LateralSpeed(PhysicsController physics, float start, float end, TweenSettings settings) => Tween.Custom(physics, start, end, settings, SetLateralSpeed);
    public static Tween LateralSpeed(PhysicsController physics, float start, float end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => LateralSpeed(physics, start, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    public static Tween LateralSpeed(PhysicsController physics, float end, TweenSettings settings) => LateralSpeed(physics, physics.LateralSpeed, end, settings);
    public static Tween LateralSpeed(PhysicsController physics, float end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => LateralSpeed(physics, physics.LateralSpeed, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    
    public static Tween VerticalSpeed(PhysicsController physics, float start, float end, TweenSettings settings) => Tween.Custom(physics, start, end, settings, SetVerticalSpeed);
    public static Tween VerticalSpeed(PhysicsController physics, float start, float end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => VerticalSpeed(physics, start, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    public static Tween VerticalSpeed(PhysicsController physics, float end, TweenSettings settings) => VerticalSpeed(physics, physics.VerticalSpeed, end, settings);
    public static Tween VerticalSpeed(PhysicsController physics, float end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => VerticalSpeed(physics, physics.VerticalSpeed, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    
    public static Tween Position(PhysicsController physics, Vector3 start, Vector3 end, TweenSettings settings) => Tween.Custom(physics, start, end, settings, SetPosition);
    public static Tween Position(PhysicsController physics, Vector3 start, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => Position(physics, start, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    public static Tween Position(PhysicsController physics, Vector3 end, TweenSettings settings) => Position(physics, physics.Position, end, settings);
    public static Tween Position(PhysicsController physics, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => Position(physics, physics.Position, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    
    public static Tween HeadPosition(PhysicsController physics, Vector3 start, Vector3 end, TweenSettings settings) => Tween.Custom(physics, start, end, settings, SetHeadPosition);
    public static Tween HeadPosition(PhysicsController physics, Vector3 start, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => HeadPosition(physics, start, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    public static Tween HeadPosition(PhysicsController physics, Vector3 end, TweenSettings settings) => HeadPosition(physics, physics.HeadPosition, end, settings);
    public static Tween HeadPosition(PhysicsController physics, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => HeadPosition(physics, physics.HeadPosition, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    
    public static Tween CenterPosition(PhysicsController physics, Vector3 start, Vector3 end, TweenSettings settings) => Tween.Custom(physics, start, end, settings, SetCenterPosition);
    public static Tween CenterPosition(PhysicsController physics, Vector3 start, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => CenterPosition(physics, start, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    public static Tween CenterPosition(PhysicsController physics, Vector3 end, TweenSettings settings) => CenterPosition(physics, physics.CenterPosition, end, settings);
    public static Tween CenterPosition(PhysicsController physics, Vector3 end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => CenterPosition(physics, physics.CenterPosition, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    
    public static Tween Rotation(PhysicsController physics, Quaternion start, Quaternion end, TweenSettings settings) => Tween.Custom(physics, start, end, settings, SetRotation);
    public static Tween Rotation(PhysicsController physics, Quaternion start, Quaternion end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => Rotation(physics, start, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));
    public static Tween Rotation(PhysicsController physics, Quaternion end, TweenSettings settings) => Rotation(physics, physics.Rotation, end, settings);
    public static Tween Rotation(PhysicsController physics, Quaternion end, float duration, Ease ease = Ease.Default, int cycles = 1,
        CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        => Rotation(physics, physics.Rotation, end, new TweenSettings(duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime));

    private static void SetVelocity(PhysicsController physics, Vector3 newVelocity) => physics.Velocity = newVelocity;
    private static void SetLateralVelocity(PhysicsController physics, Vector3 newLateralVelocity) => physics.LateralVelocity = newLateralVelocity;
    private static void SetVerticalVelocity(PhysicsController physics, Vector3 newVerticalVelocity) => physics.VerticalVelocity = newVerticalVelocity;
    private static void SetSpeed(PhysicsController physics, float newSpeed) => physics.Speed = newSpeed;
    private static void SetLateralSpeed(PhysicsController physics, float newLateralSpeed) => physics.LateralSpeed = newLateralSpeed;
    private static void SetVerticalSpeed(PhysicsController physics, float newVerticalSpeed) => physics.VerticalSpeed = newVerticalSpeed;
    private static void SetPosition(PhysicsController physics, Vector3 newPosition) => physics.Position = newPosition;
    private static void SetHeadPosition(PhysicsController physics, Vector3 newHeadPosition) => physics.HeadPosition = newHeadPosition;
    private static void SetCenterPosition(PhysicsController physics, Vector3 newCenterPosition) => physics.CenterPosition = newCenterPosition;
    private static void SetRotation(PhysicsController physics, Quaternion newRotation) => physics.Rotation = newRotation;
}