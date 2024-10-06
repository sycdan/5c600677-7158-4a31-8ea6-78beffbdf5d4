namespace KSG.RoverTwo.Models;

public class MetricFactor
{
	/// <summary>
	/// To which metric does this factor apply?
	/// </summary>
	public required string MetricId { get; set; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Metric? Metric { get; set; }

	/// <summary>
	/// How much to factor the metric.
	/// </summary>
	public required double Factor { get; set; }

	public override string ToString()
	{
		return $"{MetricId}*{Factor}";
	}
}
