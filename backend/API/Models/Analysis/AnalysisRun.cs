using AICourseTester.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace AICourseTester.Models.Analysis
{
	public class AnalysisRun
	{
		public int Id { get; set; }

		public int TaskTypeId { get; set; }
		public TaskType TaskTypeRef { get; set; } = null!;

		[NotMapped]
		public string TaskType
		{
			get => TaskTypeRef?.Code ?? string.Empty;
			set => TaskTypeId = LookupIds.TaskTypeId(value);
		}

		public int? AlphaBetaId { get; set; }
		public int? FifteenPuzzleId { get; set; }

		public string UserId { get; set; } = string.Empty;
		public ApplicationUser User { get; set; } = null!;

		public DateTime StartedAt { get; set; }
		public DateTime? CompletedAt { get; set; }

		public int StatusId { get; set; }
		public AnalysisStatus StatusRef { get; set; } = null!;

		[NotMapped]
		public string Status
		{
			get => StatusRef?.Code ?? string.Empty;
			set => StatusId = LookupIds.AnalysisStatusId(value);
		}

		public string AnalyzerVersion { get; set; } = "1.0";
		public string? ErrorMessage { get; set; }

		public List<ErrorRecord> ErrorRecords { get; set; } = new();
	}
}
