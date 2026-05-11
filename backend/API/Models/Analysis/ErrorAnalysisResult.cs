namespace AICourseTester.Models.Analysis
{
	public class ErrorAnalysisResult : ITaskAnalysisResult
	{
		public List<AnalyzedError> Errors { get; set; } = new();

		public int TotalErrors { get; set; }

		public int NodeErrorsCount { get; set; }

		public int PathErrorsCount { get; set; }

		public int PruningRelatedCount { get; set; }

		public bool HasMassNodeErrors { get; set; }

		public bool HasPathErrors { get; set; }
	}
}
