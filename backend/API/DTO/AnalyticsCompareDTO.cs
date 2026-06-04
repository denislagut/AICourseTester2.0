namespace AICourseTester.DTO
{
	public class AnalyticsCompareDTO
	{
		public AnalyticsComparePeriodDTO Before { get; set; } = new();
		public AnalyticsComparePeriodDTO After { get; set; } = new();
		public AnalyticsCompareDifferenceDTO Difference { get; set; } = new();
		public string Interpretation { get; set; } = string.Empty;
	}
}
