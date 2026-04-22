namespace AICourseTester.Models
{
	public class ErrorType
	{
		public int Id { get; set; }

		public string Code { get; set; } = null!;
		public string Name { get; set; } = null!;
		public string? Description { get; set; }

		public double DefaultSeverity { get; set; }

		public List<ErrorRecord> Errors { get; set; } = new();
		public List<ErrorTypeAspect> ErrorTypeAspects { get; set; } = new();
	}
}