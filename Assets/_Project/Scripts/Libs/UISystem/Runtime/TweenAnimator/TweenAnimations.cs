using System;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace FS.UI
{
    [Serializable]
    public abstract class TweenAnimation
    {
        // Timing 
        
        [Range(0, 1), HideInInspector] public float StartTime = 0f;
        [Range(0, 1), HideInInspector] public float EndTime = 1f;

        // Easing
        
        public Ease EaseMode = Ease.Linear;
        public bool CustomEase => EaseMode == Ease.Custom;
        [ShowIf("CustomEase")] public AnimationCurve CustomEaseCurve = AnimationCurve.Linear(0, 0, 1, 1);

        // Cycles
        [Range(1, 4)] public int Cycles = 1;
        public bool HasCycles => Cycles != 1;
        [ShowIf("HasCycles")] public CycleMode CycleMode = CycleMode.Restart;
        
        public float StartDelay(float duration) => StartTime * duration;
        public float Duration(float totalDuration) => (EndTime - StartTime) * totalDuration;
        
        public abstract Tween GetTween(float duration, bool reverse = false);
        
        public TweenSettings<T> Settings<T>(T Start, T End, float totalDuration, bool reverse = false) where T : struct
        {
            var settings = new TweenSettings<T>(Start, End, Duration(totalDuration), EaseMode, Cycles, CycleMode,
                StartDelay(totalDuration));
            if (CustomEase) settings.settings.customEase = CustomEaseCurve;
            if (reverse && !CustomEase) settings = settings.WithDirection(false);
            if (reverse && CustomEase) settings = settings.WithDirection(false, End);
            return settings;
        }
    }
    
    [Serializable]
    public abstract class TweenAnimation<TType, TValue> : TweenAnimation where TType : UnityEngine.Object
    {
        [Required] public TType Target;
        
        public TValue StartValue;
        public TValue EndValue;
    }
    
    #region World Position
    
    [Serializable]
    public class TweenPosition : TweenAnimation<Transform, Vector3>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.Position(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenPositionX : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.PositionX(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenPositionY : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.PositionY(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    
    [Serializable]
    public class TweenPositionZ : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.PositionZ(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    #endregion
    
    #region Local Position
    [Serializable]
    public class TweenLocalPosition : TweenAnimation
    {
        public Transform Target;
        
        public Vector3 StartValue;
        public Vector3 EndValue;
        
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.LocalPosition(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenLocalPositionX : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.LocalPositionX(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenLocalPositionY : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.LocalPositionY(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenLocalPositionZ : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.LocalPositionZ(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    #endregion
    
    #region Rotation
    [Serializable]
    public class TweenRotation : TweenAnimation<Transform, Vector3>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.Rotation(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    #endregion
    
    #region Local Rotation
    [Serializable]
    public class TweenLocalRotation : TweenAnimation<Transform, Vector3>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.LocalRotation(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    #endregion
    
    #region World Scale
    [Serializable]
    public class TweenScale : TweenAnimation<Transform, Vector3>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.Scale(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenScaleX : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.ScaleX(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenScaleY : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.ScaleY(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenScaleZ : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.ScaleZ(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    #endregion
    
    #region Local Scale
    [Serializable]
    public class TweenLocalScale : TweenAnimation<Transform, Vector3>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.LocalScale(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenLocalScaleX : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.LocalScaleX(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenLocalScaleY : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.LocalScaleY(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenLocalScaleZ : TweenAnimation<Transform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.LocalScaleZ(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    #endregion
    
    #region RigidBody
    
    [Serializable]
    public class TweenRigidbodyPositionX : TweenAnimation<Rigidbody, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            if (!Application.isPlaying)
                return Tween.PositionX(Target.transform, Settings(StartValue, EndValue, duration, reverse));

            var startPos = new Vector3(StartValue, Target.position.y, Target.position.z);
            var endPos = new Vector3(EndValue, Target.position.y, Target.position.z);
            return Tween.RigidbodyMovePosition(Target, Settings(startPos, endPos, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenRigidbodyPositionY : TweenAnimation<Rigidbody, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            if (!Application.isPlaying)
                return Tween.PositionY(Target.transform, Settings(StartValue, EndValue, duration, reverse));

            var startPos = new Vector3(Target.position.x, StartValue, Target.position.z);
            var endPos = new Vector3(Target.position.x, EndValue, Target.position.z);
            return Tween.RigidbodyMovePosition(Target, Settings(startPos, endPos, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenRigidbodyPositionZ : TweenAnimation<Rigidbody, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            if (!Application.isPlaying)
                return Tween.PositionZ(Target.transform, Settings(StartValue, EndValue, duration, reverse));

            var startPos = new Vector3(Target.position.x, Target.position.y, StartValue);
            var endPos = new Vector3(Target.position.x, Target.position.y, EndValue);
            return Tween.RigidbodyMovePosition(Target, Settings(startPos, endPos, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenRigidbodyPosition : TweenAnimation<Rigidbody, Vector3>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            if (!Application.isPlaying)
                return Tween.Position(Target.transform, Settings(StartValue, EndValue, duration, reverse));

            return Tween.RigidbodyMovePosition(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenRigidbodyLocalPosition : TweenAnimation<Rigidbody, Vector3>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            if (!Application.isPlaying)
                return Tween.LocalPosition(Target.transform, Settings(StartValue, EndValue, duration, reverse));

            var startPos = Target.transform.parent.InverseTransformPoint(StartValue);
            var endPos = Target.transform.parent.InverseTransformPoint(EndValue);
            return Tween.RigidbodyMovePosition(Target, Settings(startPos, endPos, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenRigidbodyLocalPositionX : TweenAnimation<Rigidbody, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            if (!Application.isPlaying)
                return Tween.LocalPositionX(Target.transform, Settings(StartValue, EndValue, duration, reverse));
            
            var space = Target.transform.parent == null ? Target.transform : Target.transform.parent;

            var startPos = Target.transform.localPosition;
            startPos.x = StartValue;
            var endPos = Target.transform.localPosition;
            endPos.x = EndValue;
            
            startPos = space.TransformPoint(startPos);
            endPos = space.TransformPoint(endPos);
            
            return Tween.RigidbodyMovePosition(Target, Settings(startPos, endPos, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenRigidbodyLocalPositionY : TweenAnimation<Rigidbody, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            if (!Application.isPlaying)
                return Tween.LocalPositionY(Target.transform, Settings(StartValue, EndValue, duration, reverse));

            var space = Target.transform.parent == null ? Target.transform : Target.transform.parent;

            var startPos = Target.transform.localPosition;
            startPos.y = StartValue;
            var endPos = Target.transform.localPosition;
            endPos.y = EndValue;
            
            startPos = space.TransformPoint(startPos);
            endPos = space.TransformPoint(endPos);
            
            return Tween.RigidbodyMovePosition(Target, Settings(startPos, endPos, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenRigidbodyLocalPositionZ : TweenAnimation<Rigidbody, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            if (!Application.isPlaying)
                return Tween.LocalPositionZ(Target.transform, Settings(StartValue, EndValue, duration, reverse));

            var space = Target.transform.parent == null ? Target.transform : Target.transform.parent;

            var startPos = Target.transform.localPosition;
            startPos.z = StartValue;
            var endPos = Target.transform.localPosition;
            endPos.z = EndValue;
            
            startPos = space.TransformPoint(startPos);
            endPos = space.TransformPoint(endPos);

            return Tween.RigidbodyMovePosition(Target, Settings(startPos, endPos, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenRigidbodyRotation : TweenAnimation<Rigidbody, Vector3>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            if (!Application.isPlaying)
                return Tween.Rotation(Target.transform, Settings(StartValue, EndValue, duration, reverse));

            return Tween.RigidbodyMoveRotation(Target, Settings(Quaternion.Euler(StartValue), Quaternion.Euler(EndValue), duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenRigidbodyLocalRotation : TweenAnimation<Rigidbody, Vector3>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            if (!Application.isPlaying)
                return Tween.LocalRotation(Target.transform, Settings(StartValue, EndValue, duration, reverse));

            var startRot = Target.transform.parent ? Target.transform.parent.rotation * Quaternion.Euler(StartValue) : Quaternion.Euler(StartValue);
            var endRot = Target.transform.parent ? Target.transform.parent.rotation * Quaternion.Euler(EndValue) : Quaternion.Euler(EndValue);
            return Tween.RigidbodyMoveRotation(Target, Settings(startRot, endRot, duration, reverse));
        }
    }
    
    #endregion
    
    #region UI
    
    [Serializable]
    public class TweenUISliderValue : TweenAnimation<Slider, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UISliderValue(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUINormalizedPosition : TweenAnimation<ScrollRect, Vector2>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UINormalizedPosition(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIHorizontalNormalizedPosition : TweenAnimation<ScrollRect, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIHorizontalNormalizedPosition(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIVerticalNormalizedPosition : TweenAnimation<ScrollRect, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIVerticalNormalizedPosition(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIPivot : TweenAnimation<RectTransform, Vector2>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIPivot(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIPivotX : TweenAnimation<RectTransform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIPivotX(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIPivotY : TweenAnimation<RectTransform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIPivotY(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIAnchoredPosition : TweenAnimation<RectTransform, Vector2>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIAnchoredPosition(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIAnchoredPositionX : TweenAnimation<RectTransform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIAnchoredPositionX(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIAnchoredPositionY : TweenAnimation<RectTransform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIAnchoredPositionY(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }

    [Serializable]
    public class TweenUISizeDelta : TweenAnimation<RectTransform, Vector2>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UISizeDelta(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }

    [Serializable]
    public class TweenCanvasGroupAlpha : TweenAnimation<CanvasGroup, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.Alpha(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenGraphicAlpha : TweenAnimation<Graphic, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.Alpha(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenGraphicColor : TweenAnimation<Graphic, Color>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.Color(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIFillAmount : TweenAnimation<Image, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIFillAmount(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIOffsetMin : TweenAnimation<RectTransform, Vector2>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIOffsetMin(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIOffsetMinX : TweenAnimation<RectTransform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIOffsetMinX(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIOffsetMinY : TweenAnimation<RectTransform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIOffsetMinY(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIOffsetMax : TweenAnimation<RectTransform, Vector2>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIOffsetMax(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIOffsetMaxX : TweenAnimation<RectTransform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIOffsetMaxX(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenUIOffsetMaxY : TweenAnimation<RectTransform, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.UIOffsetMaxY(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    #endregion
    
    #region Sprite Renderer
    
    [Serializable]
    public class TweenSpriteRendererColor : TweenAnimation<SpriteRenderer, Color>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.Color(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenSpriteRendererAlpha : TweenAnimation<SpriteRenderer, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.Alpha(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    #endregion
    
    #region Material
    
    [Serializable]
    public class TweenMaterialColor : TweenAnimation<Material, Color>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.MaterialColor(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenMaterialAlpha : TweenAnimation<Material, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.MaterialAlpha(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenMaterialMainTextureOffset : TweenAnimation<Material, Vector2>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.MaterialMainTextureOffset(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenMaterialMainTextureScale : TweenAnimation<Material, Vector2>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.MaterialMainTextureScale(Target, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    #endregion
    
    #region Material Via Image
    
    [Serializable]
    public class TweenImageMaterialColor : TweenAnimation<Image, Color>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.MaterialColor(Target.material, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenImageMaterialAlpha : TweenAnimation<Image, float>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.MaterialAlpha(Target.material, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenImageMaterialMainTextureOffset : TweenAnimation<Image, Vector2>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.MaterialMainTextureOffset(Target.material, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    [Serializable]
    public class TweenImageMaterialMainTextureScale : TweenAnimation<Image, Vector2>
    {
        public override Tween GetTween(float duration, bool reverse = false)
        {
            return Tween.MaterialMainTextureScale(Target.material, Settings(StartValue, EndValue, duration, reverse));
        }
    }
    
    #endregion
    
}