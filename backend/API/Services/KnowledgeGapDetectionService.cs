using AICourseTester.Data;
using AICourseTester.Models;
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

		private static double GetPatternMultiplier(string? patternType)
		{
			return patternType switch
			{
				"CARELESS_MISTAKE" => 0.7,
				"PARTIAL_MISUNDERSTANDING" => 1.0,
				"SYSTEMATIC_MISUNDERSTANDING" => 1.4,
				_ => 1.0
			};
		}

		public async Task<List<KnowledgeGap>> DetectForAlphaBetaAsync(int alphaBetaId, string userId)
		{
			var errors = await _context.ErrorRecords
				.Where(e => e.AlphaBetaId == alphaBetaId && e.ErrorTypeId != null)
				.Include(e => e.ErrorType)
					.ThenInclude(t => t!.ErrorTypeAspects)
						.ThenInclude(eta => eta.KnowledgeAspect)
				.ToListAsync();

			var oldGaps = await _context.KnowledgeGaps
				.Where(g => g.AlphaBetaId == alphaBetaId)
				.ToListAsync();

			_context.KnowledgeGaps.RemoveRange(oldGaps);

			if (errors.Count == 0)
			{
				await _context.SaveChangesAsync();
				return new List<KnowledgeGap>();
			}

			var aspectStats = new Dictionary<int, AspectStat>();

			foreach (var error in errors)
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

					stat.ErrorCount += 1;
					stat.TotalWeight += link.Weight;
					stat.TotalSeverity += error.SeverityScore;
					stat.WeightedSeverity += error.SeverityScore * link.Weight * patternMultiplier;
				}
			}

			var gaps = aspectStats.Values
				.Select(stat =>
				{
					var averageSeverity = stat.ErrorCount == 0
						? 0
						: stat.TotalSeverity / stat.ErrorCount;

					var gapScore = CalculateGapScore(
						stat.ErrorCount,
						stat.TotalWeight,
						stat.WeightedSeverity);

					return new KnowledgeGap
					{
						UserId = userId,
						AlphaBetaId = alphaBetaId,
						KnowledgeAspectId = stat.KnowledgeAspectId,
						ErrorCount = stat.ErrorCount,
						TotalWeight = stat.TotalWeight,
						AverageSeverity = averageSeverity,
						GapScore = gapScore,
						Level = DetermineLevel(gapScore),
						CreatedAt = DateTime.UtcNow
					};
				})
				.Where(g => g.GapScore > 0)
				.ToList();

			await _context.KnowledgeGaps.AddRangeAsync(gaps);
			await _context.SaveChangesAsync();

			return gaps;
		}

		private static double CalculateGapScore(
			int errorCount,
			double totalWeight,
			double weightedSeverity)
		{
			// Простая и понятная формула для диплома:
			// учитываем количество ошибок, вес связи с аспектом и серьезность.
			var countFactor = Math.Min(errorCount, 5) / 5.0;
			var weightFactor = Math.Min(totalWeight, 5) / 5.0;
			var severityFactor = Math.Min(weightedSeverity, 10) / 10.0;

			return Math.Round((countFactor * 0.35 + weightFactor * 0.25 + severityFactor * 0.40) * 100, 2);
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
			public double TotalWeight { get; set; }
			public double TotalSeverity { get; set; }
			public double WeightedSeverity { get; set; }
		}
	}
}