namespace AICourseTester.DTO
{
	public class TopKnowledgeGapDTO
	{
		public int KnowledgeAspectId { get; set; }
		public string AspectName { get; set; } = null!;
		public string? TopicName { get; set; }
		public int Count { get; set; }
		public double AverageGapScore { get; set; }
		public double MaxGapScore { get; set; }
	}
}