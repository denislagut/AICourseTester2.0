using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AICourseTester.Models.Analysis;

namespace AICourseTester.Models
{
	public class KnowledgeGap
	{
		public int Id { get; set; }

		[Required]
		public string UserId { get; set; } = null!;
		public ApplicationUser User { get; set; } = null!;

		public int TaskTypeId { get; set; }
		public TaskType TaskTypeRef { get; set; } = null!;

		[NotMapped]
		public string TaskType
		{
			get => TaskTypeRef?.Code ?? string.Empty;
			set => TaskTypeId = LookupIds.TaskTypeId(value);
		}

		public int? AlphaBetaId { get; set; }
		public AlphaBeta? AlphaBeta { get; set; }

		public int? FifteenPuzzleId { get; set; }
		public FifteenPuzzle? FifteenPuzzle { get; set; }

		public int KnowledgeAspectId { get; set; }
		public KnowledgeAspect KnowledgeAspect { get; set; } = null!;

		public int ErrorCount { get; set; }
		public double TotalWeight { get; set; }
		public double AverageSeverity { get; set; }

		public int? AnalysisRunId { get; set; }
		public AnalysisRun? AnalysisRun { get; set; }

		public double? PreviousGapScore { get; set; }
		public double? GapScoreDelta { get; set; }

		public int TrendId { get; set; }
		public GapTrend TrendRef { get; set; } = null!;

		[NotMapped]
		public string Trend
		{
			get => TrendRef?.Code ?? LookupIds.GapTrendCode(TrendId);
			set => TrendId = LookupIds.GapTrendId(value);
		}

		public double GapScore { get; set; }

		public int LevelId { get; set; }
		public GapLevel LevelRef { get; set; } = null!;

		[NotMapped]
		public string Level
		{
			get => LevelRef?.Code ?? LookupIds.GapLevelCode(LevelId);
			set => LevelId = LookupIds.GapLevelId(value);
		}

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
