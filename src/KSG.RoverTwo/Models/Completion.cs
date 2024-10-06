namespace KSG.RoverTwo.Models;

/// <summary>
/// The outcome of a worker completing a task at a job.
/// </summary>
public class Completion
{
	/// <summary>
	/// The worker who completed the task.
	/// </summary>
	public required Worker Worker { get; set; }

	/// <summary>
	/// The task that was completed.
	/// </summary>
	public required Task Task { get; set; }

	/// <summary>
	/// The place at which the task was located.
	/// </summary>
	public required Job Job { get; set; }

	/// <summary>
	/// How long did the worker spend on the task.
	/// </summary>
	public long WorkSeconds { get; set; }

	/// <summary>
	/// What rewards did the worker earn for completing the task.
	/// </summary>
	public required Dictionary<Metric, double> EarnedRewards { get; set; }

	public override string ToString()
	{
		return $"{Worker} $ {WorkSeconds} ? {Task} @ {Job}";
	}
}
