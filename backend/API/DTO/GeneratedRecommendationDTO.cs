namespace AICourseTester.DTO
{
	public class GeneratedRecommendationDTO
	{
		public int Id { get; set; }
		public string TargetType { get; set; } = null!;
		public string? UserId { get; set; }
		public int? GroupId { get; set; }
		public int? KnowledgeAspectId { get; set; }
		public string Title { get; set; } = null!;
		public string Description { get; set; } = null!;
		public string Priority { get; set; } = null!;
		public string? TopicName { get; set; }
		public double GapScore { get; set; }
		public int RelatedErrorCount { get; set; }
		public int? AffectedStudentsCount { get; set; }
		public DateTime CreatedAt { get; set; }
	}
}
