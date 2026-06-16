namespace AICourseTester.Models.Analysis
{
	public class AnalysisStatus
	{
		public int Id { get; set; }
		public string Code { get; set; } = null!;
		public string Name { get; set; } = null!;

		public ICollection<AnalysisRun> AnalysisRuns { get; set; } = new List<AnalysisRun>();
	}
}
