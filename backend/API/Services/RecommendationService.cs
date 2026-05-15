using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AICourseTester.Services
{
	public class RecommendationService : IRecommendationService
	{
		private readonly MainDbContext _context;

		public RecommendationService(MainDbContext context)
		{
			_context = context;
		}

		public async Task<List<RecommendationDTO>?> GetStudentRecommendationsAsync(string userId)
		{
			var studentExists = await _context.Users
				.AsNoTracking()
				.AnyAsync(u => u.Id == userId);

			if (!studentExists)
			{
				return null;
			}

			var gaps = await LoadKnowledgeGapsForUsersAsync(new[] { userId });
			var aspects = await LoadKnowledgeAspectsAsync(gaps);

			return gaps
				.Select(g => new
				{
					Gap = g,
					Aspect = GetKnowledgeAspect(g, aspects)
				})
				.Where(x => x.Aspect != null)
				.Select(x => BuildRecommendation(
					targetType: "Student",
					knowledgeAspectId: x.Gap.KnowledgeAspectId,
					aspectName: x.Aspect!.Name,
					topicName: x.Aspect.TopicName,
					gapScore: x.Gap.GapScore,
					relatedErrorCount: x.Gap.ErrorCount,
					affectedStudentsCount: null))
				.OrderByDescending(r => GetPriorityRank(r.Priority))
				.ThenByDescending(r => r.GapScore)
				.ToList();
		}

		public async Task<List<RecommendationDTO>?> GetGroupRecommendationsAsync(int groupId)
		{
			var groupExists = await _context.Groups
				.AsNoTracking()
				.AnyAsync(g => g.Id == groupId);

			if (!groupExists)
			{
				return null;
			}

			var userIds = await _context.UserGroups
				.AsNoTracking()
				.Where(ug => ug.GroupId == groupId)
				.Select(ug => ug.UserId)
				.ToListAsync();

			if (userIds.Count == 0)
			{
				return new List<RecommendationDTO>();
			}

			var gaps = await LoadKnowledgeGapsForUsersAsync(userIds);
			var aspects = await LoadKnowledgeAspectsAsync(gaps);

			return gaps
				.Select(g => new
				{
					Gap = g,
					Aspect = GetKnowledgeAspect(g, aspects)
				})
				.Where(x => x.Aspect != null)
				.GroupBy(x => new
				{
					x.Gap.KnowledgeAspectId,
					AspectName = x.Aspect!.Name,
					x.Aspect.TopicName
				})
				.Select(g => BuildRecommendation(
					targetType: "Group",
					knowledgeAspectId: g.Key.KnowledgeAspectId,
					aspectName: g.Key.AspectName,
					topicName: g.Key.TopicName,
					gapScore: Math.Round(g.Average(x => x.Gap.GapScore), 2),
					relatedErrorCount: g.Sum(x => x.Gap.ErrorCount),
					affectedStudentsCount: g.Select(x => x.Gap.UserId).Distinct().Count()))
				.OrderByDescending(r => GetPriorityRank(r.Priority))
				.ThenByDescending(r => r.GapScore)
				.ToList();
		}

		private async Task<List<KnowledgeGap>> LoadKnowledgeGapsForUsersAsync(IReadOnlyCollection<string> userIds)
		{
			var gaps = await _context.KnowledgeGaps
				.AsNoTracking()
				.Include(g => g.KnowledgeAspect)
				.Where(g => userIds.Contains(g.UserId))
				.ToListAsync();

			if (gaps.Count > 0)
			{
				return gaps;
			}

			return await _context.KnowledgeGaps
				.AsNoTracking()
				.Include(g => g.KnowledgeAspect)
				.Include(g => g.AlphaBeta)
				.Include(g => g.FifteenPuzzle)
				.Where(g =>
					(g.AlphaBeta != null && userIds.Contains(g.AlphaBeta.UserId)) ||
					(g.FifteenPuzzle != null && userIds.Contains(g.FifteenPuzzle.UserId)))
				.ToListAsync();
		}

		private async Task<Dictionary<int, KnowledgeAspect>> LoadKnowledgeAspectsAsync(IEnumerable<KnowledgeGap> gaps)
		{
			var aspectIds = gaps
				.Select(g => g.KnowledgeAspectId)
				.Distinct()
				.ToList();

			if (aspectIds.Count == 0)
			{
				return new Dictionary<int, KnowledgeAspect>();
			}

			return await _context.KnowledgeAspects
				.AsNoTracking()
				.Where(a => aspectIds.Contains(a.Id))
				.ToDictionaryAsync(a => a.Id);
		}

		private static KnowledgeAspect? GetKnowledgeAspect(
			KnowledgeGap gap,
			IReadOnlyDictionary<int, KnowledgeAspect> aspects)
		{
			if (gap.KnowledgeAspect != null)
			{
				return gap.KnowledgeAspect;
			}

			return aspects.TryGetValue(gap.KnowledgeAspectId, out var aspect)
				? aspect
				: null;
		}

		private static RecommendationDTO BuildRecommendation(
			string targetType,
			int knowledgeAspectId,
			string aspectName,
			string? topicName,
			double gapScore,
			int relatedErrorCount,
			int? affectedStudentsCount)
		{
			var priority = GetPriority(gapScore);

			return new RecommendationDTO
			{
				Title = $"Повторить тему: {aspectName}",
				Description = GetDescription(targetType, priority, aspectName, topicName),
				TargetType = targetType,
				Priority = priority,
				KnowledgeAspectId = knowledgeAspectId,
				AspectName = aspectName,
				TopicName = topicName,
				GapScore = Math.Round(gapScore, 2),
				RelatedErrorCount = relatedErrorCount,
				AffectedStudentsCount = affectedStudentsCount
			};
		}

		private static string GetPriority(double gapScore)
		{
			if (gapScore >= 80)
			{
				return "High";
			}

			if (gapScore >= 50)
			{
				return "Medium";
			}

			return "Low";
		}

		private static int GetPriorityRank(string priority)
		{
			return priority switch
			{
				"High" => 3,
				"Medium" => 2,
				_ => 1
			};
		}

		private static string GetDescription(string targetType, string priority, string aspectName, string? topicName)
		{
			var targetLabel = targetType == "Group"
				? "группы"
				: "пользователя";

			var topicLabel = string.IsNullOrWhiteSpace(topicName)
				? "соответствующей теме"
				: $"теме {topicName}";

			return priority switch
			{
				"High" => $"У {targetLabel} выявлен выраженный пробел по аспекту {aspectName}. Рекомендуется повторить материал по {topicLabel} и выполнить дополнительное задание.",
				"Medium" => $"Рекомендуется обратить внимание на аспект {aspectName} и разобрать типовые ошибки.",
				_ => $"Рекомендуется закрепить материал по аспекту {aspectName}."
			};
		}
	}
}
