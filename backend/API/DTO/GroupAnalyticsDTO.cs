namespace AICourseTester.DTO
{
	public class GroupAnalyticsDTO
	{
		public int GroupId { get; set; }
		public string GroupName { get; set; } = null!;
		public int StudentsCount { get; set; }
		public int TotalErrors { get; set; }
		public int TotalKnowledgeGaps { get; set; }
		public double AverageGapScore { get; set; }
		public int HighSeverityErrorsCount { get; set; }
		public List<TopErrorTypeDTO> TopErrorTypes { get; set; } = new();
		public List<TopKnowledgeGapDTO> TopKnowledgeGaps { get; set; } = new();
	}
}
