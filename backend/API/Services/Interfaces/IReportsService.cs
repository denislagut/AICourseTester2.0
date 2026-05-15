using AICourseTester.DTO;

namespace AICourseTester.Services.Interfaces
{
	public interface IReportsService
	{
		Task<GeneratedReportDTO?> GenerateStudentReportAsync(string userId);
		Task<GeneratedReportDTO?> GenerateGroupReportAsync(int groupId);
		Task<List<GeneratedReportDTO>?> GetStudentReportsHistoryAsync(string userId);
		Task<List<GeneratedReportDTO>?> GetGroupReportsHistoryAsync(int groupId);
	}
}
