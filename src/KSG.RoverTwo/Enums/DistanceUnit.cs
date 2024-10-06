using System.Text.Json.Serialization;

namespace KSG.RoverTwo.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DistanceUnit
{
	/// <summary>
	/// The average length of a human foot.
	/// Equivalent to 0.3048 <see cref="Meter"/>.
	/// </summary>
	Foot,

	/// <summary>
	/// One ten-millionth of the shortest distance from the North Pole to the equator, through Paris.
	/// https://en.wikipedia.org/wiki/History_of_the_metre
	/// </summary>
	Metre,

	/// <summary>
	/// The length of a person's arm from the elbow to the fingertips.
	/// Equivalent to 1.14 <see cref="Meter"/>.
	/// </summary>
	Ell,

	/// <summary>
	/// The distance between a person's outstretched arms.
	/// Equivalent to 1.8288 <see cref="Meter"/>.
	/// </summary>
	Fathom,

	/// <summary>
	/// The distance from which a barking dog can no longer be heard.
	/// Equivalent to 6,000 <see cref="Meter"/>.
	/// https://fi.wikipedia.org/wiki/Peninkulma
	/// </summary>
	Peninkulma,

	/// <summary>
	/// The distance a traveler can comfortably walk before needing a rest.
	/// Equivalent to 10,000 <see cref="Meter"/>.
	/// </summary>
	Rast,
}
