namespace Sycdan.RoverTwo.Models;

public class Location
{
	public required double X { get; set; }
	public required double Y { get; set; }

	public override string ToString()
	{
		return $"{X},{Y}";
	}

	/// <summary>
	/// Calculates the Euclidean distance between this location and another location.
	/// </summary>
	/// <param name="other">The other location.</param>
	/// <returns>The Euclidean distance between the two locations.</returns>
	public double StraightDistanceTo(Location other)
	{
		ArgumentNullException.ThrowIfNull(nameof(other));
		var deltaX = other.X - X;
		var deltaY = other.Y - Y;
		return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
	}
}
