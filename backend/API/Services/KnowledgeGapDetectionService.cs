using AICourseTester.Data;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AICourseTester.Services
{
	public class KnowledgeGapDetectionService : IKnowledgeGapDetectionService
	{
		private readonly MainDbContext _context;

		public KnowledgeGapDetectionService(MainDbContext context)
		{
			_context = context;
		}

		public async Task<List<KnowledgeGap>> DetectForAlphaBetaAsync(int alphaBetaId, string userId, int? analysisRunId)
		{
			var latestRunId = await GetLatestAnalysisRunIdAsync("AlphaBeta", alphaBetaId, null);

			var errors = await LoadErrorsAsync(
				taskType: "AlphaBeta",
				alphaBetaId: alphaBetaId,
				fifteenPuzzleId: null,
				analysisRunId: latestRunId);


			if (errors.Count == 0)
			{
				var improvedGaps = await BuildResolvedGapsAsync(
					userId,
					taskType: "AlphaBeta",
					alphaBetaId: alphaBetaId,
					fifteenPuzzleId: null,
					analysisRunId: latestRunId);

				await _context.KnowledgeGaps.AddRangeAsync(improvedGaps);
				await _context.SaveChangesAsync();

				return improvedGaps;
			}

			var gaps = BuildGapsFromErrors(
				errors,
				userId,
				taskType: "AlphaBeta",
				alphaBetaId: alphaBetaId,
				fifteenPuzzleId: null,
				analysisRunId: latestRunId);

			await ApplyGapDynamicsAsync(
				gaps,
				userId,
				taskType: "AlphaBeta",
				currentAnalysisRunId: analysisRunId);

			await _context.KnowledgeGaps.AddRangeAsync(gaps);
			await _context.SaveChangesAsync();

			return gaps;
		}

		public async Task<List<KnowledgeGap>> DetectForFifteenPuzzleAsync(int fifteenPuzzleId, string userId, int? analysisRunId)
		{
			var latestRunId = await GetLatestAnalysisRunIdAsync("FifteenPuzzle", null, fifteenPuzzleId);

			var errors = await LoadErrorsAsync(
				taskType: "FifteenPuzzle",
				alphaBetaId: null,
				fifteenPuzzleId: fifteenPuzzleId,
				analysisRunId: latestRunId);


			if (errors.Count == 0)
			{
				var improvedGaps = await BuildResolvedGapsAsync(
					userId,
					taskType: "FifteenPuzzle",
					alphaBetaId: null,
					fifteenPuzzleId: fifteenPuzzleId,
					analysisRunId: latestRunId);

				await _context.KnowledgeGaps.AddRangeAsync(improvedGaps);
				await _context.SaveChangesAsync();

				return improvedGaps;
			}

			var gaps = BuildGapsFromErrors(
				errors,
				userId,
				taskType: "FifteenPuzzle",
				alphaBetaId: null,
				fifteenPuzzleId: fifteenPuzzleId,
				analysisRunId: latestRunId);

			await ApplyGapDynamicsAsync(
				gaps,
				userId,
				taskType: "FifteenPuzzle",
				currentAnalysisRunId: analysisRunId);

			await _context.KnowledgeGaps.AddRangeAsync(gaps);
			await _context.SaveChangesAsync();

			return gaps;
		}

		private async Task<List<KnowledgeGap>> BuildResolvedGapsAsync(
			string userId,
			string taskType,
			int? alphaBetaId,
			int? fifteenPuzzleId,
			int? analysisRunId)
		{
			var previousGaps = await _context.KnowledgeGaps
				.Where(g =>
					g.UserId == userId &&
					g.TaskType == taskType &&
					g.AnalysisRunId != analysisRunId)
				.GroupBy(g => g.KnowledgeAspectId)
				.Select(g => g
					.OrderByDescending(x => x.CreatedAt)
					.First())
				.ToListAsync();

			return previousGaps
				.Where(g => g.GapScore > 0)
				.Select(previous => new KnowledgeGap
				{
					UserId = userId,
					TaskType = taskType,
					AlphaBetaId = alphaBetaId,
					FifteenPuzzleId = fifteenPuzzleId,
					KnowledgeAspectId = previous.KnowledgeAspectId,

					ErrorCount = 0,
					TotalWeight = 0,
					AverageSeverity = 0,
					GapScore = 0,
					Level = "Low",

					PreviousGapScore = previous.GapScore,
					GapScoreDelta = Math.Round(0 - previous.GapScore, 2),
					Trend = "Improved",

					AnalysisRunId = analysisRunId,
					CreatedAt = DateTime.UtcNow
				})
				.ToList();
		}

		private async Task<int?> GetLatestAnalysisRunIdAsync(
			string taskType,
			int? alphaBetaId,
			int? fifteenPuzzleId)
		{
			var query = _context.AnalysisRuns
				.Where(r => r.TaskType == taskType && r.Status == "Started");

			if (alphaBetaId.HasValue)
			{
				query = query.Where(r => r.AlphaBetaId == alphaBetaId.Value);
			}

			if (fifteenPuzzleId.HasValue)
			{
				query = query.Where(r => r.FifteenPuzzleId == fifteenPuzzleId.Value);
			}

			return await query
				.OrderByDescending(r => r.StartedAt)
				.Select(r => (int?)r.Id)
				.FirstOrDefaultAsync();
		}

		private async Task<List<ErrorRecord>> LoadErrorsAsync(
			string taskType,
			int? alphaBetaId,
			int? fifteenPuzzleId,
			int? analysisRunId)
		{
			var query = _context.ErrorRecords
				.Where(e => e.TaskType == taskType && e.ErrorTypeId != null);

			if (analysisRunId.HasValue)
			{
				query = query.Where(e => e.AnalysisRunId == analysisRunId.Value);
			}
			else
			{
				if (alphaBetaId.HasValue)
				{
					query = query.Where(e => e.AlphaBetaId == alphaBetaId.Value);
				}

				if (fifteenPuzzleId.HasValue)
				{
					query = query.Where(e => e.FifteenPuzzleId == fifteenPuzzleId.Value);
				}
			}

			return await query
				.Include(e => e.ErrorType)
					.ThenInclude(t => t!.ErrorTypeAspects)
						.ThenInclude(eta => eta.KnowledgeAspect)
				.Include(e => e.IncomingLinks)
				.Include(e => e.OutgoingLinks)
				.ToListAsync();
		}

		private List<KnowledgeGap> BuildGapsFromErrors(
			List<ErrorRecord> errors,
			string userId,
			string taskType,
			int? alphaBetaId,
			int? fifteenPuzzleId,
			int? analysisRunId)
		{

			var aspectStats = new Dictionary<int, AspectStat>();

			var effectiveErrors = errors
				.Where(ShouldAffectKnowledgeGap)
				.ToList();

			foreach (var error in effectiveErrors)
			{
				if (error.ErrorType == null)
					continue;

				foreach (var link in error.ErrorType.ErrorTypeAspects)
				{
					var aspect = link.KnowledgeAspect;

					if (aspect == null || !aspect.IsActive)
						continue;

					if (!aspectStats.ContainsKey(aspect.Id))
					{
						aspectStats[aspect.Id] = new AspectStat
						{
							KnowledgeAspectId = aspect.Id
						};
					}

					var stat = aspectStats[aspect.Id];

					var patternMultiplier = GetPatternMultiplier(error.PatternType);
					var causalityMultiplier = GetCausalityMultiplier(error);
					var confidenceMultiplier = GetConfidenceMultiplier(error);

					var effectiveSeverity =
						error.SeverityScore *
						link.Weight *
						patternMultiplier *
						causalityMultiplier *
						confidenceMultiplier;

					stat.ErrorCount += 1;
					stat.TotalWeight += link.Weight;
					stat.TotalSeverity += error.SeverityScore;
					stat.WeightedSeverity += effectiveSeverity;

					if (IsRootCause(error))
					{
						stat.RootCauseCount += 1;
					}

					if (error.PatternType == "SYSTEMATIC_MISUNDERSTANDING")
					{
						stat.SystematicCount += 1;
					}
				}
			}

			return aspectStats.Values
				.Select(stat =>
				{
					var averageSeverity = stat.ErrorCount == 0
						? 0
						: stat.TotalSeverity / stat.ErrorCount;

					var gapScore = CalculateGapScore(
						stat.ErrorCount,
						stat.TotalWeight,
						stat.WeightedSeverity,
						stat.RootCauseCount,
						stat.SystematicCount);

					return new KnowledgeGap
					{
						UserId = userId,
						TaskType = taskType,

						AlphaBetaId = alphaBetaId,
						FifteenPuzzleId = fifteenPuzzleId,

						KnowledgeAspectId = stat.KnowledgeAspectId,
						ErrorCount = stat.ErrorCount,
						TotalWeight = Math.Round(stat.TotalWeight, 2),
						AverageSeverity = Math.Round(averageSeverity, 2),
						AnalysisRunId = analysisRunId,
						GapScore = gapScore,
						Level = DetermineLevel(gapScore),
						CreatedAt = DateTime.UtcNow
					};
				})
				.Where(g => g.GapScore > 0)
				.OrderByDescending(g => g.GapScore)
				.ToList();
		}

		private async Task ApplyGapDynamicsAsync(
	List<KnowledgeGap> gaps,
	string userId,
	string taskType,
	int? currentAnalysisRunId)
		{
			foreach (var gap in gaps)
			{
				var previousGap = await _context.KnowledgeGaps
					.Where(g =>
						g.UserId == userId &&
						g.TaskType == taskType &&
						g.KnowledgeAspectId == gap.KnowledgeAspectId &&
						g.AnalysisRunId != currentAnalysisRunId)
					.OrderByDescending(g => g.CreatedAt)
					.FirstOrDefaultAsync();

				if (previousGap == null)
				{
					gap.PreviousGapScore = null;
					gap.GapScoreDelta = null;
					gap.Trend = "New";
					continue;
				}

				gap.PreviousGapScore = previousGap.GapScore;

				gap.GapScoreDelta = Math.Round(
					gap.GapScore - previousGap.GapScore,
					2);

				if (gap.GapScoreDelta <= -5)
				{
					gap.Trend = "Improved";
				}
				else if (gap.GapScoreDelta >= 5)
				{
					gap.Trend = "Worsened";
				}
				else
				{
					gap.Trend = "Stable";
				}
			}
		}

		private static bool ShouldAffectKnowledgeGap(ErrorRecord error)
		{
			if (error.ErrorType == null)
				return false;

			if (error.IsSummary)
				return false;

			if (!error.IsPrimary)
				return false;

			return true;
		}

		private static bool IsSummaryOrMetaError(string code)
		{
			return code == ErrorCodes.ValuePathInconsistency ||
				   code == ErrorCodes.ValuePruningInconsistency ||
				   code == ErrorCodes.PruningPathInconsistency ||
				   code == ErrorCodes.ValuesCorrectPruningWrong ||
				   code == ErrorCodes.ValuesAndPruningCorrectPathWrong ||
				   code == ErrorCodes.PruningCorrectResultWrongReason ||
				   code == ErrorCodes.ValueCorrectPathWrong;
		}

		private static double GetPatternMultiplier(string? patternType)
		{
			return patternType switch
			{
				"CARELESS_MISTAKE" => 0.6,
				"PARTIAL_MISUNDERSTANDING" => 1.0,
				"SYSTEMATIC_MISUNDERSTANDING" => 1.45,
				_ => 1.0
			};
		}

		private static double GetConfidenceMultiplier(ErrorRecord error)
		{
			if (error.SimilarErrorRatio <= 0)
				return 1.0;

			return Math.Clamp(0.7 + error.SimilarErrorRatio * 0.6, 0.7, 1.3);
		}

		private static double GetCausalityMultiplier(ErrorRecord error)
		{
			var hasStrongIncomingCause = error.IncomingLinks.Any(l =>
				l.RelationType == "CAUSES" ||
				l.RelationType == "EXPLAINS");

			var hasOutgoingCause = error.OutgoingLinks.Any(l =>
				l.RelationType == "CAUSES" ||
				l.RelationType == "EXPLAINS" ||
				l.RelationType == "MAY_CAUSE");

			if (hasStrongIncomingCause && !hasOutgoingCause)
			{
				return 0.65;
			}

			if (hasOutgoingCause && !hasStrongIncomingCause)
			{
				return 1.25;
			}

			if (hasOutgoingCause && hasStrongIncomingCause)
			{
				return 1.0;
			}

			return 1.0;
		}

		private static bool IsRootCause(ErrorRecord error)
		{
			var hasStrongIncomingCause = error.IncomingLinks.Any(l =>
				l.RelationType == "CAUSES" ||
				l.RelationType == "EXPLAINS");

			var hasOutgoingCause = error.OutgoingLinks.Any(l =>
				l.RelationType == "CAUSES" ||
				l.RelationType == "EXPLAINS" ||
				l.RelationType == "MAY_CAUSE");

			return !hasStrongIncomingCause && hasOutgoingCause;
		}

		private static double CalculateGapScore(
			int errorCount,
			double totalWeight,
			double weightedSeverity,
			int rootCauseCount,
			int systematicCount)
		{
			var countFactor = Math.Min(errorCount, 5) / 5.0;
			var weightFactor = Math.Min(totalWeight, 5) / 5.0;
			var severityFactor = Math.Min(weightedSeverity, 12) / 12.0;
			var rootCauseFactor = Math.Min(rootCauseCount, 3) / 3.0;
			var systematicFactor = Math.Min(systematicCount, 3) / 3.0;

			var score =
				countFactor * 0.25 +
				weightFactor * 0.20 +
				severityFactor * 0.35 +
				rootCauseFactor * 0.10 +
				systematicFactor * 0.10;

			return Math.Round(score * 100, 2);
		}

		private static string DetermineLevel(double score)
		{
			if (score >= 70)
				return "High";

			if (score >= 40)
				return "Medium";

			return "Low";
		}

		private class AspectStat
		{
			public int KnowledgeAspectId { get; set; }
			public int ErrorCount { get; set; }
			public int RootCauseCount { get; set; }
			public int SystematicCount { get; set; }
			public double TotalWeight { get; set; }
			public double TotalSeverity { get; set; }
			public double WeightedSeverity { get; set; }
		}
	}
}