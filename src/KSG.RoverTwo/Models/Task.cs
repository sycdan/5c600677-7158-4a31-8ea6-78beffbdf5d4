using KSG.RoverTwo.Interfaces;

namespace KSG.RoverTwo.Models;

public class Task : IAmUnique
{
	public string Id { get; init; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Where does this task sit in the sequence of tasks at the job.
	/// Set during validation.
	/// </summary>
	internal int Order { get; set; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Place? Place { get; set; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	public Tool? Tool { get; set; }

	/// <summary>
	/// Which tool is required for this task?
	/// </summary>
	public required string ToolId { get; set; }

	/// <summary>
	/// Optional display name.
	/// </summary>
	public string? Name { get; set; }

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
		return Name ?? $"{nameof(Task)}:{Id}";
	}
}
