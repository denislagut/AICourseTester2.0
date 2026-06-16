namespace AICourseTester.Models
{
	public class KnowledgeTopic
	{
		public int Id { get; set; }
		public string Name { get; set; } = null!;

		public ICollection<KnowledgeAspect> KnowledgeAspects { get; set; } = new List<KnowledgeAspect>();
	}
}
