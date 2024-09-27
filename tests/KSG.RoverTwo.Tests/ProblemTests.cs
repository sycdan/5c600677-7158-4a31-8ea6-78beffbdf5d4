using KSG.RoverTwo.Exceptions;
using KSG.RoverTwo.Models;

namespace KSG.RoverTwo.Tests;

public class ProblemTests : TestBase
{
#pragma warning disable CS8602 // Dereference of a possibly null reference.
	[Fact]
	public void Places_DuplicateId_FailsValidation()
	{
		var json = LoadJsonDataFromFile();
		var duplicateId = json["places"][0]["id"].ToString();
		json["places"][1]["id"] = duplicateId;
		var exception = Assert.Throws<ValidationError>(() =>
		{
			Problem.FromJson(json.ToString()).Validate();
		});
		Assert.Contains($"places#1.id={duplicateId} is NotUnique", exception.Message);
	}

	[Fact]
	public void WhenDistanceMatrix_IsMissingAndRequiredByFactor_LocationsAreRequired()
	{
		var json = LoadJsonDataFromFile();
		json["distanceMatrix"] = null;
		json["places"][0]["location"] = null;
		var exception = Assert.Throws<ValidationError>(() =>
		{
			Problem.FromJson(json.ToString()).Validate();
		});
		Assert.Contains("Missing", exception.Message);
	}

	[Fact]
	public void Tools_MissingOrEmpty_FailsValidation()
	{
		var json = LoadJsonDataFromFile();
		json["tools"] = null;
		var exception = Assert.Throws<ValidationError>(() =>
		{
			Problem.FromJson(json.ToString()).Validate();
		});
		Assert.Contains("tools is MissingOrEmpty", exception.Message);
	}

	[Fact]
	public void Tools_EmptyId_FailsValidation()
	{
		var json = LoadJsonDataFromFile();
		json["tools"][0]["id"] = " ";
		var exception = Assert.Throws<ValidationError>(() =>
		{
			Problem.FromJson(json.ToString()).Validate();
		});
		Assert.Contains("tools#0.id is MissingOrEmpty", exception.Message);
	}

	[Fact]
	public void Tools_DuplicateId_FailsValidation()
	{
		var json = LoadJsonDataFromFile();
		var duplicateId = json["tools"][0]["id"].ToString();
		json["tools"][1]["id"] = duplicateId;
		var exception = Assert.Throws<ValidationError>(() =>
		{
			Problem.FromJson(json.ToString()).Validate();
		});
		Assert.Contains($"tools#1.id={duplicateId} is NotUnique", exception.Message);
	}
#pragma warning restore CS8602 // Dereference of a possibly null reference.
}
