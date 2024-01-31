namespace Sycdan.RoverTwo.Models;

public class Worker
{
	public required string Id { get; set; }
	private string? _name;
	public string Name
	{
		get => _name ?? Id;
		set => _name = value;
	}
	public required string HubId { get; set; }
	public required List<Capability> Capabilities { get; set; }

	public override string ToString()
	{
		return Name;
	}
}
