using System.Text.Json.Serialization;

namespace KSG.RoverTwo.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlaceType
{
	/// <summary>
	/// A place where work is done for rewards.
	/// May be visited by only one worker.
	/// </summary>
	Job,

	/// <summary>
	/// A place where workers start/end their days.
	/// May be visited by many workers.
	/// </summary>
	Hub,
}
