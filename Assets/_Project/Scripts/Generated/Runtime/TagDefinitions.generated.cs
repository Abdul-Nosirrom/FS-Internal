// ============================================================================
// AUTO-GENERATED FILE — Do not edit manually.
// Generated from .gameTags definition files by TagImporter.
// ============================================================================

namespace FS.TagSystem
{
    public partial struct Tag
    {
        /// <summary>Tags broadcast by animation events for gameplay timing</summary>
        public static readonly Animation_ Animation = new();

        /// <summary>Tags broadcast by animation events for gameplay timing</summary>
        public class Animation_
        {
            public const string Path = "Animation";
            public static implicit operator Tag(Animation_ _) => new("Animation");

            public static readonly LedgeGrab_ LedgeGrab = new();

            public class LedgeGrab_
            {
                public const string Path = "Animation.LedgeGrab";
                public static implicit operator Tag(LedgeGrab_ _) => new("Animation.LedgeGrab");

                /// <summary>Ledge grab settled, allow input</summary>
                public static readonly Tag Settled = new("Animation.LedgeGrab.Settled");
                public const string Settled_Path = "Animation.LedgeGrab.Settled";

            }

            public static readonly SpringKick_ SpringKick = new();

            public class SpringKick_
            {
                public const string Path = "Animation.SpringKick";
                public static implicit operator Tag(SpringKick_ _) => new("Animation.SpringKick");

                /// <summary>Eject from wall during spring kick</summary>
                public static readonly Tag WallEject = new("Animation.SpringKick.WallEject");
                public const string WallEject_Path = "Animation.SpringKick.WallEject";

                /// <summary>Launch off enemy bounce</summary>
                public static readonly Tag EnemyBounce = new("Animation.SpringKick.EnemyBounce");
                public const string EnemyBounce_Path = "Animation.SpringKick.EnemyBounce";

            }

            /// <summary>Timing in which skid turn anim boosts out</summary>
            public static readonly Tag SkidBoost = new("Animation.SkidBoost");
            public const string SkidBoost_Path = "Animation.SkidBoost";

            public static readonly RailGrind_ RailGrind = new();

            public class RailGrind_
            {
                public const string Path = "Animation.RailGrind";
                public static implicit operator Tag(RailGrind_ _) => new("Animation.RailGrind");

                /// <summary>Timing For Rebound Kick-Off / End</summary>
                public static readonly Tag Rebound = new("Animation.RailGrind.Rebound");
                public const string Rebound_Path = "Animation.RailGrind.Rebound";

                /// <summary>When the trick is considered over to allow for reinput</summary>
                public static readonly Tag TrickEnd = new("Animation.RailGrind.TrickEnd");
                public const string TrickEnd_Path = "Animation.RailGrind.TrickEnd";

            }

        }

    }
}
