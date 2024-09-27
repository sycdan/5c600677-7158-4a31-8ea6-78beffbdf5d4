namespace KSG.RoverTwo.Models;

public class Node(int id, Place place, List<Task> tasks, (long open, long close)? timeWindow = null) : Entity(id)
{
	public Place Place { get; private init; } = place;
	public string PlaceId => Place.Id;

	public bool IsHub => Place.IsHub;

	public bool IsJob => Place.IsJob;

	public Location? Location => Place.Location;

	public (long Open, long Close)? TimeWindow { get; init; } = timeWindow;

	public List<Task> Tasks { get; private init; } = tasks;

	public override string ToString()
	{
		return $"{nameof(Node)}{Id}:{Place}";
	}
}
