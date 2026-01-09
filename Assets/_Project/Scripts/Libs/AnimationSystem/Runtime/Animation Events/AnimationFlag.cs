using System;
using UnityEngine;

namespace FS.Animation
{
    // TODO: Remember the thing i wanted to do where we can do a "GetFlagTime(Flag)" to get the time when a flag is triggered in an animation clip?
    [Flags]
    public enum AnimationFlags
    {
        // Generic gameplay flags
        SKID_START = 1 << 0,
        SKID_END = 1 << 1,
        
        LEDGE_GRAB_SETTLED = 1 << 2,

        // Spring Kick Dash Flags
        SPRING_KICK_WALL_EJECT = 1 << 3,
        SPRING_KICK_ENEMY_BOUNCE_LAUNCH = 1 << 4,
    }
    
    [Serializable]
    [EventPath("Animation Flag")]
    public class AnimationFlag : IAnimationEvent
    {
        public string Name => Flags.ToString();

        public bool IsRangedEvent => false;

        public AnimationFlags Flags;

        public void Execute(GameObject context, float normalizedTime)
        {
            var animator = context.GetComponentInChildren<FSAnimator>();
            if (animator)
            {
                animator.BroadcastAnimationFlag(Flags);
            }
        }

        public static bool TryGetFlagTime(FSAnimation anim, AnimationFlags flag, out float time)
        {
            foreach (var evt in anim.GetEventsFor(-1).Events)
            {
                if (evt.Event is AnimationFlag animFlag && (animFlag.Flags & flag) != 0)
                {
                    time = evt.TriggerTime;
                    return true;
                }
            }
            
            time = -1f;
            return false;
        }
    }
}