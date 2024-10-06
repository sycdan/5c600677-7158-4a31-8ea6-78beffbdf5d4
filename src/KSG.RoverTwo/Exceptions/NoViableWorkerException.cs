using KSG.RoverTwo.Models;

namespace KSG.RoverTwo.Exceptions;

public class NoViableWorkerException(Job job) : ApplicationException($"No viable workers found for {job}.") { }
