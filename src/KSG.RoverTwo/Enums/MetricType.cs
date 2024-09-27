using System.Text.Json.Serialization;

namespace KSG.RoverTwo.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MetricType
{
	/// <summary>
	/// Will be accrued via a combination of visit costs and task rewards.
	/// </summary>
	Custom,

	/// <summary>
	/// Tracks the distance between a and b, independent of vehicle.
	/// </summary>
	Distance,

	/// <summary>
	/// Tracks the travel time from a to b, based on vehicle speed.
	/// </summary>
	TravelTime,

	/// <summary>
	/// Tracks a worker's time spent at a, modified by capabilities.
	/// </summary>
	WorkTime,
}
