namespace KSG.RoverTwo.Models;

public class Capability
{
	public required string ToolId { get; init; }

	/// <summary>
	/// How slow will this tool be used vs the average?
	/// Smaller number means faster:
	/// 0.05 ==  95% faster than average.
	/// 2.00 == 100% slower than average.
	/// </summary>
	public double DelayFactor { get; init; } = 1;

	public override string ToString()
	{
		return $"{ToolId} * {DelayFactor}";
	}
}
