namespace AICourseTester.Models.Analysis
{
	public class TaskType
	{
		public int Id { get; set; }
		public string Code { get; set; } = null!;
		public string Name { get; set; } = null!;

		public ICollection<AnalysisRun> AnalysisRuns { get; set; } = new List<AnalysisRun>();
		public ICollection<ErrorRecord> ErrorRecords { get; set; } = new List<ErrorRecord>();
		public ICollection<KnowledgeGap> KnowledgeGaps { get; set; } = new List<KnowledgeGap>();
		public ICollection<CausalErrorRule> CausalErrorRules { get; set; } = new List<CausalErrorRule>();
	}
}
