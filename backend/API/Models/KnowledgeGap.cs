using System.ComponentModel.DataAnnotations;

namespace AICourseTester.Models
{
	public class KnowledgeGap
	{
		public int Id { get; set; }

		[Required]
		public string UserId { get; set; } = null!;

		public ApplicationUser User { get; set; } = null!;

		[MaxLength(64)]
		public string TaskType { get; set; } = "AlphaBeta";

		public int? AlphaBetaId { get; set; }

		public AlphaBeta? AlphaBeta { get; set; }

		public int? FifteenPuzzleId { get; set; }

		public FifteenPuzzle? FifteenPuzzle { get; set; }

		public int KnowledgeAspectId { get; set; }

		public KnowledgeAspect KnowledgeAspect { get; set; } = null!;

		public int ErrorCount { get; set; }

		public double TotalWeight { get; set; }

		public double AverageSeverity { get; set; }

		public int? AnalysisRunId { get; set; }

		public double? PreviousGapScore { get; set; }

		public double? GapScoreDelta { get; set; }

		public string Trend { get; set; } = "Stable";

		public double GapScore { get; set; }

		[MaxLength(32)]
		public string Level { get; set; } = "Low";

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}