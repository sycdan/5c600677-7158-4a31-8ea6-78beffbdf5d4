using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Models;
using Build = KSG.RoverTwo.Tests.Helpers.ProblemBuilder;
using Task = KSG.RoverTwo.Models.Task;

namespace KSG.RoverTwo.Tests.Extensions;

public static class ProblemExtensions
{
	public static Problem WithWorker(this Problem problem, Worker worker)
	{
		return problem.WithWorkers([worker]);
	}

	public static Problem WithWorkers(this Problem problem, Worker[] workers)
	{
		problem.Workers.Clear();
		problem.Workers.AddRange(workers);
		return problem;
	}

	public static Problem WithPlaces(this Problem problem, Place[] places)
	{
		foreach (var place in places)
		{
			if (place is Hub)
			{
				problem.WithAddedHub((place as Hub)!);
			}
			else
			{
				problem.WithJob((place as Job)!);
			}
		}
		return problem;
	}

	/// <summary>
	/// Replaces all jobs in the problem with the provided ones.
	/// </summary>
	/// <param name="problem"></param>
	/// <param name="jobs"></param>
	/// <returns></returns>
	public static Problem WithJobs(this Problem problem, Job[] jobs)
	{
		problem.Jobs.Clear();
		problem.Jobs.AddRange(jobs);
		return problem;
	}

	public static Problem WithAddedHub(this Problem problem, Hub hub)
	{
		problem.Hubs.Add(hub);
		return problem;
	}

	public static Problem WithHub(this Problem problem, Hub hub)
	{
		return problem.WithHubs([hub]);
	}

	public static Problem WithHubs(this Problem problem, Hub[] hubs)
	{
		problem.Hubs.Clear();
		problem.Hubs.AddRange(hubs);
		return problem;
	}

	public static Problem WithTools(this Problem problem, Tool[] tool)
	{
		problem.Tools.Clear();
		problem.Tools.AddRange(tool);
		return problem;
	}

	public static Problem WithAddedTool(this Problem problem, Tool tool)
	{
		problem.Tools.Add(tool);
		return problem;
	}

	public static Problem WithJob(this Problem problem, Job job)
	{
		problem.Jobs.Add(job);
		return problem;
	}

	public static Problem WithIdleTime(this Problem problem, double maxIdleTime)
	{
		problem.MaxIdleTime = maxIdleTime;
		return problem;
	}

	/// <summary>
	/// Replaces all the job's tasks with the provided ones.
	/// </summary>
	/// <param name="job"></param>
	/// <param name="tasks"></param>
	/// <returns></returns>
	public static Job WithTasks(this Job job, Task[] tasks)
	{
		job.Tasks.Clear();
		job.Tasks.AddRange(tasks);
		return job;
	}

	public static Job WithTask(this Job job, Task task)
	{
		return job.WithTasks([task]);
	}

	public static Worker WithCapabilities(this Worker worker, Capability[] capabilities)
	{
		worker.Capabilities.Clear();
		worker.Capabilities.AddRange(capabilities);
		return worker;
	}

	public static Worker WithCapabilities(this Worker worker, Tool[] tools)
	{
		worker.Capabilities.Clear();
		worker.Capabilities.AddRange(tools.Select(t => Build.Capability(t)));
		return worker;
	}

	public static Worker WithAddedCapability(this Worker worker, Capability capability)
	{
		worker.Capabilities.Add(capability);
		return worker;
	}

	public static Worker WithAddedCapability(
		this Worker worker,
		Tool tool,
		double? workTime = null,
		double workTimeFactor = 1,
		double? completionChance = null,
		double rewardFactor = 1
	)
	{
		var capability = Build.Capability(tool.Id, workTime, completionChance, workTimeFactor);
		if (rewardFactor != 1)
		{
			capability.RewardFactors.Add(new MetricFactor { Factor = rewardFactor, MetricId = "reward" });
		}
		return worker.WithAddedCapability(capability);
	}

	public static Task WithTool(this Task task, Tool tool)
	{
		task.Tool = tool;
		task.ToolId = tool.Id;
		return task;
	}

	public static Task WithRewards(this Task task, List<Reward> rewards)
	{
		task.Rewards = rewards;
		return task;
	}

	public static Task WithRewards(this Task task, Dictionary<Metric, double> rewardsByMetric)
	{
		var rewards = new List<Reward>();
		foreach (var (metric, amount) in rewardsByMetric)
		{
			rewards.Add(new Reward { MetricId = metric.Id, Amount = amount });
		}
		return task.WithRewards(rewards);
	}

	public static Task WithReward(this Task task, Metric metric, double amount)
	{
		return task.WithRewards([new Reward { MetricId = metric.Id, Amount = amount }]);
	}

	/// <summary>
	/// Fill in any data gaps in the problem.
	/// </summary>
	/// <param name="problem"></param>
	/// <returns></returns>
	public static Problem Fill(this Problem problem)
	{
		// Ensure there is a job.
		if (problem.Jobs.Count == 0)
		{
			var job = Build.Job();
			problem.Jobs.Add(job);
		}

		// Ensure there is a metric.
		if (problem.Metrics.Count == 0)
		{
			problem.Metrics.Add(Build.Metric(MetricType.Distance));
		}

		// If there are workers, ensure their hubs exist.
		foreach (var worker in problem.Workers)
		{
			var startHubId = worker.StartHubId;
			var startHub = problem.Hubs.FirstOrDefault(p => p.Id == startHubId);
			if (startHub is null)
			{
				problem.Hubs.Add(Build.Hub(startHubId));
			}
			var endHubId = worker.EndHubId;
			var endHub = problem.Hubs.FirstOrDefault(p => p.Id == endHubId);
			if (endHub is null)
			{
				problem.Hubs.Add(Build.Hub(endHubId));
			}
		}

		// Ensure there is a hub.
		var hub = problem.Hubs.FirstOrDefault();
		if (hub is null)
		{
			hub = Build.Hub(name: "Hub");
			problem.Hubs.Add(hub);
		}

		// Add a worker if there are none.
		if (problem.Workers.Count == 0)
		{
			var worker = Build.Worker(startHub: hub);
			problem.Workers.Add(worker);
		}

		// Add a task to jobs that have none.
		foreach (var job in problem.Jobs.Where(j => j.Tasks.Count == 0))
		{
			var task = Build.Task(name: $"Visit {job}");
			var tool = Build.Tool($"tool-for-task-{task.Id}");
			job.Tasks.Add(task.WithTool(tool));
		}

		// Add tools from tasks.
		foreach (var place in problem.Jobs)
		{
			foreach (var task in place.Tasks)
			{
				// A tool object will take precedence over a tool ID.
				var tool = task.Tool ?? Build.Tool(task.ToolId);
				if (!problem.Tools.Any(t => t.Id == tool.Id))
				{
					problem.Tools.Add(tool);
				}
			}
		}

		// Add tools from capabilities.
		foreach (var worker in problem.Workers)
		{
			foreach (var capability in worker.Capabilities)
			{
				var toolId = capability.ToolId;
				if (!problem.Tools.Any(t => t.Id == toolId))
				{
					problem.Tools.Add(Build.Tool(toolId));
				}
			}
		}

		// If there are no tools, add a dummy tool.
		if (problem.Tools.Count == 0)
		{
			var tool = new Tool
			{
				Id = Guid.NewGuid().ToString(),
				DefaultWorkTime = 1,
				Name = "Tool",
			};
			problem.Tools.Add(tool);
		}

		// Populate capabilities for each worker for all tools, if they have none.
		foreach (var worker in problem.Workers.Where(w => w.Capabilities.Count == 0))
		{
			foreach (var tool in problem.Tools)
			{
				worker.Capabilities.Add(new Capability { ToolId = tool.Id });
			}
		}

		// Ensure all reward metrics are present and maximized.
		foreach (var place in problem.Jobs)
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
