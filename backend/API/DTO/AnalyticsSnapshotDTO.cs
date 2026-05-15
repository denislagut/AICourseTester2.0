namespace AICourseTester.DTO
{
	public class AnalyticsSnapshotDTO
	{
		public int Id { get; set; }
		public string ScopeType { get; set; } = null!;
		public string? UserId { get; set; }
		public int? GroupId { get; set; }
		public int TotalStudents { get; set; }
		public int TotalGroups { get; set; }
		public int TotalErrors { get; set; }
		public int TotalKnowledgeGaps { get; set; }
		public double AverageGapScore { get; set; }
		public int HighSeverityErrorsCount { get; set; }
		public string? TopErrorTypesJson { get; set; }
		public string? TopKnowledgeGapsJson { get; set; }
		public DateTime CreatedAt { get; set; }
	}
}
