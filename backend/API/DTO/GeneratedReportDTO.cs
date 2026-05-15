namespace AICourseTester.DTO
{
	public class GeneratedReportDTO
	{
		public int Id { get; set; }
		public string ReportType { get; set; } = null!;
		public string? UserId { get; set; }
		public int? GroupId { get; set; }
		public string Title { get; set; } = null!;
		public string SummaryJson { get; set; } = null!;
		public string AnalyticsJson { get; set; } = null!;
		public string RecommendationsJson { get; set; } = null!;
		public string Format { get; set; } = null!;
		public string? FilePath { get; set; }
		public DateTime CreatedAt { get; set; }
	}
}
