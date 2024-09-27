namespace KSG.RoverTwo.Models;

/// <summary>
/// Ensure that a worker does or does not visit a place.
/// </summary>
public class Guarantee
{
	public required string WorkerId { get; init; }
	public required string PlaceId { get; init; }
	public bool MustVisit { get; init; } = true;

	public override string ToString() => $"{WorkerId} {(MustVisit ? "Must" : "Must not")} visit {PlaceId}";
}
