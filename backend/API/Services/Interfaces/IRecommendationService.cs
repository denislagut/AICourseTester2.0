using AICourseTester.DTO;

namespace AICourseTester.Services.Interfaces
{
	public interface IRecommendationService
	{
		Task<List<RecommendationDTO>?> GetStudentRecommendationsAsync(string userId, AnalyticsFilterDTO? filters = null);
		Task<List<RecommendationDTO>?> GetGroupRecommendationsAsync(int groupId, AnalyticsFilterDTO? filters = null);
		Task<List<GeneratedRecommendationDTO>?> GetStudentRecommendationHistoryAsync(string userId);
		Task<List<GeneratedRecommendationDTO>?> GetGroupRecommendationHistoryAsync(int groupId);
	}
}
