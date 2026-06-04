using AICourseTester.DTO;

namespace AICourseTester.Services.Interfaces
{
	public interface IAnalyticsService
	{
		Task<AnalyticsSummaryDTO> GetSummaryAsync();
		Task<List<TopErrorTypeDTO>> GetTopErrorTypesAsync();
		Task<List<TopKnowledgeGapDTO>> GetTopKnowledgeGapsAsync();
		Task<List<ErrorTypeReferenceDTO>> GetErrorTypesAsync();
		Task<List<KnowledgeAspectReferenceDTO>> GetKnowledgeAspectsAsync();
		Task<StudentAnalyticsDTO?> GetStudentAnalyticsAsync(string userId, AnalyticsFilterDTO? filters = null);
		Task<GroupAnalyticsDTO?> GetGroupAnalyticsAsync(int groupId, AnalyticsFilterDTO? filters = null);
		Task<List<AnalyticsSnapshotDTO>> GetGlobalSnapshotsAsync();
		Task<List<AnalyticsSnapshotDTO>?> GetStudentSnapshotsAsync(string userId);
		Task<List<AnalyticsSnapshotDTO>?> GetGroupSnapshotsAsync(int groupId);
		Task<List<AnalyticsSnapshotDTO>?> GetStudentSnapshotsForPeriodAsync(string userId, DateTime? from, DateTime? to);
		Task<List<AnalyticsSnapshotDTO>?> GetGroupSnapshotsForPeriodAsync(int groupId, DateTime? from, DateTime? to);
		Task<AnalyticsCompareDTO?> CompareStudentPeriodsAsync(string userId, DateTime? beforeFrom, DateTime? beforeTo, DateTime? afterFrom, DateTime? afterTo, AnalyticsFilterDTO? filters = null);
		Task<AnalyticsCompareDTO?> CompareGroupPeriodsAsync(int groupId, DateTime? beforeFrom, DateTime? beforeTo, DateTime? afterFrom, DateTime? afterTo, AnalyticsFilterDTO? filters = null);
		Task<List<string>?> GetStudentActivityDatesAsync(string userId);
		Task<List<string>?> GetGroupActivityDatesAsync(int groupId);
	}
}
