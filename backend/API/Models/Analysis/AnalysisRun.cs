namespace AICourseTester.Models.Analysis
{
	public class AnalysisRun
	{
		public int Id { get; set; }

		public string TaskType { get; set; } = string.Empty;

		public int? AlphaBetaId { get; set; }
		public int? FifteenPuzzleId { get; set; }

		public string UserId { get; set; } = string.Empty;

		public DateTime StartedAt { get; set; }
		public DateTime? CompletedAt { get; set; }

		public string Status { get; set; } = "Started";

		public string AnalyzerVersion { get; set; } = "1.0";

		public string? ErrorMessage { get; set; }

		public List<ErrorRecord> ErrorRecords { get; set; } = new();
	}
}