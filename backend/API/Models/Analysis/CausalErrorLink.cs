namespace AICourseTester.Models.Analysis
{
	public class CausalErrorLink
	{
		public int Id { get; set; }

		// Причина
		public int SourceErrorId { get; set; }
		public ErrorRecord SourceError { get; set; } = null!;

		// Следствие
		public int TargetErrorId { get; set; }
		public ErrorRecord TargetError { get; set; } = null!;

		// Тип связи
		public string RelationType { get; set; } = "CAUSES";

		// Насколько сильное влияние
		public double Weight { get; set; } = 1.0;
	}
}