namespace AICourseTester.DTO
{
	public class AnalyticsFilterDTO
	{
		public List<int> ExcludedErrorTypeIds { get; set; } = new();
		public List<int> ExcludedKnowledgeAspectIds { get; set; } = new();
	}
}
