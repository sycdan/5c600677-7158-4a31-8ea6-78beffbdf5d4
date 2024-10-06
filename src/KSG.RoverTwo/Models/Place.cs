using KSG.RoverTwo.Interfaces;

namespace KSG.RoverTwo.Models;

public abstract class Place : IAmUnique
{
	public required string Id { get; init; }
	public string? Name { get; set; }

	/// <summary>
	/// The coordinates of the place, for distance calculations.
	/// If null, the place is considered to require no travel to reach.
	/// </summary>
	public Location? Location { get; set; } = null;

	public override string ToString()
	{
		if (string.IsNullOrWhiteSpace(Name))
		{
			return $"{GetType()}:{Id}";
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
