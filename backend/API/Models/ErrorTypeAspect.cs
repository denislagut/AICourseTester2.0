namespace AICourseTester.Models
{
	public class ErrorTypeAspect
	{
		public int Id { get; set; }

		public int ErrorTypeId { get; set; }
		public ErrorType ErrorType { get; set; } = null!;

		public int KnowledgeAspectId { get; set; }
		public KnowledgeAspect KnowledgeAspect { get; set; } = null!;

		public double Weight { get; set; } = 1.0;
	}
}