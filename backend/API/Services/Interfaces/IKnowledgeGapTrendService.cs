using AICourseTester.Models;

namespace AICourseTester.Services.Interfaces
{
	public interface IKnowledgeGapTrendService
	{
		Task ApplyTrendAsync(
			List<KnowledgeGap> gaps,
			string userId,
			string taskType,
			int? currentAnalysisRunId);
	}
}