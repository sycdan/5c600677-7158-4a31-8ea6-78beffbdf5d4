namespace KSG.RoverTwo.Models;

public class Node(int id, Place place, (long open, long close)? timeWindow = null) : Entity(id)
{
	public Place Place { get; private init; } = place;
	public string PlaceId => Place.Id;

	public bool IsHub => Place.IsHub;

	public bool IsJob => Place.IsJob;

	public Location? Location => Place.Location;

	public (long Open, long Close) TimeWindow { get; init; } = timeWindow ?? (0, long.MaxValue);

	public override string ToString()
	{
		return $"{Place} <{Id}>";
	}
}
