namespace AICourseTester.Models.Analysis
{
	public class AnalyzedError
	{
		public string Code { get; set; } = null!;
		public string Message { get; set; } = null!;

		public int? NodeId { get; set; }
		public int? TreeLevel { get; set; }
		public string ElementType { get; set; } = null!; // Leaf, InternalNode, PathStep

		public int? ExpectedA { get; set; }
		public int? ActualA { get; set; }

		public int? ExpectedB { get; set; }
		public int? ActualB { get; set; }

		public int? PathStepIndex { get; set; }
		public int? ExpectedPathNodeId { get; set; }
		public int? ActualPathNodeId { get; set; }

		public bool IsPrimary { get; set; } = true;
		public double SeverityScore { get; set; }
		public string? GroupKey { get; set; }
		public int? RootBranchId { get; set; }
		public bool IsOnCorrectPath { get; set; }
		public bool IsUserPruned { get; set; }
		public bool IsExpectedPruned { get; set; }
	}
}