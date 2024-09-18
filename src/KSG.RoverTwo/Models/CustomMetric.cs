namespace KSG.RoverTwo.Models;

public class CustomMetric
{
	public required string FactorId { get; init; }

	public required string PlaceId { get; init; }

	public required double Value { get; init; }

	public override string ToString()
	{
		return $"{FactorId} @ {PlaceId} = {Value}";
	}
}
