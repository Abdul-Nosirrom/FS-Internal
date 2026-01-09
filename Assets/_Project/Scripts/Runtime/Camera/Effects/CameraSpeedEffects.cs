
using FS.CameraSystem;
using PrimeTween;
using UnityEngine;

// https://youtu.be/RkuUUhKWd7k?si=sDf7xSKyyL9FHlCj
public class CameraHoldPosition : CameraFX
{
    //private const float HOLD_DURATION = 0.02f;
    //private const float SNAP_BACK_DURATION = 0.05f;

    public override Mode FXMode => Mode.CameraState;

    private Vector3 m_heldPosition;
    private Quaternion m_startRotation;
    
    protected override void OnStartFX()
    {
        // Camera effects should allow for direct modifications of CameraState because doing these effects
        // on an orbit vector isn't great
        m_heldPosition = m_cameraState.m_position;
        m_startRotation = m_cameraState.m_rotation;
    }

    public override void OnUpdateFX()
    {
        float HOLD_DURATION = 0.2f;
        float SNAP_BACK_DURATION = 0.3f;
        if (TimeSinceStarted < HOLD_DURATION)
        {
            m_heldPosition = Vector3.Lerp(m_heldPosition, m_cameraState.m_position, 5f * Time.deltaTime);
            m_cameraState.m_position = m_heldPosition;
            //m_cameraVector.SetFromWorldPosition(m_heldPosition, m_startRotation);
            //m_cameraController.CameraState.m_rotation = m_startRotation;
        }
        else if (TimeSinceStarted < SNAP_BACK_DURATION + HOLD_DURATION)
        {
            var snapBackAlpha = (TimeSinceStarted - HOLD_DURATION) / SNAP_BACK_DURATION;
            snapBackAlpha = Easing.Evaluate(snapBackAlpha, Ease.OutQuad);
            var currentPos = m_cameraState.m_position;
            var newPos = Vector3.Lerp(m_heldPosition, currentPos, snapBackAlpha);
            m_cameraState.m_position = newPos;
            //m_cameraVector.SetFromWorldPosition(newPos, m_startRotation);
        }
        else StopFX();
    }
}