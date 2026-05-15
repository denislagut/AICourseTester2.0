namespace AICourseTester.DTO
{
	public class TopErrorTypeDTO
	{
		public int ErrorTypeId { get; set; }
		public string Code { get; set; } = null!;
		public string Name { get; set; } = null!;
		public int Count { get; set; }
		public double AverageSeverity { get; set; }
	}
}