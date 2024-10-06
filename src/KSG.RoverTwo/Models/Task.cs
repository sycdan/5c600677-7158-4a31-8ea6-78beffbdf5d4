using KSG.RoverTwo.Interfaces;

namespace KSG.RoverTwo.Models;

public class Task : IAmUnique
{
	public required string Id { get; init; }

	/// <summary>
	/// Optional display name.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// The order in which the tasks should be completed.
	/// Defaults to the task's index in the list.
	/// </summary>
	public int? Order { get; set; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Job? Job { get; set; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Tool? Tool { get; set; }

	/// <summary>
	/// Which tool is required for this task?
	/// </summary>
	public required string ToolId { get; set; }

	/// <summary>
	/// Is the worker able to skip this task?
	/// </summary>
	public bool Optional { get; set; }

	/// <summary>
	/// The default rewards that may be earned by a worker for completing this task.
	/// </summary>
	public required List<Reward> Rewards { get; set; }

	/// <summary>
	/// Populated during validation.
	/// </summary>
	internal Dictionary<Metric, double> RewardsByMetric { get; set; } = [];

	public override string ToString()
	{
		if (string.IsNullOrWhiteSpace(Name))
		{
			return $"{nameof(Task)}:{Id}";
		}
		return Name;
	}
}
