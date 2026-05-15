using AICourseTester.DTO;

namespace AICourseTester.Services.Interfaces
{
	public interface IAnalyticsService
	{
		Task<AnalyticsSummaryDTO> GetSummaryAsync();
		Task<List<TopErrorTypeDTO>> GetTopErrorTypesAsync();
		Task<List<TopKnowledgeGapDTO>> GetTopKnowledgeGapsAsync();
	}
}