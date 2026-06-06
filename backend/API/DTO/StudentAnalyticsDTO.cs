namespace AICourseTester.DTO
{
	public class StudentAnalyticsDTO
	{
		public string UserId { get; set; } = null!;
		public string? UserName { get; set; }
		public string? FullName { get; set; }
		public int? GroupId { get; set; }
		public string? GroupName { get; set; }
		public int TotalErrors { get; set; }
		public int TotalKnowledgeGaps { get; set; }
		public double AverageGapScore { get; set; }
		public int HighSeverityErrorsCount { get; set; }
		public List<TopErrorTypeDTO> TopErrorTypes { get; set; } = new();
		public List<TopKnowledgeGapDTO> TopKnowledgeGaps { get; set; } = new();
		public LearningProgressDTO StudentProgress { get; set; } = new();
	}
}
