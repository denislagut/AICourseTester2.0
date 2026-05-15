using System.ComponentModel.DataAnnotations;

namespace AICourseTester.Models
{
	public class GeneratedRecommendation
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(32)]
		public string TargetType { get; set; } = null!;

		public string? UserId { get; set; }

		public ApplicationUser? User { get; set; }

		public int? GroupId { get; set; }

		public Group? Group { get; set; }

		public int? KnowledgeAspectId { get; set; }

		public KnowledgeAspect? KnowledgeAspect { get; set; }

		[Required]
		[MaxLength(256)]
		public string Title { get; set; } = null!;

		[Required]
		public string Description { get; set; } = null!;

		[Required]
		[MaxLength(32)]
		public string Priority { get; set; } = null!;

		[MaxLength(256)]
		public string? TopicName { get; set; }

		public double GapScore { get; set; }

		public int RelatedErrorCount { get; set; }

		public int? AffectedStudentsCount { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
