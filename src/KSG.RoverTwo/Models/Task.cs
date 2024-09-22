namespace KSG.RoverTwo.Models;

public class Task
{
	public string Id { get; init; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Set during validation.
	/// </summary>
	public Tool? Tool { get; set; }
	public required string ToolId { get; set; }
	public string? Name { get; set; }

	/// <summary>
	/// All rewards that can be earned for completing this task.
	/// </summary>
	public required List<Reward> Rewards { get; set; }
	internal Dictionary<Metric, double> RewardsByMetric { get; set; } = [];

	public override string ToString()
	{
		return Name ?? $"{nameof(Task)}:{Id}";
	}
}
