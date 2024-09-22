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
	/// Must exist in Tools.
	/// </summary>
	public required string ToolId { get; init; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Tool? Tool { get; set; }

	/// <summary>
	/// How much to factor the reward.
	/// Cannot be less than 0.
	/// </summary>
	public required double Factor { get; set; }
}
