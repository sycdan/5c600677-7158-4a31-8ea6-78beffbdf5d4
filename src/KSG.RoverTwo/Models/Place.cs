using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Interfaces;

namespace KSG.RoverTwo.Models;

public class Place : IAmUnique
{
	public string Id { get; init; } = Guid.NewGuid().ToString();
	public string? Name { get; set; }
	public PlaceType Type { get; init; } = PlaceType.Job;
	public bool IsHub => PlaceType.Hub.Equals(Type);
	public bool IsJob => PlaceType.Job.Equals(Type);
	public Location? Location { get; set; } = null;
	public Window ArrivalWindow { get; set; } = new();
	public List<Task> Tasks { get; set; } = [];

	public override string ToString()
	{
		if (string.IsNullOrWhiteSpace(Name))
		{
			return $"{nameof(Place)}:{Id}";
		}
		return Name;
	}

	public override int GetHashCode()
	{
		return Id.GetHashCode();
	}

	public override bool Equals(object? obj)
	{
		if (obj is Place other)
		{
			return Id.Equals(other.Id);
		}
		return false;
	}
}
