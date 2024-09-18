namespace KSG.RoverTwo.Models;

public class Task
{
	public required string ToolId { get; set; }
	public required string Name { get; set; }

	/// <summary>
	/// Key: costFactorId
	/// Value: reward amount
	/// </summary>
	public Dictionary<string, double> Rewards { get; init; } = [];

	public override string ToString()
	{
		return Name;
	}
}
