namespace AICourseTester.DTO
{
	public class StudentGroupStatisticsDTO
	{
		public string UserId { get; set; } = null!;
		public string? UserName { get; set; }
		public string? FullName { get; set; }
		public int TotalErrors { get; set; }
		public int TotalKnowledgeGaps { get; set; }
		public double AverageGapScore { get; set; }
		public int HighSeverityErrorsCount { get; set; }
		public List<TopErrorTypeDTO> TopErrorTypes { get; set; } = new();
		public List<TopKnowledgeGapDTO> TopKnowledgeGaps { get; set; } = new();
		public double CurrentAverageGapScore { get; set; }
		public double? PreviousAverageGapScore { get; set; }
		public double AverageGapScoreDelta { get; set; }
		public string TrendSummary { get; set; } = "InsufficientData";
		public LearningProgressDTO LearningProgress { get; set; } = new();
	}
}
