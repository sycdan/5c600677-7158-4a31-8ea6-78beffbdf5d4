using KSG.RoverTwo.Enums;

namespace KSG.RoverTwo.Models;

public class Place()
{
	public required string Id { get; init; }
	private string? _name;
	public string Name
	{
		get => _name ?? Id;
		set => _name = value;
	}
	public PlaceType Type { get; init; } = PlaceType.Job;
	public bool IsHub => PlaceType.Hub.Equals(Type);
	public bool IsJob => PlaceType.Job.Equals(Type);
	public Location? Location { get; set; } = null;
	public Window ArrivalWindow { get; set; } = new();
	public List<Task> Tasks { get; set; } = [];

	public override string ToString()
	{
		return $"{Name} <{Id}>";
	}
}
