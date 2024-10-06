using CommandLine;
using KSG.RoverTwo.Exceptions;
using KSG.RoverTwo.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace KSG.RoverTwo;

public class Program
{
	public class Options
	{
		[Value(0, Required = false, HelpText = "Problem request as JSON.")]
		public string Json { get; set; } = "";

		[Option('f', "file", HelpText = "Path to file containing problem request data (overrides Json).")]
		public string File { get; set; } = "";

		[Option('p', "pretty", HelpText = "Use indenting when printing the JSON response.", Default = false)]
		public bool Pretty { get; set; }

		public bool IsValid => !string.IsNullOrWhiteSpace(Json) || !string.IsNullOrWhiteSpace(File);
	}

	public static void Main(string[] args)
	{
		Startup();
		try
		{
			Parser
				.Default.ParseArguments<Options>(args)
				.WithParsed(options =>
				{
					Log.Debug("Args: {Args}", JsonConvert.SerializeObject(options));
					if (!options.IsValid)
					{
						Console.WriteLine("Please provide either a JSON string or a file path.");
						Environment.Exit(1);
					}
					var problem = LoadData(options.Json, options.File);
					var solver = new Solver(problem);
					var solution = solver.Solve();
					RenderSolution(solution, options.Pretty);
				});
		}
		catch (ValidationError ex)
		{
			Log.Error(ex.Message);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "unhandled exception");
		}
		finally
		{
			Log.CloseAndFlush();
		}
	}

	private static void RenderSolution(Solution solution, bool pretty)
	{
		var settings = new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver(),
			Formatting = pretty ? Formatting.Indented : Formatting.None,
		};
		Console.WriteLine("<Solution>");
		Console.WriteLine(JsonConvert.SerializeObject(solution.BuildResponse(), settings));
		Console.WriteLine("</Solution>");
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

	private static Problem LoadData(string json, string filePath)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(filePath))
			{
				Log.Information("loading data from: {FilePath}", filePath);
				json = File.ReadAllText(filePath);
			}
			return Problem.FromJson(json);
		}
		catch (JsonException ex)
		{
			Log.Error(ex, "error reading or deserializing file: {FilePath}", filePath);
			throw new ValidationError(ex.Message);
		}
	}
}
