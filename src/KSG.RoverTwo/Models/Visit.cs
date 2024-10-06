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
	/// </summary>
	public DateTimeOffset? ArrivalTime { get; set; }

	/// <summary>
	/// When the worker is expected to depart from the place.
	/// </summary>
	public DateTimeOffset? DepartureTime { get; set; }

	/// <summary>
	/// Which tasks were completed by the worker at the place.
	/// </summary>
	public List<Task> CompletedTasks { get; private init; } = [];

	/// <summary>
	/// All rewards earned by the worker at the place.
	/// </summary>
	public Dictionary<Metric, double> EarnedRewards { get; private init; } = [];

	/// <summary>
	/// Accrued time spent completing tasks at the place.
	/// </summary>
	internal long WorkSeconds { get; set; } = 0;

	public override string ToString()
	{
		return $"{Worker} > {Place} @ {ArrivalTime}";
	}
}
