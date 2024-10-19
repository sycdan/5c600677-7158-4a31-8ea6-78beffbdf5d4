using KSG.RoverTwo.Interfaces;

namespace KSG.RoverTwo.Models;

public class Worker : IAmUnique
{
	public required string Id { get; init; }

	/// <summary>
	/// Optional display name.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// At which place should the worker begin their route?
	/// </summary>
	public required string StartHubId { get; set; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Hub? StartHub { get; set; }

	/// <summary>
	/// At which place should the worker end their route?
	/// </summary>
	public required string EndHubId { get; set; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Hub? EndHub { get; set; }

	/// <summary>
	/// How early can the worker start their route?
	/// </summary>
	public DateTimeOffset? EarliestStartTime { get; set; }

	/// <summary>
	/// How late can the worker end their route?
	/// </summary>
	public DateTimeOffset? LatestEndTime { get; set; }

	/// <summary>
	/// How fast does this worker travel vs the default?
	/// Bigger number means faster:
	/// 0.5 ==  50% (half speed)
	///   1 == 100%
	/// 1.5 == 150% of the average.
	/// </summary>
	public double TravelSpeedFactor { get; set; } = 1;

	/// <summary>
	/// How effective is this worker at the given tool?
	/// If a tool is not listed, the worker can't use it.
	/// </summary>
	public required List<Capability> Capabilities { get; set; }

	/// <summary>
	/// Populated during validation.
	/// </summary>
	internal Dictionary<Tool, Capability> CapabilitiesByTool { get; private init; } = [];

	public override string ToString()
	{
		if (string.IsNullOrWhiteSpace(Name))
		{
			return $"{nameof(Worker)}:{Id}";
		}
		return Name;
	}
}
