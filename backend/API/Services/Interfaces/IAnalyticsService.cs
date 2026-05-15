using AICourseTester.DTO;

namespace AICourseTester.Services.Interfaces
{
	public interface IAnalyticsService
	{
		Task<AnalyticsSummaryDTO> GetSummaryAsync();
		Task<List<TopErrorTypeDTO>> GetTopErrorTypesAsync();
		Task<List<TopKnowledgeGapDTO>> GetTopKnowledgeGapsAsync();
		Task<StudentAnalyticsDTO?> GetStudentAnalyticsAsync(string userId);
		Task<GroupAnalyticsDTO?> GetGroupAnalyticsAsync(int groupId);
		Task<List<AnalyticsSnapshotDTO>> GetGlobalSnapshotsAsync();
		Task<List<AnalyticsSnapshotDTO>?> GetStudentSnapshotsAsync(string userId);
		Task<List<AnalyticsSnapshotDTO>?> GetGroupSnapshotsAsync(int groupId);
	}
}
