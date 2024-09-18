namespace KSG.RoverTwo.Models;

/// <summary>
/// The solution to a problem.
/// </summary>
public class Response()
{
	/// <summary>
	/// Every visit made by a worker.
	/// </summary>
	public List<Visit> Visits { get; private init; } = [];

	/// <summary>
	/// Total route cost divided by number of visits, per worker.
	/// </summary>
	public Dictionary<string, double> CostPerVisit { get; private init; } = [];

	/// <summary>
	/// What percentage of the available rewards were claimed, per benefit factor.
	/// </summary>
	public Dictionary<string, double> RewardRate { get; private init; } = [];

	/// <summary>
	/// PlaceIds that were not visited.
	/// </summary>
	public List<string> Skipped { get; private init; } = [];
}
