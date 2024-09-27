using System.Text;

namespace KSG.RoverTwo.Models;

public class RewardModifier
{
	/// <summary>
	/// Must exist in Metrics.
	/// </summary>
	public required string MetricId { get; init; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Metric? Metric { get; set; }

	/// <summary>
	/// If set, this modifier will be applied when using the tool.
	/// </summary>
	public string? ToolId { get; init; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Tool? Tool { get; set; }

	/// <summary>
	/// If set, this modifier will be applied when visiting the place.
	/// </summary>
	public string? PlaceId { get; init; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Place? Place { get; set; }

	/// <summary>
	/// A concrete amount to add to or subtract from matching rewards at a given place.
	/// Cannot reduce the reward below 0.
	/// </summary>
	public double? Amount { get; set; }

	/// <summary>
	/// How much to factor rewards of this type.
	/// Cannot be less than 0.
	/// </summary>
	public double? Factor { get; set; }

	public override string ToString()
	{
		return Newtonsoft.Json.JsonConvert.SerializeObject(this);
	}
}
