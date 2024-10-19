namespace KSG.RoverTwo.Models;

/// <summary>
/// The result of solving a problem.
/// </summary>
public class Solution()
{
	/// <summary>
	/// Every visit made by any worker.
	/// </summary>
	public List<Visit> Visits { get; private init; } = [];

	/// <summary>
	/// Places that were not visited.
	/// </summary>
	public List<Job> SkippedJobs { get; private init; } = [];

	/// <summary>
	/// The total of each metric accrued from all visits.
	/// </summary>
	public Dictionary<Metric, double> TotalMetrics { get; private init; } = [];

	/// <summary>
	/// The total costs accrued by the solver.
	/// This is fairly arbitrary, and mostly only useful to rank multiple solutions.
	/// </summary>
	public double TotalCost { get; set; }

	/// <summary>
	/// Builds a response object containing the solution details, fit for serialization.
	/// </summary>
	/// <returns>An anonymous object.</returns>
	public object BuildResponse()
	{
		return new
		{
			Visits = Visits.Select(v => new
			{
				PlaceId = v.Place.Id,
				WorkerId = v.Worker.Id,
				v.ArrivalTime,
				v.DepartureTime,
				EarnedRewards = v.EarnedRewards.ToDictionary(x => x.Key.Id, x => x.Value),
				CompletedTasks = v.CompletedTasks.Select(t => t.Name ?? t.Id),
			}),
			SkippedJobs = SkippedJobs.Select(p => p.Id),
			TotalMetrics = TotalMetrics.ToDictionary(
				x => x.Key.Type == Enums.MetricType.Custom ? x.Key.Id : x.Key.Type.ToString(),
				x => x.Value
			),
			TotalCost,
		};
	}
}
