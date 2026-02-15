using FS.TagSystem.Editor;
using Sirenix.OdinInspector.Editor.Validation;

[assembly: RegisterValidator(typeof(TagValidator))]
[assembly: RegisterValidator(typeof(TagSetValidator))]
[assembly: RegisterValidator(typeof(TagStackValidator))]

namespace FS.TagSystem.Editor
{
    /// <summary>Validates that a single <see cref="Tag"/> field references a defined tag.</summary>
    public class TagValidator : ValueValidator<Tag>
    {
        protected override void Validate(ValidationResult result)
        {
            var tag = ValueEntry.SmartValue;
            if (!tag.IsValid) return;
        
            if (!TagTreeProvider.TryGetNode(tag.Value, out _))
            {
                result.ResultType = ValidationResultType.Warning;
                result.Message = $"Tag \"{tag.Value}\" not found in any .gameplayTags definition file.";
            }
        }
    }

    /// <summary>Validates that all tags in a <see cref="TagSet"/> reference defined tags.</summary>
    public class TagSetValidator : ValueValidator<TagSet>
    {
        protected override void Validate(ValidationResult result)
        {
            var tagSet = ValueEntry.SmartValue;
            foreach (var tag in tagSet.Tags)
            {
                if (!tag.IsValid) continue;

                if (!TagTreeProvider.TryGetNode(tag.Value, out _))
                {
                    result.ResultType = ValidationResultType.Warning;
                    result.Message = $"Tag \"{tag.Value}\" not found in any .gameplayTags definition file.";
                }
            }
        }
    }

    /// <summary>Validates that all tags in a <see cref="TagStack"/> reference defined tags.</summary>
    public class TagStackValidator : ValueValidator<TagStack>
    {
        protected override void Validate(ValidationResult result)
        {
            var tagStack = ValueEntry.SmartValue;
            foreach (var tag in tagStack.Tags)
            {
                if (!tag.IsValid) continue;

                if (!TagTreeProvider.TryGetNode(tag.Value, out _))
                {
                    result.ResultType = ValidationResultType.Warning;
                    result.Message = $"Tag \"{tag.Value}\" not found in any .gameplayTags definition file.";
                }
            }
        }
    }
}
