using System;
using System.Text;
using FS.TagSystem;
using UnityEngine;

namespace FS.Animation
{
    // TODO: Remember the thing i wanted to do where we can do a "GetFlagTime(Flag)" to get the time when a flag is triggered in an animation clip?
    // [Flags]
    // public enum AnimationFlags
    // {
    //     // Generic gameplay flags
    //     SKID_START = 1 << 0,
    //     SKID_END = 1 << 1,
    //     
    //     LEDGE_GRAB_SETTLED = 1 << 2,
    //
    //     // Spring Kick Dash Flags
    //     SPRING_KICK_WALL_EJECT = 1 << 3,
    //     SPRING_KICK_ENEMY_BOUNCE_LAUNCH = 1 << 4,
    // }
    
    [Serializable]
    [EventPath("Animation Flag")]
    public class AnimationFlag : IAnimationEvent
    {
        public string Name
        {
            get
            {
                if (Tags.Count == 0) return "None";
                if (Tags.Count == 1) return Tags[0].ShortName;

                StringBuilder strBuilder = new StringBuilder();
                int maxCount = 3;
                foreach (var tag in Tags.Tags)
                {
                    if (maxCount == 0) break;
                    maxCount--;
                    strBuilder.Append($"{tag.ShortName}, ");
                }

                strBuilder.Append("...");
                return strBuilder.ToString();
            }
        }

        public bool IsRangedEvent => false;
        
        [TagFilter(Tag.Animation_.Path)]
        public TagSet Tags;

        public void Execute(GameObject context, float normalizedTime)
        {
            var animator = context.GetComponentInChildren<FSAnimator>();
            if (animator)
            {
                animator.BroadcastAnimationFlag(Tags);
            }
        }

        public static bool TryGetFlagTime(FSAnimation anim, Tag flag, out float time)
        {
            foreach (var evt in anim.GetEventsFor(-1).Events)
            {
                if (evt.Event is AnimationFlag animFlag && animFlag.Tags.Has(flag))
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