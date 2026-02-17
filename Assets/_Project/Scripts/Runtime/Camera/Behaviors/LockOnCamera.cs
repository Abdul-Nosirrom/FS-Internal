using Drawing;
using FS.CameraSystem;
using FS.CombatSystem;
using FS.Math;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Lock-on camera behavior based on the GenoKids "double target" approach.
/// 
/// Core concept: The camera always orbits the player — the pivot never moves to a midpoint.
/// Instead, lock-on works by:
/// 1. Gently steering the orbit yaw toward the player→enemy direction (with a deadzone)
/// 2. Blending the final look direction between "orbit forward" and "toward enemy"
/// 3. Adjusting pitch conservatively based on vertical angle to enemy
/// 
/// This avoids pivot instability during crossing, keeps the player always centered,
/// and naturally handles close-range combat without spinning.
/// </summary>
public class LockOnCamera : CameraBehavior
{
    private LockOnController m_lockOn;

    #region Tuning Parameters

    [Header("Yaw Steering")]
    [SerializeField, Range(0f, 1f), Tooltip("Deadzone before camera starts rotating toward enemy, as fraction of FOV. " +
        "At 0.66, enemy must be outside ~66% of FOV before camera chases.")]
    private float m_deadzoneAsFovFraction = 0.66f;

    [SerializeField, Range(0f, 1f), Tooltip("Base speed for rotating yaw toward the enemy direction")]
    private float m_yawSteerSpeed = 0.1f;

    [SerializeField, Tooltip("Curve: X = distance to enemy, Y = multiplier on yaw steer speed. " +
        "Close enemies should steer slower to prevent spinning during close combat.")]
    private AnimationCurve m_yawSteerByDistance = new AnimationCurve(
        new Keyframe(0.5f, 0.025f, 0f, 0f),
        new Keyframe(5f, 0.5f, 0.317f, 0.317f),
        new Keyframe(6f, 1f, 0f, 0f)
    );

    [Header("Look Direction Blend")]
    [SerializeField, Range(0f, 1f), Tooltip("Blend between orbit look direction (0) and enemy look direction (1). " +
        "0.5 = equal blend, keeps both player and enemy reasonably framed.")]
    private float m_lookBlendToEnemy = 0.5f;

    [SerializeField, Range(1f, 30f), Tooltip("Smoothing speed for the look blend")]
    private float m_lookBlendSmooth = 10f;

    [Header("Pitch")]
    [SerializeField, Tooltip("Pitch limits when locked on. Narrower than normal to keep things stable.")]
    private Vector2 m_lockOnPitchLimits = new Vector2(-5f, 25f);

    [SerializeField, Range(1f, 60f), Tooltip("How fast pitch adjusts during lock-on")]
    private float m_pitchSmooth = 30f;

    [SerializeField, Range(0f, 20f), Tooltip("Base desired pitch angle during lock-on")]
    private float m_basePitch = 5f;

    [Header("Enemy Position Tracking")]
    [SerializeField, Range(1f, 30f), Tooltip("How fast the tracked enemy position smooths. " +
        "Higher = snappier enemy tracking, lower = more cinematic.")]
    private float m_enemyPosSmooth = 12.5f;

    [SerializeField, Tooltip("Range over which enemy smoothing replaces player-follow smoothing. " +
        "X = start blending, Y = fully replaced.")]
    private Vector2 m_enemySmoothBlendRange = new Vector2(2f, 5f);

    [Header("Enemy Switch Smoothing")]
    [SerializeField, Range(1f, 30f), Tooltip("How fast the camera adapts when switching lock-on targets")]
    private float m_targetSwitchSmooth = 10f;

    [Header("Wall Avoidance")]
    [SerializeField, Tooltip("Enable raycasting left/right to push camera away from walls")]
    private bool m_pushSidewaysFromWalls = true;

    [SerializeField, Range(0.5f, 5f), Tooltip("Raycast distance for wall detection")]
    private float m_wallRaycastLength = 2f;

    [SerializeField, Tooltip("Curve: X = wall proximity imbalance (0-1), Y = degrees/sec to push")]
    private AnimationCurve m_wallPushStrength = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 75f, 0f, 0f)
    );

    [SerializeField]
    private LayerMask m_wallLayerMask = 1; // Default layer

    [Header("Behind-Target Prevention")]
    [SerializeField, Tooltip("Curve: X = distance to camera collision, Y = angle threshold. " +
        "Prevents camera from getting between player and target.")]
    private AnimationCurve m_behindTargetAngle = new AnimationCurve(
        new Keyframe(0f, 20f, 0f, 0f),
        new Keyframe(3f, 10f, 0f, 0f)
    );

    [SerializeField, Range(0f, 1f), Tooltip("Min input speed to trigger behind-target correction")]
    private float m_behindTargetMinSpeed = 0.2f;

    [Header("Distance")]
    [SerializeField, Range(2f, 8f)]
    private float m_minDistance = 3f;

    [SerializeField, Range(8f, 25f)]
    private float m_maxDistance = 16f;

    [SerializeField, Range(1f, 15f), Tooltip("How fast distance adjusts")]
    private float m_distanceSmooth = 8f;

    [Header("Camera Assist")]
    [SerializeField, Tooltip("X = time after last input before assist kicks in. Y = transition duration.")]
    private Vector2 m_assistTiming = new Vector2(1.5f, 1.5f);

    #endregion

    #region Runtime State

    /// <summary>
    /// Accumulated camera euler angles — the "orbit state" that gets steered.
    /// This is equivalent to GenoKids' camRotForPosition.
    /// </summary>
    private Vector3 m_orbitEuler;

    /// <summary>
    /// Smoothed version of m_orbitEuler, used for actual camera positioning.
    /// </summary>
    private Vector3 m_smoothedOrbitEuler;

    /// <summary>
    /// Smoothed enemy world position for stable tracking.
    /// </summary>
    private Vector3 m_smoothedEnemyPos;

    /// <summary>
    /// Rotation offset applied when switching targets, smoothed to zero over time.
    /// Prevents camera snapping on target switch.
    /// </summary>
    private Vector3 m_targetSwitchOffset;

    /// <summary>
    /// Smoothed look-blend alpha between orbit forward and enemy direction.
    /// </summary>
    private float m_currentLookBlend;

    /// <summary>
    /// Smoothed pitch value.
    /// </summary>
    private float m_smoothedPitch;

    /// <summary>
    /// Previous lock-on target for detecting target switches.
    /// </summary>
    private Transform m_prevTarget;

    /// <summary>
    /// Smoothed player position for stable pivot (horizontal).
    /// </summary>
    private Vector2 m_smoothedPlayerPosXZ;

    /// <summary>
    /// Smoothed player position (vertical), with asymmetric up/down speeds.
    /// </summary>
    private float m_smoothedPlayerPosY;

    /// <summary>
    /// Current smoothed distance.
    /// </summary>
    private float m_currentDistance;

    /// <summary>
    /// Cached last euler before look blend, used for target switch offset calculation.
    /// </summary>
    private Vector3 m_lastWantedEuler;

    #endregion

    #region Lifecycle

    protected override void Initialize()
    {
        m_lockOn = m_cameraController.GetComponent<LockOnController>();

        BlendInParams = CameraBlendParams.Create(0.35f, Ease.InOutQuad, false);
        BlendOutParams = CameraBlendParams.Create(0.4f, Ease.OutQuad, false);

        // We control pivot, distance, and rotation ourselves
        InheritedCameraValues = ~(
            CameraBehaviorInheritance.Pivot |
            CameraBehaviorInheritance.Distance |
            CameraBehaviorInheritance.Rotation
        );
    }

    public override bool ShouldActivate() => m_lockOn && m_lockOn.IsLockOnActive;

    protected override void OnCameraActivated()
    {
        var playerPos = m_cameraController.PlayerPivot;
        var currentRot = m_cameraController.m_cameraVector.Rotation;

        // Initialize orbit euler from current camera state
        m_orbitEuler = new Vector3(currentRot.pitch, currentRot.yaw, 0f);
        m_smoothedOrbitEuler = m_orbitEuler;

        // Initialize player position smoothing
        m_smoothedPlayerPosXZ = new Vector2(playerPos.x, playerPos.z);
        m_smoothedPlayerPosY = playerPos.y;

        // Initialize enemy tracking
        if (m_lockOn.CurrentLockOnTarget != null)
        {
            m_smoothedEnemyPos = m_lockOn.CurrentLockOnTarget.transform.position;
            m_prevTarget = m_lockOn.CurrentLockOnTarget.transform;
        }

        m_targetSwitchOffset = Vector3.zero;
        m_currentLookBlend = 0f; // Start blended toward orbit, ease into enemy tracking
        m_smoothedPitch = m_orbitEuler.x;
        m_currentDistance = m_cameraController.m_cameraVector.Distance;
        m_lastWantedEuler = m_orbitEuler;
    }

    #endregion

    #region Main Update

    public override void UpdateCamera(in CameraOrbitVector inOrbitVector)
    {
        if (!m_lockOn.IsLockOnActive || m_lockOn.CurrentLockOnTarget == null) return;

        float dt = Time.deltaTime;
        var playerPos = m_cameraController.PlayerPivot;
        var targetTransform = m_lockOn.CurrentLockOnTarget.transform;
        var targetPos = targetTransform.position;

        // --- Detect target switch ---
        HandleTargetSwitch(targetTransform);

        // --- Accumulate player input into orbit euler ---
        AccumulateInput();

        // --- Compute camera assist alpha (0 = player controlling, 1 = full assist) ---
        float assistAlpha = ComputeAssistAlpha();

        // --- Smooth enemy position ---
        m_smoothedEnemyPos = Vector3.Lerp(m_smoothedEnemyPos, targetPos, m_enemyPosSmooth * dt);

        // --- Smooth player position (pivot) ---
        var pivot = UpdateSmoothedPlayerPos(playerPos, dt);

        // --- Compute desired pitch & apply ---
        UpdatePitch(pivot, targetPos, assistAlpha, dt);

        // --- Steer yaw toward enemy (with deadzone) ---
        SteerYawTowardEnemy(pivot, assistAlpha, dt);

        // --- Wall avoidance ---
        if (m_pushSidewaysFromWalls)
            ApplyWallAvoidance(dt);

        // --- Clamp pitch ---
        m_orbitEuler.x = Mathf.Clamp(m_orbitEuler.x, m_lockOnPitchLimits.x, m_lockOnPitchLimits.y);

        // --- Smooth the orbit euler for positioning ---
        m_smoothedOrbitEuler = LerpEuler(m_smoothedOrbitEuler, m_orbitEuler, 15f * dt);

        // --- Compute camera distance ---
        UpdateDistance(pivot, targetPos, dt);

        // --- Position camera from orbit ---
        var orbitRotation = Quaternion.Euler(m_smoothedOrbitEuler);
        var orbitPosition = pivot - orbitRotation * Vector3.forward * m_currentDistance;

        // --- Compute the two look directions ---
        // 1. Orbit look direction (looking back at pivot from orbit position)
        Vector3 orbitForward = (pivot - orbitPosition);
        if (orbitForward.sqrMagnitude < 0.01f) orbitForward = orbitRotation * Vector3.forward;
        else orbitForward.Normalize();

        // 2. Enemy look direction (from camera position toward smoothed enemy)
        Vector3 toEnemy = (m_smoothedEnemyPos - orbitPosition);
        if (toEnemy.sqrMagnitude < 0.01f) toEnemy = orbitForward;
        else toEnemy.Normalize();

        // --- Blend look directions ---
        m_currentLookBlend = Mathf.Lerp(m_currentLookBlend, m_lookBlendToEnemy, m_lookBlendSmooth * dt);

        Quaternion orbitLook = Quaternion.LookRotation(orbitForward, UpVector);
        Quaternion enemyLook = Quaternion.LookRotation(toEnemy, UpVector);
        Quaternion blendedLook = Quaternion.Slerp(orbitLook, enemyLook, m_currentLookBlend);

        // --- Apply target switch offset (smoothed to zero) ---
        m_targetSwitchOffset = Vector3.Lerp(m_targetSwitchOffset, Vector3.zero, m_targetSwitchSmooth * dt);
        Vector3 finalEuler = blendedLook.eulerAngles + m_targetSwitchOffset;
        m_lastWantedEuler = blendedLook.eulerAngles;

        // --- Write to camera vector ---
        m_cameraVector.Pivot = pivot;
        m_cameraVector.Distance = m_currentDistance;
        m_cameraVector.Rotation = new EulerAngles(finalEuler.x, finalEuler.y, 0f);
    }

    #endregion

    #region Camera Assist

    /// <summary>
    /// Computes the camera assist alpha: 0 = player is actively controlling camera, 1 = full auto.
    /// After the player stops inputting, there's a delay before assist kicks in, then it eases in.
    /// Matches GenoKids' assistTimeDisabled system.
    /// </summary>
    private float ComputeAssistAlpha()
    {
        float timeSinceInput = m_cameraController.TimeSinceLastInput;
        float startTime = m_assistTiming.x;
        float endTime = m_assistTiming.x + m_assistTiming.y;

        return Mathf.Clamp01((timeSinceInput - startTime) / Mathf.Max(0.01f, endTime - startTime));
    }

    #endregion

    #region Player Input

    /// <summary>
    /// Accumulates player stick/mouse input directly into the orbit euler angles.
    /// This is the same model as GenoKids' camRotForPosition — input always applies,
    /// and the lock-on systems steer on top of it.
    /// </summary>
    private void AccumulateInput()
    {
        var inputDelta = m_cameraController.LookInputDelta;
        m_orbitEuler.x += inputDelta.pitch;
        m_orbitEuler.y += inputDelta.yaw;
    }

    #endregion

    #region Smoothed Player Position

    /// <summary>
    /// Smooths the player position for the camera pivot.
    /// Horizontal (XZ) uses uniform smoothing.
    /// Vertical (Y) uses asymmetric smoothing: faster going down, slower going up.
    /// This matches GenoKids' genokidPosXZ/YSmoother approach.
    /// </summary>
    private Vector3 UpdateSmoothedPlayerPos(Vector3 playerPos, float dt)
    {
        // Horizontal
        float hSmooth = 25f;
        m_smoothedPlayerPosXZ = Vector2.Lerp(
            m_smoothedPlayerPosXZ,
            new Vector2(playerPos.x, playerPos.z),
            hSmooth * dt
        );

        // Vertical — asymmetric: up is slower (20), down is faster (30)
        float vTarget = playerPos.y;
        float vSmooth = vTarget >= m_smoothedPlayerPosY ? 20f : 30f;
        m_smoothedPlayerPosY = Mathf.Lerp(m_smoothedPlayerPosY, vTarget, vSmooth * dt);

        return new Vector3(m_smoothedPlayerPosXZ.x, m_smoothedPlayerPosY, m_smoothedPlayerPosXZ.y);
    }

    #endregion

    #region Pitch

    /// <summary>
    /// Adjusts pitch based on vertical angle to enemy, clamped to conservative limits.
    /// Matches GenoKids' approach: compute the inclination angle to the enemy, 
    /// blend it with the base pitch, and clamp to pitchMinMaxWithSecondaryTarget.
    /// </summary>
    private void UpdatePitch(Vector3 pivot, Vector3 targetPos, float assistAlpha, float dt)
    {
        float desiredPitch = m_basePitch;

        // Vertical angle from pivot to enemy
        Vector3 toEnemy = targetPos - pivot;
        float horizontalDist = new Vector2(toEnemy.x, toEnemy.z).magnitude;

        if (horizontalDist > 0.1f)
        {
            // Signed angle: positive = enemy below, negative = enemy above
            float inclinationAngle = Mathf.Atan2(-toEnemy.y, horizontalDist) * Mathf.Rad2Deg;
            float enemyPitch = Mathf.Clamp(
                inclinationAngle + m_basePitch,
                m_lockOnPitchLimits.x,
                m_lockOnPitchLimits.y
            );

            // Blend toward enemy pitch based on distance (further = stronger influence)
            float blendFactor = Mathf.Clamp01((toEnemy.magnitude - 2f) * 0.5f);
            desiredPitch = Mathf.Lerp(desiredPitch, enemyPitch, blendFactor);
        }

        m_smoothedPitch = Mathf.Lerp(m_smoothedPitch, desiredPitch, m_pitchSmooth * dt);

        // Apply pitch correction into orbit euler, gated by assist alpha
        // The 0.016 cap matches GenoKids' pitchMaxSpeed — limits how fast pitch changes per frame
        float pitchDelta = m_smoothedPitch - m_orbitEuler.x;
        float maxPitchStep = 0.016f * assistAlpha;
        m_orbitEuler.x += Mathf.Clamp(pitchDelta * maxPitchStep, -2f, 2f);
    }

    #endregion

    #region Yaw Steering

    /// <summary>
    /// Steers the orbit yaw toward the player→enemy direction, with a deadzone.
    /// 
    /// GenoKids' approach:
    /// 1. Compute direction from player to enemy
    /// 2. Create a rotation looking that direction
    /// 3. RotateTowards from that to current orbit rotation, clamped by deadzone (fraction of FOV)
    /// 4. Lerp yaw toward that, scaled by distance curve
    /// 
    /// The deadzone prevents the camera from chasing small movements.
    /// The distance scaling prevents spinning during close combat.
    /// </summary>
    private void SteerYawTowardEnemy(Vector3 pivot, float assistAlpha, float dt)
    {
        if (assistAlpha < 0.01f) return; // Player is actively controlling, skip steering

        // Direction from smoothed player to smoothed enemy
        Vector3 playerToEnemy = m_smoothedEnemyPos - pivot;
        Vector2 playerToEnemyFlat = new Vector2(playerToEnemy.x, playerToEnemy.z);
        float flatDistance = playerToEnemyFlat.magnitude;

        if (flatDistance < 0.5f) return; // Too close horizontally to determine a meaningful direction

        // Compute the ideal look direction: from player toward enemy
        Quaternion lookAtEnemy = Quaternion.LookRotation(playerToEnemy, UpVector);

        // Deadzone: RotateTowards from enemy-look direction back toward our current orbit.
        // This creates an "allowed zone" — if our current orbit is within the deadzone angle
        // of the enemy direction, the result equals our current orbit (no steering).
        // If outside, it gives us a target on the edge of the deadzone.
        float deadzoneAngle = m_deadzoneAsFovFraction * m_cameraController.m_camera.fieldOfView;
        Quaternion deadzoned = Quaternion.RotateTowards(
            lookAtEnemy,
            Quaternion.Euler(m_orbitEuler),
            deadzoneAngle
        );

        // Steer speed scales with distance: slower when close (prevents spinning), faster when far
        float steerFactor = m_yawSteerSpeed * m_yawSteerByDistance.Evaluate(flatDistance) * assistAlpha;

        m_orbitEuler.y = Mathf.LerpAngle(
            m_orbitEuler.y,
            deadzoned.eulerAngles.y,
            steerFactor
        );
    }

    #endregion

    #region Wall Avoidance

    /// <summary>
    /// Raycasts left and right from the camera position. If one side is closer to a wall,
    /// pushes the yaw away from it. Matches GenoKids' pushSidewaysIfCloseToWall.
    /// </summary>
    private void ApplyWallAvoidance(float dt)
    {
        var camPos = m_cameraController.CameraState.m_position;
        Vector3 right = Quaternion.Euler(0f, m_orbitEuler.y, 0f) * Vector3.right;

        float rightDist = m_wallRaycastLength;
        float leftDist = m_wallRaycastLength;

        if (Physics.Raycast(camPos, right, out RaycastHit hitRight, m_wallRaycastLength, m_wallLayerMask, QueryTriggerInteraction.Ignore))
            rightDist = hitRight.distance;

        if (Physics.Raycast(camPos, -right, out RaycastHit hitLeft, m_wallRaycastLength, m_wallLayerMask, QueryTriggerInteraction.Ignore))
            leftDist = hitLeft.distance;

        // Positive = right wall closer, negative = left wall closer
        float imbalance = (rightDist - leftDist) / m_wallRaycastLength;
        float pushDegrees = Mathf.Sign(imbalance) * m_wallPushStrength.Evaluate(Mathf.Abs(imbalance));

        m_orbitEuler.y -= pushDegrees * dt;
    }

    #endregion

    #region Distance

    /// <summary>
    /// Computes camera distance. Gently pulls back based on player-enemy separation.
    /// </summary>
    private void UpdateDistance(Vector3 pivot, Vector3 targetPos, float dt)
    {
        float separation = Vector3.Distance(pivot, targetPos);

        // Base distance from the orbit vector's default
        float baseDistance = m_cameraController.m_cameraVector.Distance;
        float targetDistance = baseDistance + separation * 0.15f;

        targetDistance = Mathf.Clamp(targetDistance, m_minDistance, m_maxDistance);
        m_currentDistance = Mathf.Lerp(m_currentDistance, targetDistance, m_distanceSmooth * dt);
    }

    #endregion

    #region Target Switch

    /// <summary>
    /// Detects when the lock-on target changes and stores a rotation offset that gets smoothed to zero.
    /// This prevents camera snapping when switching targets.
    /// Matches GenoKids' enemyChangedRotSmoother.
    /// </summary>
    private void HandleTargetSwitch(Transform newTarget)
    {
        if (m_prevTarget != null && newTarget != m_prevTarget)
        {
            // Store the angular difference so we can smooth it out
            Vector3 oldDir = (m_smoothedEnemyPos - m_cameraController.CameraState.m_position).normalized;
            Vector3 newDir = (newTarget.position - m_cameraController.CameraState.m_position).normalized;

            if (oldDir.sqrMagnitude > 0.01f && newDir.sqrMagnitude > 0.01f)
            {
                Quaternion oldLook = Quaternion.LookRotation(oldDir, UpVector);
                Quaternion newLook = Quaternion.LookRotation(newDir, UpVector);

                // The offset is the difference between where we WERE looking and where we SHOULD look.
                // By adding this to the final euler and smoothing it to zero, the camera
                // holds its current angle and gradually transitions to the new target.
                Vector3 delta = new Vector3(
                    Mathf.DeltaAngle(newLook.eulerAngles.x, oldLook.eulerAngles.x),
                    Mathf.DeltaAngle(newLook.eulerAngles.y, oldLook.eulerAngles.y),
                    0f
                );

                m_targetSwitchOffset += delta;
            }

            // Reset smoothed enemy pos to new target to avoid lerping across the world
            m_smoothedEnemyPos = newTarget.position;
        }

        m_prevTarget = newTarget;
    }

    #endregion

    #region Utility

    /// <summary>
    /// Lerps euler angles handling wraparound correctly.
    /// </summary>
    private static Vector3 LerpEuler(Vector3 from, Vector3 to, float t)
    {
        return new Vector3(
            Mathf.LerpAngle(from.x, to.x, t),
            Mathf.LerpAngle(from.y, to.y, t),
            Mathf.LerpAngle(from.z, to.z, t)
        );
    }

    #endregion

    #region Debug Visualization

    public override void OnDrawCameraGizmos(CameraBehaviorController camera)
    {
        if (!m_lockOn || !m_lockOn.IsLockOnActive || m_lockOn.CurrentLockOnTarget == null) return;

        var playerPos = camera.PlayerPivot;
        var targetPos = m_lockOn.CurrentLockOnTarget.transform.position;
        var camPos = camera.CameraState.m_position;

        // Pivot (smoothed player pos)
        Vector3 pivot = new Vector3(m_smoothedPlayerPosXZ.x, m_smoothedPlayerPosY, m_smoothedPlayerPosXZ.y);
        Draw.ingame.WireSphere(pivot, 0.12f, Color.cyan);
        Draw.ingame.Label2D(pivot + Vector3.up * 0.3f, "Pivot", 12, Color.cyan);

        // Smoothed enemy pos
        Draw.ingame.WireSphere(m_smoothedEnemyPos, 0.12f, Color.red);
        Draw.ingame.Label2D(m_smoothedEnemyPos + Vector3.up * 0.3f, "Enemy (smoothed)", 10, Color.red);

        // Actual target
        Draw.ingame.WireSphere(targetPos, 0.08f, new Color(1f, 0.3f, 0.3f, 0.4f));

        // Player→Enemy line
        Draw.ingame.Line(playerPos, targetPos, new Color(1f, 1f, 1f, 0.2f));

        // Orbit direction (where the orbit position logic thinks we should face)
        Vector3 orbitDir = Quaternion.Euler(m_smoothedOrbitEuler) * Vector3.forward;
        Draw.ingame.Arrow(pivot, pivot + orbitDir * 2f, Color.yellow);

        // Look blend visualization
        Vector3 orbitForward = (pivot - camPos).normalized;
        Vector3 toEnemy = (m_smoothedEnemyPos - camPos).normalized;
        if (orbitForward.sqrMagnitude > 0.01f && toEnemy.sqrMagnitude > 0.01f)
        {
            Vector3 blendedDir = Vector3.Slerp(orbitForward, toEnemy, m_currentLookBlend);
            Draw.ingame.Arrow(camPos, camPos + blendedDir * 2f, Color.green);
            Draw.ingame.Label2D(camPos + blendedDir * 2.2f, $"Blend: {m_currentLookBlend:F2}", 10, Color.green);
        }

        // Deadzone indicator
        float deadzoneAngle = m_deadzoneAsFovFraction * camera.m_camera.fieldOfView;
        Draw.ingame.Label2D(camPos + Vector3.up * 0.5f, $"DZ: {deadzoneAngle:F0}deg", 10, Color.yellow);

        // Target switch offset magnitude
        if (m_targetSwitchOffset.sqrMagnitude > 0.1f)
            Draw.ingame.Label2D(camPos - Vector3.up * 0.3f, $"Switch: {m_targetSwitchOffset.magnitude:F1}deg", 10, Color.magenta);
    }

    #endregion
}