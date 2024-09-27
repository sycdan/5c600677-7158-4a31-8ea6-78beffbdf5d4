namespace KSG.RoverTwo.Models;

/// <summary>
/// The result of solving a problem.
/// </summary>
public class Solution()
{
	/// <summary>
	/// Every visit made by any worker.
	/// </summary>
	public List<Visit> Visits { get; private init; } = [];

	/// <summary>
	/// Places that were not visited.
	/// </summary>
	public List<Place> SkippedPlaces { get; private init; } = [];

	/// <summary>
	/// Builds a response object containing the solution details, fit for serialization.
	/// </summary>
	/// <returns>An anonymous object.</returns>
	public object BuildResponse()
	{
		return new
		{
			Visits = Visits.Select(v => new
			{
				WorkerId = v.Worker.Id,
				PlaceId = v.Place.Id,
				v.ArrivalTime,
				v.DepartureTime,
				EarnedRewards = v.EarnedRewards.ToDictionary(x => x.Key.Id, x => x.Value),
				CompletedTasks = v.CompletedTasks.OrderBy(t => t.Order).Select(t => t.Name ?? t.Id),
			}),
			SkippedPlaces = SkippedPlaces.Select(p => p.Id),
		};
	}
}
