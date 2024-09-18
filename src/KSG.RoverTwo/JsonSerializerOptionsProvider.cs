using System.Text.Json;

namespace KSG.RoverTwo;

public static class JsonSerializerOptionsProvider
{
	public static JsonSerializerOptions Default { get; } =
		new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
}
