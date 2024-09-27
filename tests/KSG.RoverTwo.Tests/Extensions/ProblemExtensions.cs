using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Models;
using KSG.RoverTwo.Tests.Helpers;
using Build = KSG.RoverTwo.Tests.Helpers.Builder;
using Task = KSG.RoverTwo.Models.Task;

namespace KSG.RoverTwo.Tests.Extensions;

public static class ProblemExtensions
{
	public static Problem WithWorker(this Problem problem, Worker worker)
	{
		problem.Workers.Add(worker);
		return problem;
	}

	public static Problem WithWorkers(this Problem problem, Worker[] workers)
	{
		foreach (var worker in workers)
		{
			problem.WithWorker(worker);
		}
		return problem;
	}

	public static Problem WithPlaces(this Problem problem, Place[] places)
	{
		foreach (var place in places)
		{
			if (place.IsHub)
			{
				problem.WithHub(place);
			}
			else
			{
				problem.WithJob(place);
			}
		}
		return problem;
	}

	public static Problem WithHub(this Problem problem, Place place)
	{
		if (!place.IsHub)
		{
			throw new ArgumentException($"{nameof(place.Type)} must be {PlaceType.Hub}");
		}
		problem.Places.Add(place);
		return problem;
	}

	public static Problem WithTool(this Problem problem, Tool tool)
	{
		problem.Tools.Add(tool);
		return problem;
	}

	public static Problem WithJob(this Problem problem, Place place)
	{
		if (!place.IsJob)
		{
			throw new ArgumentException($"{nameof(place.Type)} must be {PlaceType.Job}");
		}
		problem.Places.Add(place);
		return problem;
	}

	public static Place WithTasks(this Place place, Task[] tasks)
	{
		foreach (var task in tasks)
		{
			place.Tasks.Add(task);
		}
		return place;
	}

	public static Problem WithIdleTime(this Problem problem, double maxIdletime)
	{
		problem.MaxIdleTime = maxIdletime;
		return problem;
	}

	public static Worker WithCapability(this Worker worker, Tool tool, double delayFactor = 1)
	{
		worker.Capabilities.Add(new Capability { ToolId = tool.Id, DelayFactor = delayFactor });
		return worker;
	}

	public static Task WithTool(this Task task, Tool tool)
	{
		task.Tool = tool;
		task.ToolId = tool.Id;
		return task;
	}

	public static Task WithOptional(this Task task, bool optional)
	{
		task.Optional = optional;
		return task;
	}

	/// <summary>
	/// Fill in any data gaps in the problem.
	/// </summary>
	/// <param name="problem"></param>
	/// <returns></returns>
	public static Problem Fill(this Problem problem)
	{
		// Ensure there is a hub
		var hub = problem.Places.FirstOrDefault(p => p.IsHub);
		if (hub is null)
		{
			hub = Build.Hub(name: "Home", coordinates: (0, 0));
			problem.Places.Add(hub);
		}

		// Add a worker if there are none
		if (problem.Workers.Count == 0)
		{
			var worker = Build.Worker(startPlace: hub);
			problem.Workers.Add(worker);
		}

		// Add tasks to jobs that have none
		foreach (var place in problem.Places.Where(p => p.IsJob && p.Tasks.Count == 0))
		{
			var task = Build.Task();
			place.Tasks.Add(task);
		}

		// Add tools from tasks
		foreach (var place in problem.Places)
		{
			foreach (var task in place.Tasks)
			{
				// A tool object will take precedence over a tool ID
				var tool = task.Tool ?? Build.Tool(id: task.ToolId);
				if (!problem.Tools.Any(t => t.Id == tool.Id))
				{
					problem.Tools.Add(tool);
				}
			}
		}

		// If there are no tools, add a dummy tool
		if (problem.Tools.Count == 0)
		{
			var tool = new Tool
			{
				Id = Guid.NewGuid().ToString(),
				Delay = 1,
				Name = "Tool",
			};
			problem.Tools.Add(tool);
		}

		// Populate capabilities for each worker for all tools, if they have none
		foreach (var worker in problem.Workers.Where(w => w.Capabilities.Count == 0))
		{
			foreach (var tool in problem.Tools)
			{
				worker.Capabilities.Add(new Capability { ToolId = tool.Id });
			}
		}

		// Ensure all reward metrics are present and maximized
		foreach (var place in problem.Places)
		{
			foreach (var task in place.Tasks)
			{
				foreach (var reward in task.Rewards)
				{
					if (!problem.Metrics.Any(m => m.Id == reward.MetricId))
					{
						var metric = new Metric
						{
							Id = reward.MetricId,
							Type = MetricType.Custom,
							Mode = MetricMode.Maximize,
						};
						problem.Metrics.Add(metric);
					}
				}
			}
		}

		return problem;
	}
}
