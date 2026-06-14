namespace AICourseTester.DTO
{
	public class KnowledgeAspectViewDTO
	{
		public int Id { get; set; }
		public string Name { get; set; } = null!;
		public string? Description { get; set; }
		public string? TopicName { get; set; }
		public bool IsActive { get; set; }
	}
}
