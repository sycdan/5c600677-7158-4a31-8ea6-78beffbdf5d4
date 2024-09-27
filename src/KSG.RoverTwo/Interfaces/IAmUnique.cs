namespace KSG.RoverTwo.Interfaces;

/// <summary>
/// Applies to every entity that can be uniquely identified within a problem.
/// </summary>
public interface IAmUnique
{
	/// <summary>
	/// Unique identifier for this entity within the problem.
	/// UUID is recommended.
	/// </summary>
	string Id { get; }

	string ToString();

	int GetHashCode();

	bool Equals(object? other);
}
