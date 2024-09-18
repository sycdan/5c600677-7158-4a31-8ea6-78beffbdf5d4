using KSG.RoverTwo.Enums;

namespace KSG.RoverTwo.Models;

public class Worker
{
	public required string Id { get; init; }
	private string? _name;
	public string Name
	{
		get => _name ?? Id;
		set => _name = value;
	}
	public required string StartPlaceId { get; init; }
	public required string EndPlaceId { get; init; }
	public DateTimeOffset? EarliestStartTime { get; init; }
	public DateTimeOffset? LatestEndTime { get; init; }

	/// <summary>
	/// How fast does this worker travel compared to their peers?
	/// Bigger number means faster:
	/// 0.5 ==  50% (half speed)
	///   1 == 100%
	/// 1.5 == 150% of the average.
	/// </summary>
	public double TravelSpeedFactor { get; init; } = 1;

	public required List<Capability> Capabilities { get; init; }

	/// <summary>
	/// These costs are incurred when transiting to the given place.
	/// e.g.
	/// "visitCosts": {
	/// 	"cost-factor-id": {
	/// 		"place-id": 100
	/// 	}
	/// }
	/// </summary>
	public Dictionary<string, Dictionary<string, double>> VisitCosts { get; init; } = [];

	/// <summary>
	/// How many seconds does it take this worker to use the given tool.
	/// </summary>
	/// <param name="tool"></param>
	/// <returns>zero if no capability</returns>
	public double TimeToUse(Tool tool)
	{
		var capability = Capabilities.FirstOrDefault(x => x.ToolId == tool.Id);
		if (!tool.DefaultTimeInSeconds.HasValue)
		{
			throw new ApplicationException($"call {nameof(tool.NormalizeTime)} first");
		}
		if (null != capability)
		{
			return tool.DefaultTimeInSeconds.Value * capability.DelayFactor;
		}
		return 0;
	}

	public override string ToString()
	{
		return Name;
	}
}
