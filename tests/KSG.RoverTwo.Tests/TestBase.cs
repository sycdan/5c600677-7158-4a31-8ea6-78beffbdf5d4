using System.Text.Json.Nodes;
using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Models;
using Task = KSG.RoverTwo.Models.Task;

namespace KSG.RoverTwo.Tests;

public abstract class TestBase
{
	internal const string DEFAULT_PROBLEM_DATA_FILE = "vikings.json";

	public static Problem LoadProblemFromFile(string jsonFileName = DEFAULT_PROBLEM_DATA_FILE)
	{
		var json = LoadJsonDataFromFile(jsonFileName);
		var problem = Problem.FromJson(json.ToString());
		return problem;
	}

	public static JsonNode LoadJsonDataFromFile(string jsonFileName = DEFAULT_PROBLEM_DATA_FILE)
	{
		string jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../data/", jsonFileName);
		string jsonData = File.ReadAllText(jsonFilePath);
		var json = JsonNode.Parse(jsonData);
		if (null == json)
		{
			throw new ApplicationException($"No problem data found in {jsonFileName}");
		}
		return json;
	}

}
