namespace KSG.RoverTwo.Models;

public class Capability
{
	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Worker? Worker { get; set; }

	/// <summary>
	/// Which tool is this capability for?
	/// </summary>
	public required string ToolId { get; init; }

	/// <summary>
	/// Set during validation.
	/// </summary>
	internal Tool? Tool { get; set; }

	/// <summary>
	/// How long does it take to use this tool, if different from the default?
	/// Set during validation if not provided.
	/// </summary>
	public double? WorkTime { get; set; }

	/// <summary>
	/// How much to factor <see cref="Tool.DefaultWorkTime"/> in the absence of a specific <see cref="WorkTime"/>.
	/// </summary>
	public double WorkTimeFactor { get; set; } = 1;

	/// <summary>
	/// How likely is this tool to be used, if different from the default?
	/// Set during validation if not provided.
	/// </summary>
	public double? CompletionChance { get; set; }

	/// <summary>
	/// Per metric, how much to multiply rewards.
	/// Any metric not specified will use the default of 1 (100%);
	/// </summary>
	public List<MetricFactor> RewardFactors { get; init; } = [];

	public override string ToString()
	{
		return $"{nameof(Capability)}:{ToolId}";
	}
}
