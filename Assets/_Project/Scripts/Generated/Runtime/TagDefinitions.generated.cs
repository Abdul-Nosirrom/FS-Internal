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
            private static readonly Tag _internal = new("Animation");
            public static implicit operator Tag(Animation_ _) => _internal;

            public readonly LedgeGrab_ LedgeGrab = new();

            public class LedgeGrab_
            {
                public const string Path = "Animation.LedgeGrab";
                private static readonly Tag _internal = new("Animation.LedgeGrab");
                public static implicit operator Tag(LedgeGrab_ _) => _internal;

                /// <summary>Ledge grab settled, allow input</summary>
                private static readonly Tag _internal_Settled = new("Animation.LedgeGrab.Settled");
                public Tag Settled => _internal_Settled;
                public const string Settled_Path = "Animation.LedgeGrab.Settled";

            }

            public readonly SpringKick_ SpringKick = new();

            public class SpringKick_
            {
                public const string Path = "Animation.SpringKick";
                private static readonly Tag _internal = new("Animation.SpringKick");
                public static implicit operator Tag(SpringKick_ _) => _internal;

                /// <summary>Eject from wall during spring kick</summary>
                private static readonly Tag _internal_WallEject = new("Animation.SpringKick.WallEject");
                public Tag WallEject => _internal_WallEject;
                public const string WallEject_Path = "Animation.SpringKick.WallEject";

                /// <summary>Launch off enemy bounce</summary>
                private static readonly Tag _internal_EnemyBounce = new("Animation.SpringKick.EnemyBounce");
                public Tag EnemyBounce => _internal_EnemyBounce;
                public const string EnemyBounce_Path = "Animation.SpringKick.EnemyBounce";

            }

            /// <summary>Timing for skid turn anim boosts out</summary>
            private static readonly Tag _internal_SkidBoost = new("Animation.SkidBoost");
            public Tag SkidBoost => _internal_SkidBoost;
            public const string SkidBoost_Path = "Animation.SkidBoost";

            public readonly RailGrind_ RailGrind = new();

            public class RailGrind_
            {
                public const string Path = "Animation.RailGrind";
                private static readonly Tag _internal = new("Animation.RailGrind");
                public static implicit operator Tag(RailGrind_ _) => _internal;

                /// <summary>Timing For Rebound Kick-Off / End</summary>
                private static readonly Tag _internal_Rebound = new("Animation.RailGrind.Rebound");
                public Tag Rebound => _internal_Rebound;
                public const string Rebound_Path = "Animation.RailGrind.Rebound";

                /// <summary>When the trick is considered over to allow for reinput</summary>
                private static readonly Tag _internal_TrickEnd = new("Animation.RailGrind.TrickEnd");
                public Tag TrickEnd => _internal_TrickEnd;
                public const string TrickEnd_Path = "Animation.RailGrind.TrickEnd";

            }

        }

    }
}
