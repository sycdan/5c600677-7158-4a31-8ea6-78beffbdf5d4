namespace KSG.RoverTwo.Models;

public abstract class Entity(int id)
{
	public int Id { get; private init; } = id;
}
