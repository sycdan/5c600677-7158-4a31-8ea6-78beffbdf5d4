namespace KSG.RoverTwo.Exceptions;

public class ValidationError(string message = "Validation failed") : ApplicationException(message) { }
