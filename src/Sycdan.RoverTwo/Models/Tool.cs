namespace Sycdan.RoverTwo.Models;

public class Tool
{
	public required string Id { get; set; }
	private string? _name;
	public string Name
	{
		get => _name ?? Id;
		set => _name = value;
	}
	public int Delay { get; set; } = 1;

	public override string ToString()
	{
		return Name;
	}
}
