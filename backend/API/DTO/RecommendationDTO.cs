using System.Text.Json.Serialization;

namespace AICourseTester.DTO
{
	public class RecommendationDTO
	{
		public string Title { get; set; } = null!;
		public string Description { get; set; } = null!;
		public string TargetType { get; set; } = null!;
		public string Priority { get; set; } = null!;
		public int KnowledgeAspectId { get; set; }
		public string AspectName { get; set; } = null!;
		public string? TopicName { get; set; }
		public double GapScore { get; set; }
		public int RelatedErrorCount { get; set; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public int? AffectedStudentsCount { get; set; }
	}
}
