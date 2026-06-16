namespace AICourseTester.Models.Analysis
{
	public class GapTrend
	{
		public int Id { get; set; }
		public string Code { get; set; } = null!;
		public string Name { get; set; } = null!;

		public ICollection<KnowledgeGap> KnowledgeGaps { get; set; } = new List<KnowledgeGap>();
	}
}
