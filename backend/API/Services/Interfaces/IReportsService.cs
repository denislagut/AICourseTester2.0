using AICourseTester.DTO;

namespace AICourseTester.Services.Interfaces
{
	public interface IReportsService
	{
		Task<GeneratedReportDTO?> GenerateStudentReportAsync(string userId, AnalyticsFilterDTO? filters = null);
		Task<GeneratedReportDTO?> GenerateGroupReportAsync(int groupId, AnalyticsFilterDTO? filters = null);
		Task<List<GeneratedReportDTO>?> GetStudentReportsHistoryAsync(string userId);
		Task<List<GeneratedReportDTO>?> GetGroupReportsHistoryAsync(int groupId);
	}
}
