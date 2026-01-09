using FS.Attributes;
using FS.UI;
using UnityEngine;

[Experimental]
[RequireComponent(typeof(Rigidbody))]
public class PhysicsAnimator : TweenAnimator
{
    public bool m_playOnStart = false;
    public bool m_pingPong = true;
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        for (int i = 0; i < m_tweenAnimations.Count; i++)
        {
            var anim = m_tweenAnimations[i];
            if (anim.Animation == null) continue;
            
            var targetObj = anim.Animation.GetType().GetField("Target", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (targetObj == null) continue;
            if (targetObj.FieldType == typeof(Transform))
                targetObj.SetValue(anim.Animation, transform);
            if (targetObj.FieldType == typeof(Rigidbody) && TryGetComponent<Rigidbody>(out var rb))
                targetObj.SetValue(anim.Animation, rb);
        }
    }
#endif

    private void Start()
    {
        if (m_playOnStart) Play();
    }

    private bool isReversed = false;
    
    protected override void OnSequenceComplete()
    {
        isReversed = !isReversed && m_pingPong;
        if (isReversed) Reverse();
        else Play();
    }
}