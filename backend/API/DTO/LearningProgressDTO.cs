namespace AICourseTester.DTO
{
	public class LearningProgressDTO
	{
		public double CurrentAverageGapScore { get; set; }
		public double? PreviousAverageGapScore { get; set; }
		public double AverageGapScoreDelta { get; set; }
		public int ImprovedGapsCount { get; set; }
		public int WorsenedGapsCount { get; set; }
		public int StableGapsCount { get; set; }
		public int NewGapsCount { get; set; }
		public string TrendSummary { get; set; } = "InsufficientData";
		public List<KnowledgeGapProgressDTO> GapsProgress { get; set; } = new();
	}
}
