using System.Text.Json.Serialization;

namespace KSG.RoverTwo.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Metric
{
	/// <summary>
	/// Will pull directly from a worker's custom metrics or task rewards.
	/// </summary>
	Custom,

	/// <summary>
	/// A static measurement from a to b, independent of vehicle.
	/// </summary>
	Distance,

	/// <summary>
	/// A measure of time from a to b.
	/// </summary>
	TravelTime,

	/// <summary>
	/// Time units spent at a.
	/// </summary>
	WorkTime,
}
