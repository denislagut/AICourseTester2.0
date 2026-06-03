using AICourseTester.Models.Analysis;

public class AnalysisStrategyRegistry
{
	private readonly Dictionary<string, ITaskAnalysisStrategy> _strategies;

	public AnalysisStrategyRegistry(IEnumerable<ITaskAnalysisStrategy> strategies)
	{
		_strategies = strategies.ToDictionary(
			s => s.TaskType,
			StringComparer.OrdinalIgnoreCase);
	}

	public ITaskAnalysisStrategy GetStrategy(string taskType)
	{
		if (!_strategies.TryGetValue(taskType, out var strategy))
		{
			throw new NotSupportedException(
				$"Тип задания '{taskType}' не поддерживается.");
		}

		return strategy;
	}
}