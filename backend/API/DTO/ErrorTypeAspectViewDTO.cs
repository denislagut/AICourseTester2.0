namespace AICourseTester.DTO
{
	public class ErrorTypeAspectViewDTO
	{
		public int Id { get; set; }
		public int ErrorTypeId { get; set; }
		public string ErrorTypeName { get; set; } = null!;
		public string ErrorTypeCode { get; set; } = null!;
		public int KnowledgeAspectId { get; set; }
		public string KnowledgeAspectName { get; set; } = null!;
		public string? TopicName { get; set; }
		public double Weight { get; set; }
	}
}
