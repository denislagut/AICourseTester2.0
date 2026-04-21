using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AICourseTester.Models
{
	public class ErrorRecord
	{
		public int Id { get; set; }

		[Required]
		public int AlphaBetaId { get; set; }

		public AlphaBeta AlphaBeta { get; set; } = null!;

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
	}
}