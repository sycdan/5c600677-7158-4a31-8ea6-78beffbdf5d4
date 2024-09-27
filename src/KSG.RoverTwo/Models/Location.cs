using MathNet.Numerics;

namespace KSG.RoverTwo.Models;

public class Location()
{
	public required double X { get; set; }
	public required double Y { get; set; }

	public override string ToString()
	{
		return $"{X},{Y}";
	}

	public static Location From((double x, double y) coordinates)
	{
		return new Location { X = coordinates.x, Y = coordinates.y };
	}

	/// <summary>
	/// Calculates the Manhattan distance between this location and another location.
	/// </summary>
	/// <param name="other">The other location.</param>
	/// <returns>The distance between the two locations, as a double.</returns>
	public double ManhattanDistanceTo(Location other)
	{
		ArgumentNullException.ThrowIfNull(nameof(other));
		double[] vectorA = [X, Y];
		double[] vectorB = [other.X, other.Y];
		return Distance.Manhattan(vectorA, vectorB);
	}
}
