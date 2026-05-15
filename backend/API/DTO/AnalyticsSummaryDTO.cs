namespace AICourseTester.DTO
{
	public class AnalyticsSummaryDTO
	{
		public int TotalStudents { get; set; }
		public int TotalGroups { get; set; }
		public int TotalErrors { get; set; }
		public int TotalKnowledgeGaps { get; set; }
		public double AverageGapScore { get; set; }
		public int HighSeverityErrorsCount { get; set; }
	}
}