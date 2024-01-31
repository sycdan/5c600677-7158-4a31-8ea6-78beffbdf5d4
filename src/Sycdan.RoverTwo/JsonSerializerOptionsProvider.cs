using System.Text.Json;

namespace Sycdan.RoverTwo;

public static class JsonSerializerOptionsProvider
{
	public static JsonSerializerOptions Default { get; } =
		new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
