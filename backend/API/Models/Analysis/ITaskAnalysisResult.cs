namespace AICourseTester.Models.Analysis
{
	public interface ITaskAnalysisResult
	{
		List<AnalyzedError> Errors { get; }

		int TotalErrors { get; set; }

		int NodeErrorsCount { get; set; }

		int PathErrorsCount { get; set; }

		int PruningRelatedCount { get; set; }

		bool HasMassNodeErrors { get; set; }

		bool HasPathErrors { get; set; }
	}
}