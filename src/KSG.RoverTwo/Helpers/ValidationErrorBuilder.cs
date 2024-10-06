using System.Text;
using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Exceptions;

namespace KSG.RoverTwo.Helpers;

public class ValidationErrorBuilder(string message = "Validation failed because")
{
	private const string DEFAULT_SEPARATOR = ".";
	internal string Message { get; private init; } = message;
	public List<string> Context { get; private init; } = [];

	public ValidationErrorBuilder AddContext(
		string field,
		string separator = DEFAULT_SEPARATOR,
		bool forceSeparator = false,
		bool lowerCamelCase = false
	)
	{
		var prefix = "";
		if (forceSeparator || Context.Count > 0)
		{
			prefix = separator;
		}
		if (lowerCamelCase)
		{
			field = char.ToLowerInvariant(field[0]) + field[1..];
		}
		Context.Add($"{prefix}{field}");
		return this;
	}

	public ValidationErrorBuilder AddContext(double value, string separator)
	{
		return AddContext(value.ToString(), separator, true);
	}

	public ValidationErrorBuilder AddContext(int? value, string separator)
	{
		return AddContext(value is null ? "" : value.Value.ToString(), separator, true);
	}

	public ValidationErrorBuilder AddContext(long field, string separator)
	{
		return AddContext(field.ToString(), separator, true);
	}

	public ValidationErrorBuilder AddContext(long index)
	{
		return AddContext(index.ToString(), "#", true);
	}

	public ValidationErrorBuilder PopContext(int times = 1)
	{
		for (var i = 0; i < times; i++)
		{
			if (Context.Count > 0)
			{
				Context.RemoveAt(Context.Count - 1);
			}
		}
		return this;
	}

	public ValidationError Build(ValidationErrorType validationErrorType = ValidationErrorType.Invalid)
	{
		return Build($"is {validationErrorType}");
	}

	public ValidationError Build(string reason)
	{
		var stringBuilder = new StringBuilder();
		if (!string.IsNullOrEmpty(Message))
		{
			stringBuilder.Append($"{Message} ");
		}
		foreach (var context in Context)
		{
			stringBuilder.Append(context);
		}
		stringBuilder.Append($" {reason}.");
		return new ValidationError(stringBuilder.ToString());
	}
}
