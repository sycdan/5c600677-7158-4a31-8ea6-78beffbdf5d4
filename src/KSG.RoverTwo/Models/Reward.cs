namespace KSG.RoverTwo.Models;

public class Reward
{
	/// <summary>
	/// Must exist in Metrics.
	/// </summary>
	public required string MetricId { get; set; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Metric? Metric { get; set; }

	/// <summary>
	/// How much is the reward.
	/// </summary>
	public required double Amount { get; set; }

	public override string ToString()
  {
    return $"{MetricId}:{Amount}";
  }
}
