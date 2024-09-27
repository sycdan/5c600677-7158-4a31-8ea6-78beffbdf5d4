using KSG.RoverTwo.Interfaces;

namespace KSG.RoverTwo.Models;

public class Worker : IAmUnique
{
	public string Id { get; init; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Optional display name.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// At which place should the worker begin their route?
	/// </summary>
	public required string StartPlaceId { get; init; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Place? StartPlace { get; set; }

	/// <summary>
	/// At which place should the worker end their route?
	/// </summary>
	public required string EndPlaceId { get; init; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Place? EndPlace { get; set; }

	/// <summary>
	/// How early can the worker start their route?
	/// </summary>
	public DateTimeOffset? EarliestStartTime { get; init; }

	/// <summary>
	/// How late can the worker end their route?
	/// </summary>
	public DateTimeOffset? LatestEndTime { get; init; }

	/// <summary>
	/// How fast does this worker travel compared to their peers?
	/// Bigger number means faster:
	/// 0.5 ==  50% (half speed)
	///   1 == 100%
	/// 1.5 == 150% of the average.
	/// </summary>
	public double TravelSpeedFactor { get; init; } = 1;

	/// <summary>
	/// How effective is this worker at the given tool?
	/// If a tool is not listed, the worker can't use it.
	/// </summary>
	public required List<Capability> Capabilities { get; init; }

	/// <summary>
	/// Populated during validation.
	/// </summary>
	internal Dictionary<Tool, Capability> CapabilitiesByTool { get; private init; } = [];

	/// <summary>
	/// Costs incurred when transiting to specific locations, by metric and place.
	/// </summary>
	public List<VisitCost> VisitCosts { get; init; } = [];
	internal Dictionary<Metric, Dictionary<Place, double>> VisitCostsByMetric { get; private init; } = [];

	/// <summary>
	/// Static visit rewards and multipliers for task rewards.
	/// Any combination not specified here will use the default factor (100%).
	/// Values cannot be negative.
	/// </summary>
	public List<RewardModifier> RewardModifiers { get; init; } = [];

	public override string ToString()
	{
		if (string.IsNullOrWhiteSpace(Name))
		{
			return $"{nameof(Worker)}:{Id}";
		}
		return Name;
	}
}
