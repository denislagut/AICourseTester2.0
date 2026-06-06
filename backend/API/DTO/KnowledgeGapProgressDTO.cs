namespace AICourseTester.DTO
{
	public class KnowledgeGapProgressDTO
	{
		public int KnowledgeAspectId { get; set; }
		public string AspectName { get; set; } = null!;
		public string? Topic { get; set; }
		public double? PreviousGapScore { get; set; }
		public double CurrentGapScore { get; set; }
		public double GapScoreDelta { get; set; }
		public string Trend { get; set; } = "Stable";
		public string Level { get; set; } = "Low";
	}
}
