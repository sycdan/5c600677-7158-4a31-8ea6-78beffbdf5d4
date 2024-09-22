using Serilog;

namespace KSG.RoverTwo.Models;

public class Visit()
{
	/// <summary>
	/// The worker who visited the place.
	/// </summary>
	public required Worker Worker { get; init; }

	/// <summary>
	/// The place visited by the worker.
	/// </summary>
	public required Place Place { get; init; }

	/// <summary>
	/// When the worker is arrives at the place.
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
	internal long WorkSeconds { get; set; }

	internal Dictionary<Metric, double> EarnedRewards { get; private init; } = [];

	/// <summary>
	/// Simulate the work done by the worker at the place.
	/// Any compelted task will be rewarded and cost time.
	/// For each task, if the worker has the required tool,
	/// there is a random chance to complete the task,
	/// based on its completion rate (1 == always).
	/// </summary>
	/// <param name="vehicle"></param>
	public void SimulateWork(Vehicle vehicle)
	{
		WorkSeconds = 0;
		foreach (var task in Place.Tasks)
		{
			var tool = task.Tool!;
			var toolTime = vehicle.ToolTimes[tool];
			if (Random.Shared.NextDouble() < tool.CompletionRate && toolTime > 0)
			{
				WorkSeconds += toolTime;
				foreach (var (metric, reward) in task.RewardsByMetric)
				{
					EarnedRewards.TryAdd(metric, 0);
					EarnedRewards[metric] += reward * vehicle.RewardFactor(tool, metric);
				}
				Log.Verbose(
					"{worker} spent {timeSpent} seconds on {task} at {job} using {tool} for {reward} rewards",
					Worker,
					toolTime,
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
		return $"{Worker} > {Place} @ {ArrivalTime}";
	}
}
