using AICourseTester.Models.Analysis;

public interface ITaskAnalysisStrategy
{
	string TaskType { get; }

	Task<ITaskAnalysisResult> AnalyzeAsync(
		int taskId,
		string userId,
		CancellationToken cancellationToken = default);
}