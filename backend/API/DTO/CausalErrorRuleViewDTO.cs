namespace AICourseTester.DTO
{
	public class CausalErrorRuleViewDTO
	{
		public int Id { get; set; }
		public int TaskTypeId { get; set; }
		public string TaskTypeCode { get; set; } = null!;
		public string TaskTypeName { get; set; } = null!;
		public int SourceErrorTypeId { get; set; }
		public string SourceErrorTypeCode { get; set; } = null!;
		public string SourceErrorTypeName { get; set; } = null!;
		public int TargetErrorTypeId { get; set; }
		public string TargetErrorTypeCode { get; set; } = null!;
		public string TargetErrorTypeName { get; set; } = null!;
		public int RelationTypeId { get; set; }
		public string RelationTypeCode { get; set; } = null!;
		public string RelationTypeName { get; set; } = null!;
		public double Weight { get; set; }
		public bool SameNodeRequired { get; set; }
		public bool SameRootBranchRequired { get; set; }
		public bool IsActive { get; set; }
	}
}
