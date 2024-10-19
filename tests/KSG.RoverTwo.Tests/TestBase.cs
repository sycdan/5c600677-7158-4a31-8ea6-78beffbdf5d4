using System.Text.Json.Nodes;
using KSG.RoverTwo.Models;
using KSG.RoverTwo.Tests.Extensions;
using Build = KSG.RoverTwo.Tests.Helpers.ProblemBuilder;

namespace KSG.RoverTwo.Tests;

public abstract class TestBase
{
	/// <summary>
	/// Creates a new Problem instance with default values.
	/// </summary>
	/// <returns></returns>
	public static Problem BasicProblem()
	{
		var hub = Build.Hub(name: "Hub");
		var job = Build.Job(name: "Job");
		var problem = Build.Problem().WithAddedHub(hub).WithJob(job);
		return problem;
	}
}
