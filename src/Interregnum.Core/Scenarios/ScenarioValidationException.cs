namespace Interregnum.Core.Scenarios;

public sealed class ScenarioValidationException(IReadOnlyList<string> errors)
    : Exception($"Scenario validation failed:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
