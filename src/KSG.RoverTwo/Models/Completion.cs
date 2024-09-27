namespace KSG.RoverTwo.Models;

/// <summary>
/// The outcome of a worker completing a task at a job.
/// </summary>
public class Completion
{
	/// <summary>
	/// The worker who completed the task.
	/// </summary>
	public required Worker Worker { get; init; }

	/// <summary>
	/// The place at which the task was located.
	/// </summary>
	public required Place Place { get; init; }

	/// <summary>
	/// The task being completed.
	/// </summary>
	public required Task Task { get; init; }

	/// <summary>
	/// How long did the worker spend on the task.
	/// </summary>
	public long WorkSeconds { get; init; }

	/// <summary>
	/// What rewards did the worker earn for completing the task.
	/// </summary>
	public required Dictionary<Metric, double> EarnedRewards { get; init; }

	public override string ToString()
	{
		return $"{Worker} $ {WorkSeconds} ? {Task} @ {Place}";
	}
}
