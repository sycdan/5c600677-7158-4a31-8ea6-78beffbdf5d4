using System.Text;

namespace KSG.RoverTwo.Models;

/// <summary>
/// An entity that can be visited.
/// </summary>
/// <param name="id"></param>
/// <param name="place"></param>
/// <param name="tasks"></param>
/// <param name="timeWindow"></param>
/// <param name="skippable"></param>
public class Node(
	int id,
	Place place,
	List<Task> tasks,
	bool skippable = false,
	(long open, long close)? timeWindow = null
) : Entity(id)
{
	/// <summary>
	/// The hub or job this node represents.
	/// </summary>
	public Place Place { get; private init; } = place;

	/// <summary>
	/// Is the place a hub? If so, it cannot be a job.
	/// </summary>
	public bool IsHub => Place is Hub;

	/// <summary>
	/// Is the place a job? If so, it cannot be a hub.
	/// </summary>
	public bool IsJob => Place is Job;

	/// <summary>
	/// The time window in which the node can be visited, in seconds.
	/// </summary>
	public (long Open, long Close)? TimeWindow { get; init; } = timeWindow;

	/// <summary>
	/// Which tasks are available to completed at this node?
	/// </summary>
	public List<Task> Tasks { get; private init; } = tasks;

	/// <summary>
	/// Can this node be skipped by all vehicles without failing the solution?
	/// </summary>
	public bool IsSkippable { get; set; } = skippable;

	public override string ToString()
	{
		var sb = new StringBuilder();
		sb.Append($"{nameof(Node)}:{Id}:{Place}");
		if (IsJob && Tasks.Count > 0)
		{
			sb.Append(':');
			sb.Append(Tasks.First().ToString());
		}
		return sb.ToString();
	}
}
