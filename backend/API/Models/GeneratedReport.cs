using System.ComponentModel.DataAnnotations;

namespace AICourseTester.Models
{
	public class GeneratedReport
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(32)]
		public string ReportType { get; set; } = null!;

		public string? UserId { get; set; }

		public ApplicationUser? User { get; set; }

		public int? GroupId { get; set; }

		public Group? Group { get; set; }

		[Required]
		[MaxLength(256)]
		public string Title { get; set; } = null!;

		[Required]
		public string SummaryJson { get; set; } = null!;

		[Required]
		public string RecommendationsJson { get; set; } = null!;

		[Required]
		public string AnalyticsJson { get; set; } = null!;

		[Required]
		[MaxLength(32)]
		public string Format { get; set; } = null!;

		[MaxLength(512)]
		public string? FilePath { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
