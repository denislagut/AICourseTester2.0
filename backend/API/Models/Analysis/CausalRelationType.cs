namespace AICourseTester.Models.Analysis
{
	public class CausalRelationType
	{
		public int Id { get; set; }
		public string Code { get; set; } = null!;
		public string Name { get; set; } = null!;

		public ICollection<CausalErrorLink> CausalErrorLinks { get; set; } = new List<CausalErrorLink>();
		public ICollection<CausalErrorRule> CausalErrorRules { get; set; } = new List<CausalErrorRule>();
	}
}
