namespace AICourseTester.DTO
{
	public class KnowledgeAspectEditDTO
	{
		public string Name { get; set; } = null!;
		public string? Description { get; set; }
		public string? TopicName { get; set; }
		public bool IsActive { get; set; } = true;
	}
}
