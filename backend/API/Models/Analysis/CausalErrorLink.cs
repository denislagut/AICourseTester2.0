using System.ComponentModel.DataAnnotations.Schema;

namespace AICourseTester.Models.Analysis
{
	public class CausalErrorLink
	{
		public int Id { get; set; }

		public int SourceErrorId { get; set; }
		public ErrorRecord SourceError { get; set; } = null!;

		public int TargetErrorId { get; set; }
		public ErrorRecord TargetError { get; set; } = null!;

		public int RelationTypeId { get; set; }
		public CausalRelationType RelationTypeRef { get; set; } = null!;

		[NotMapped]
		public string RelationType
		{
			get => RelationTypeRef?.Code ?? LookupIds.CausalRelationTypeCode(RelationTypeId);
			set => RelationTypeId = LookupIds.CausalRelationTypeId(value);
		}

		public double Weight { get; set; } = 1.0;
	}
}
