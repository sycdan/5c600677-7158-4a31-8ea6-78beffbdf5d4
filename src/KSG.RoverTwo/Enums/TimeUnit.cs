using System.Text.Json.Serialization;

namespace KSG.RoverTwo.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimeUnit
{
	/// <summary>
	/// https://en.wikipedia.org/wiki/Second
	/// </summary>
	Second,

	/// <summary>
	/// Equivalent to 60 <see cref="Second"/>.
	/// </summary>
	Minute,

	/// <summary>
	/// Equivalent to 3600 <see cref="Second"/>.
	/// </summary>
	Hour,
}
