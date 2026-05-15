using AICourseTester.DTO;

namespace AICourseTester.Services.Interfaces
{
	public interface IRecommendationService
	{
		Task<List<RecommendationDTO>?> GetStudentRecommendationsAsync(string userId);
		Task<List<RecommendationDTO>?> GetGroupRecommendationsAsync(int groupId);
		Task<List<GeneratedRecommendationDTO>?> GetStudentRecommendationHistoryAsync(string userId);
		Task<List<GeneratedRecommendationDTO>?> GetGroupRecommendationHistoryAsync(int groupId);
	}
}
