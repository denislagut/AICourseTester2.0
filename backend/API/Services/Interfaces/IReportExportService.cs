namespace AICourseTester.Services.Interfaces
{
	public interface IReportExportService
	{
		Task<byte[]?> ExportPdfAsync(int reportId);
		Task<byte[]?> ExportExcelAsync(int reportId);
	}
}
