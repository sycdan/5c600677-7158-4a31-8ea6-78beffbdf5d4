namespace KSG.RoverTwo.Models;

/// <summary>
/// An object with the solution.
/// </summary>
/// <param name="id"></param>
public abstract class Entity(int id)
{
	public int Id { get; private init; } = id;
}
