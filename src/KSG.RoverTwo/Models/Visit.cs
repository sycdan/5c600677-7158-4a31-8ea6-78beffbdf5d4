using Newtonsoft.Json;
using Serilog;

namespace KSG.RoverTwo.Models;

public class Visit()
{
	[JsonIgnore]
	public required Worker Worker { get; set; }
	public string WorkerId => Worker.Id;

	[JsonIgnore]
	public required Place Place { get; init; }
	public string PlaceId => Place.Id;

	/// <summary>
	/// When the worker is expected to arrive at the place.
	/// Will be set after a solution is found.
	/// </summary>
	public DateTimeOffset ArrivalTime { get; set; }

	/// <summary>
	/// When the worker is expected to depart from the place.
	/// </summary>
	public DateTimeOffset DepartureTime => ArrivalTime.AddSeconds(WorkSeconds);

	/// <summary>
	/// How long did the worker spend at the place.
	/// </summary>
	[JsonIgnore]
	public long WorkSeconds { get; set; }

	/// <summary>
	/// Key: costFactorId.
	/// Value: raw value accrued from completed tasks.
	/// </summary>
	public Dictionary<string, double> Rewards { get; init; } = [];

	public void SimulateWork(Dictionary<string, Tool> toolsById)
	{
		WorkSeconds = 0;
		foreach (var task in Place.Tasks)
		{
			var tool = toolsById[task.ToolId];
			var timeSpent = (long)Math.Round(Worker.TimeToUse(tool));
			if (Random.Shared.NextDouble() < tool.CompletionRate && timeSpent > 0)
			{
				WorkSeconds += timeSpent;
				foreach (var (costFactorId, reward) in task.Rewards)
				{
					Rewards.TryAdd(costFactorId, 0);
					Rewards[costFactorId] += reward;
				}
				Log.Verbose(
					"{worker} spent {timeSpent} seconds on {task} at {job} using {tool} for {reward} rewards",
					Worker,
					timeSpent,
					task,
					Place,
					tool,
					task.Rewards
				);
			}
			else
			{
				Log.Verbose(
					"{worker} skipped {task} at {place} and missed {reward} rewards",
					Worker,
					task,
					Place,
					task.Rewards
				);
			}
		}
	}

	public override string ToString()
	{
		return $"{WorkerId} > {PlaceId} @ {ArrivalTime}";
	}
}
