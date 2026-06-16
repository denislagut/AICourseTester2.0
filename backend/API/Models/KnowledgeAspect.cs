using System.ComponentModel.DataAnnotations.Schema;

namespace AICourseTester.Models
{
	public class KnowledgeAspect
	{
		public int Id { get; set; }

		public string Name { get; set; } = null!;
		public string? Description { get; set; }

		public int? TopicId { get; set; }
		public KnowledgeTopic? Topic { get; set; }

		[NotMapped]
		public string? TopicName
		{
			get => Topic?.Name;
			set { }
		}

		public bool IsActive { get; set; } = true;

		public List<ErrorTypeAspect> ErrorTypeAspects { get; set; } = new();
	}
}
