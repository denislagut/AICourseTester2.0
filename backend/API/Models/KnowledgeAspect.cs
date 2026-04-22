namespace AICourseTester.Models
{
	public class KnowledgeAspect
	{
		public int Id { get; set; }

		public string Name { get; set; } = null!;
		public string? Description { get; set; }
		public string? TopicName { get; set; }

		public bool IsActive { get; set; } = true;

		public List<ErrorTypeAspect> ErrorTypeAspects { get; set; } = new();
	}
}