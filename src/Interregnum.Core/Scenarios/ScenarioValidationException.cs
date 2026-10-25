namespace Interregnum.Core.Scenarios;

public sealed class ScenarioValidationException : Exception
{
    public ScenarioValidationException(IEnumerable<string> errors, Exception? innerException = null)
        : this(errors.ToArray(), innerException)
    {
    }

    private ScenarioValidationException(string[] errors, Exception? innerException)
        : base(CreateMessage(errors), innerException)
    {
        Errors = Array.AsReadOnly(errors);
    }

    public IReadOnlyList<string> Errors { get; }

    private static string CreateMessage(IReadOnlyList<string> errors) =>
        errors.Count == 0
            ? "Scenario validation failed."
            : $"Scenario validation failed:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}";
}
