namespace KSG.RoverTwo.Models;

public class VisitCost
{
	/// <summary>
	/// Which place is this cost for.
	/// </summary>
	public required string PlaceId { get; set; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Place? Place { get; set; }

	/// <summary>
	/// Must exist in Metrics.
	/// </summary>
	public required string MetricId { get; set; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Metric? Metric { get; set; }

	/// <summary>
	/// How much is the cost.
	/// </summary>
	public required double Amount { get; set; }
}
