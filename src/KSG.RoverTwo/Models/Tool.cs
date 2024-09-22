namespace KSG.RoverTwo.Models;

public class Tool()
{
	/// <summary>
	/// Must be unique among all tools; UUID is recommended.
	/// </summary>
	public required string Id { get; set; }
	private string? _name;
	public string Name
	{
		get => _name ?? Id;
		set => _name = value;
	}

	/// <summary>
	/// The base number of time units consumed when using this tool.
	/// </summary>
	public required double Delay { get; set; }

	/// <summary>
	/// The rate at which this tool is typically used to complete tasks.
	/// </summary>
	public double CompletionRate { get; init; } = 1.0;

	public override string ToString()
	{
		return Name;
	}
}
