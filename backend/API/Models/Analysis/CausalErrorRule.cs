namespace AICourseTester.Models.Analysis
{
	public class CausalErrorRule
	{
		public int Id { get; set; }

		public string TaskType { get; set; } = string.Empty;

		public string SourceErrorCode { get; set; } = string.Empty;

		public string TargetErrorCode { get; set; } = string.Empty;

		public string RelationType { get; set; } = string.Empty;

		public double Weight { get; set; }

		public bool SameNodeRequired { get; set; }

		public bool SameRootBranchRequired { get; set; }

		public bool IsActive { get; set; } = true;
	}
}
