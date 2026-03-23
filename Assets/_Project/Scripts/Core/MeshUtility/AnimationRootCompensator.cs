using FS.Extensions;
using UnityEngine;

public class AnimationRootCompensator : MonoBehaviour
{
    private Transform m_rootBone;

    private void Awake()
    {
        m_rootBone = transform.FindChildRecursive("root");
    }

    /// <summary>
    /// The root bone's local position this frame, before compensation.
    /// Other systems can read this to know the animation's intended offset.
    /// </summary>
    public Vector3 AnimationRootLocalPosition { get; private set; }
    public Quaternion AnimationRootLocalRotation { get; private set; }
    
    private void LateUpdate()
    {
        if (m_rootBone == null) return;
        
        AnimationRootLocalPosition = m_rootBone.localPosition;
        AnimationRootLocalRotation = m_rootBone.localRotation;
        
        transform.localPosition = -AnimationRootLocalPosition;
        
        // TODO: Rotation if needed
    }
}