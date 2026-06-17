namespace AICourseTester.DTO
{
	public class CausalErrorRuleEditDTO
	{
		public int TaskTypeId { get; set; }
		public int SourceErrorTypeId { get; set; }
		public int TargetErrorTypeId { get; set; }
		public int RelationTypeId { get; set; }
		public double Weight { get; set; }
		public bool SameNodeRequired { get; set; }
		public bool SameRootBranchRequired { get; set; }
		public bool IsActive { get; set; } = true;
	}
}
