using System.Text.Json;
using CommandLine;
using KSG.RoverTwo.Exceptions;
using KSG.RoverTwo.Models;
using MathNet.Numerics.LinearAlgebra;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace KSG.RoverTwo;

public class Program
{
	public class Options
	{
		[Value(0, Required = true, HelpText = "Path to file containing problem data.")]
		public string File { get; set; } = "";

		[Option('p', "pretty", HelpText = "Use indenting when printing the JSON response.", Default = false)]
		public bool Pretty { get; set; }
	}

	public static void Main(string[] args)
	{
		Startup();
		try
		{
			Parser
				.Default.ParseArguments<Options>(args)
				.WithParsed<Options>(o =>
				{
					Log.Debug("Args: {Args}", JsonSerializer.Serialize(o));
					var request = LoadData(o.File);
					var solver = new Solver(request);
					var response = solver.Solve();
					// TODO save response to file
					Solver.Render(response, o.Pretty);
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
			Log.Information("loading data from: {FilePath}", filePath);
			string jsonData = File.ReadAllText(filePath);
			return Request.BuildFromJson(jsonData);
		}
		catch (JsonException ex)
		{
			Log.Error(ex, "error reading or deserializing file: {FilePath}", filePath);
			throw new ValidationError(ex.Message);
		}
	}
}
