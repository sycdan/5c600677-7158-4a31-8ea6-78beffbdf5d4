namespace Sycdan.RoverTwo.Models;

public class Place
{
	public required string Id { get; set; }
	private string? _name;
	public string Name
	{
		get => _name ?? Id;
		set => _name = value;
	}

	public override string ToString()
	{
		return Name;
	}

	public required Location Location { get; set; }
}
