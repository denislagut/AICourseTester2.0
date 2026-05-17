namespace AICourseTester.DTO
{
	public class AnalyticsComparePeriodDTO
	{
		public int TotalErrors { get; set; }
		public int TotalKnowledgeGaps { get; set; }
		public double AverageGapScore { get; set; }
		public int HighSeverityErrorsCount { get; set; }
		public DateTime CreatedAt { get; set; }
	}
}
