using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AICourseTester.Models
{
	public class ErrorRecord
	{
		public int Id { get; set; }

		[MaxLength(64)]
		public string TaskType { get; set; } = "AlphaBeta";

		public int? FifteenPuzzleId { get; set; }

		public FifteenPuzzle? FifteenPuzzle { get; set; }

		public int? AlphaBetaId { get; set; }

		public AlphaBeta? AlphaBeta { get; set; } = null!;

		[Required]
		[MaxLength(64)]
		public string Code { get; set; } = null!;

		[Required]
		[MaxLength(512)]
		public string Message { get; set; } = null!;

		public int? NodeId { get; set; }
		public int? TreeLevel { get; set; }

		[MaxLength(64)]
		public string ElementType { get; set; } = null!;

		public int? ExpectedA { get; set; }
		public int? ActualA { get; set; }

		public int? ExpectedB { get; set; }
		public int? ActualB { get; set; }

		public int? PathStepIndex { get; set; }
		public int? ExpectedPathNodeId { get; set; }
		public int? ActualPathNodeId { get; set; }

		public bool IsPrimary { get; set; } = true;
		public double SeverityScore { get; set; }

		[MaxLength(128)]
		public string? GroupKey { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public int? ErrorTypeId { get; set; }
		public ErrorType? ErrorType { get; set; }
		public int? RootBranchId { get; set; }
		public bool IsOnCorrectPath { get; set; }
		public bool IsUserPruned { get; set; }
		public bool IsExpectedPruned { get; set; }

		[MaxLength(64)]
		public string? PatternType { get; set; }

		public int SimilarErrorCount { get; set; }

		public int SimilarOpportunityCount { get; set; }

		public double SimilarErrorRatio { get; set; }
	}
}