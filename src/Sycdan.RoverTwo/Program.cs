using System.Text.Json;
using CommandLine;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Sycdan.RoverTwo.Models;

namespace Sycdan.RoverTwo;

public class Program
{
	public class Options
	{
		[Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
		public bool Verbose { get; set; }
	}

	public static void Main(string[] args)
	{
		Startup();

		// Parse CLI args
		Log.Debug("Args: {Args}", JsonSerializer.Serialize(args));
		Parser
			.Default.ParseArguments<Options>(args)
			.WithParsed<Options>(o =>
			{
				if (o.Verbose)
				{
					Console.WriteLine($"Verbose output enabled. Current Arguments: -v {o.Verbose}");
				}
				else
				{
					Console.WriteLine($"Current Arguments: -v {o.Verbose}");
				}
			});

		if (args.Length == 0)
		{
			Log.Error("No data file provided");
			return;
		}

		string filePath = args[0];
		// todo check argc
		int frameDelay = int.Parse(args[1]);

		try
		{
			var request = LoadData(filePath);
			ValidateRequest(request);
			var solver = new Solver(request);
			solver.Render(frameDelay);
		}
		finally
		{
			Log.CloseAndFlush();
		}
	}

	private static void Startup()
	{
		ConfigureSerilog();
	}

	private static void ConfigureSerilog()
	{
		if (!Enum.TryParse<LogEventLevel>(Environment.GetEnvironmentVariable("LOG_LEVEL"), true, out var logLevel))
		{
			logLevel = LogEventLevel.Information;
		}
		var levelSwitch = new LoggingLevelSwitch(logLevel);
		Log.Logger = new LoggerConfiguration().MinimumLevel.ControlledBy(levelSwitch).WriteTo.Console().CreateLogger();
	}

	private static Request LoadData(string filePath)
	{
		try
		{
			Log.Debug("Loading data from: {FilePath}", filePath);
			string jsonString = File.ReadAllText(filePath);
			var request = JsonSerializer.Deserialize<Request>(jsonString, JsonSerializerOptionsProvider.Default);
			if (null == request)
			{
				throw new ArgumentException("Request is null");
			}
			return request;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error reading or deserializing file: {FilePath}", filePath);
			throw;
		}
	}

	private static void ValidateRequest(Request request)
	{
		Log.Information("Validating request data");

		if (null == request.Workers || request.Workers.Count <= 0)
		{
			throw new ArgumentException("At least one worker is required");
		}

		if (null == request.Hubs || request.Jobs.Count <= 0)
		{
			throw new ArgumentException("At least one job is required");
		}

		if (null == request.Jobs || request.Jobs.Count <= 0)
		{
			throw new ArgumentException("At least one job is required");
		}

		if (null == request.Tools || request.Tools.Count <= 0)
		{
			throw new ArgumentException("At least one tool is required");
		}

		var workerIds = new HashSet<string>();
		foreach (var worker in request.Workers)
		{
			if (string.IsNullOrWhiteSpace(worker.Id))
			{
				throw new ArgumentException("Worker ID is blank");
			}
			if (!workerIds.Add(worker.Id))
			{
				throw new ArgumentException($"Duplicate worker ID found: {worker.Id}");
			}

			if (string.IsNullOrWhiteSpace(worker.HubId))
			{
				throw new ArgumentException($"Worker {worker.Id} has no Hub ID");
			}
		}

		var toolIds = new HashSet<string>();
		foreach (var tool in request.Tools)
		{
			if (string.IsNullOrWhiteSpace(tool.Id))
			{
				throw new ArgumentException("Tool ID is blank");
			}
			if (!toolIds.Add(tool.Id))
			{
				throw new ArgumentException($"Duplicate tool ID found: {tool.Id}");
			}
		}

		foreach (var job in request.Jobs)
		{
			if (string.IsNullOrWhiteSpace(job.Id))
			{
				throw new ArgumentException($"Job ID is blank");
			}

			if (job.ArrivalWindow.Open >= job.ArrivalWindow.Close)
			{
				throw new ArgumentException($"Arrival window close is before open for Job: {job.Id}");
			}
			if (job.ArrivalWindow.Open < request.TZero)
			{
				throw new ArgumentException($"Arrival window opens before TZero for Job: {job.Id}");
			}

			Log.Debug("{job} @ {location}", job, job.Location.ToString());

			foreach (var task in job.Tasks)
			{
				if (string.IsNullOrWhiteSpace(task.ToolId))
				{
					throw new ArgumentException($"Task in Job {job.Id} has no Tool ID");
				}
				if (string.IsNullOrWhiteSpace(task.Name))
				{
					throw new ArgumentException($"Task in Job {job.Id} has no Name");
				}
				if (task.Value <= 0)
				{
					throw new ArgumentException($"Task value must be greater than 0 for Job {job.Id}");
				}
			}
		}

		Log.Debug("ValueUnit: {ValueUnit}", request.ValueUnit);
		Log.Debug("TZero: {TZero}", request.TZero);
	}
}
