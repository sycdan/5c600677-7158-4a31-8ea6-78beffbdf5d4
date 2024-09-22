using System.Text.Json.Serialization;

namespace KSG.RoverTwo.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoutingEngine
{
	/// <summary>
	/// Use Manhattan distance & a time factor.
	/// </summary>
	Simple,

	/// <summary>
	/// Use point-to-point distance & duration by car using OSRM.
	/// </summary>
	Osrm,
}
