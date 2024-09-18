using System.Text.Json.Serialization;

namespace KSG.RoverTwo.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationErrorType
{
	Invalid,
	Missing,
	Empty,
	MissingOrEmpty,
	NotUnique,
	LessThanZero,
	LessThanOrEqualToZero,
	Zero,
}
