using System.Text.Json.Serialization;

namespace Worker.Models.Content;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AocRunStart), nameof(AocRunStart))]
[JsonDerivedType(typeof(AocRunFinished), nameof(AocRunFinished))]
[JsonDerivedType(typeof(AocRunError), nameof(AocRunError))]
public abstract class AocSystemContent
{
}
