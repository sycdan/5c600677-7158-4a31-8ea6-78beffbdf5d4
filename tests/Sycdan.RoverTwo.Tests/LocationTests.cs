using Sycdan.RoverTwo.Models;

namespace Sycdan.RoverTwo.Tests;

public class LocationTests
{
	[Theory]
	[InlineData(0, 0, 1, 1, 1.41, 2)]
	[InlineData(42.89, -74.58, 30.27, -97.74, 26.38, 2)]
	public void StraightDistanceTo_Works(double x1, double y1, double x2, double y2, double expected, int precision)
	{
		var location = new Location() { X = x1, Y = y1 };
		var other = new Location() { X = x2, Y = y2 };
		var distance = Math.Round(location.StraightDistanceTo(other), precision);
		// https://calculator.dev/math/euclidean-distance-calculator/
		Assert.Equal(expected, distance);
	}
}
