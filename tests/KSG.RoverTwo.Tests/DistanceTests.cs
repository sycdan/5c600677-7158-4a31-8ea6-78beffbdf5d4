using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Helpers;
using KSG.RoverTwo.Models;

namespace KSG.RoverTwo.Tests;

public class LocationTests
{
	[Theory]
	[InlineData(0, 0, 1, 1, 2)]
	[InlineData(42.89, -74.58, 30.27, -97.74, 35.78)]
	public void ManhattanDistanceTo_Works(double x1, double y1, double x2, double y2, double expected)
	{
		var location = new Location() { X = x1, Y = y1 };
		var other = new Location() { X = x2, Y = y2 };
		var distance = Math.Round(location.ManhattanDistanceTo(other), 2);
		Assert.Equal(expected, distance);
	}

	[Theory]
	[InlineData(10, DistanceUnit.Foot, 3.048)]
	[InlineData(10, DistanceUnit.Metre, 10)]
	[InlineData(10, DistanceUnit.Ell, 11.43)]
	[InlineData(10, DistanceUnit.Fathom, 18.288)]
	[InlineData(10, DistanceUnit.Peninkulma, 60_000)]
	[InlineData(10, DistanceUnit.Rast, 100_000)]
	[InlineData(10, DistanceUnit.Degree, 1_110_000)]
	public void ConvertDistance_Works(double amount, DistanceUnit distanceUnit, double expectedMeters)
	{
		var meters = ConvertDistance.ToMeters(amount, distanceUnit);
		Assert.Equal(expectedMeters, meters);
		var distance = ConvertDistance.FromMeters(meters, distanceUnit);
		Assert.Equal(distance, amount);
	}
}
