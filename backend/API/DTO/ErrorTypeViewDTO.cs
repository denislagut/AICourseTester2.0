namespace AICourseTester.DTO
{
	public class ErrorTypeViewDTO
	{
		public int Id { get; set; }
		public string Code { get; set; } = null!;
		public string Name { get; set; } = null!;
		public string? Description { get; set; }
		public double DefaultSeverity { get; set; }
	}
}
