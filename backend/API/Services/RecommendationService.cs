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

		public async Task<List<RecommendationDTO>?> GetStudentRecommendationsAsync(string userId, AnalyticsFilterDTO? filters = null)
		{
			var studentExists = await _context.Users
				.AsNoTracking()
				.AnyAsync(u => u.Id == userId);

			if (!studentExists)
			{
				return null;
			}

			var gaps = await LoadKnowledgeGapsForUsersAsync(new[] { userId });
			gaps = await ApplyRecommendationFiltersAsync(gaps, filters);
			var aspects = await LoadKnowledgeAspectsAsync(gaps);

			var recommendations = gaps
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

			if (!HasActiveFilters(filters))
			{
				await SaveStudentRecommendationsAsync(userId, recommendations);
			}

			return recommendations;
		}

		public async Task<List<RecommendationDTO>?> GetGroupRecommendationsAsync(int groupId, AnalyticsFilterDTO? filters = null)
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
				if (!HasActiveFilters(filters))
				{
					await SaveGroupRecommendationsAsync(groupId, new List<RecommendationDTO>());
				}

				return new List<RecommendationDTO>();
			}

			var gaps = await LoadKnowledgeGapsForUsersAsync(userIds);
			gaps = await ApplyRecommendationFiltersAsync(gaps, filters);
			var aspects = await LoadKnowledgeAspectsAsync(gaps);

			var recommendations = gaps
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

			if (!HasActiveFilters(filters))
			{
				await SaveGroupRecommendationsAsync(groupId, recommendations);
			}

			return recommendations;
		}

		public async Task<List<GeneratedRecommendationDTO>?> GetStudentRecommendationHistoryAsync(string userId)
		{
			var studentExists = await _context.Users
				.AsNoTracking()
				.AnyAsync(u => u.Id == userId);

			if (!studentExists)
			{
				return null;
			}

			return await _context.GeneratedRecommendations
				.AsNoTracking()
				.Where(r => r.TargetType == "Student" && r.UserId == userId)
				.OrderByDescending(r => r.CreatedAt)
				.Select(r => new GeneratedRecommendationDTO
				{
					Id = r.Id,
					TargetType = r.TargetType,
					UserId = r.UserId,
					GroupId = r.GroupId,
					KnowledgeAspectId = r.KnowledgeAspectId,
					Title = r.Title,
					Description = r.Description,
					Priority = r.Priority,
					TopicName = r.TopicName,
					GapScore = r.GapScore,
					RelatedErrorCount = r.RelatedErrorCount,
					AffectedStudentsCount = r.AffectedStudentsCount,
					CreatedAt = r.CreatedAt
				})
				.ToListAsync();
		}

		public async Task<List<GeneratedRecommendationDTO>?> GetGroupRecommendationHistoryAsync(int groupId)
		{
			var groupExists = await _context.Groups
				.AsNoTracking()
				.AnyAsync(g => g.Id == groupId);

			if (!groupExists)
			{
				return null;
			}

			return await _context.GeneratedRecommendations
				.AsNoTracking()
				.Where(r => r.TargetType == "Group" && r.GroupId == groupId)
				.OrderByDescending(r => r.CreatedAt)
				.Select(r => new GeneratedRecommendationDTO
				{
					Id = r.Id,
					TargetType = r.TargetType,
					UserId = r.UserId,
					GroupId = r.GroupId,
					KnowledgeAspectId = r.KnowledgeAspectId,
					Title = r.Title,
					Description = r.Description,
					Priority = r.Priority,
					TopicName = r.TopicName,
					GapScore = r.GapScore,
					RelatedErrorCount = r.RelatedErrorCount,
					AffectedStudentsCount = r.AffectedStudentsCount,
					CreatedAt = r.CreatedAt
				})
				.ToListAsync();
		}

		private async Task SaveStudentRecommendationsAsync(string userId, IReadOnlyCollection<RecommendationDTO> recommendations)
		{
			await _context.GeneratedRecommendations
				.Where(r => r.TargetType == "Student" && r.UserId == userId)
				.ExecuteDeleteAsync();

			await SaveGeneratedRecommendationsAsync(
				recommendations,
				userId: userId,
				groupId: null);
		}

		private async Task SaveGroupRecommendationsAsync(int groupId, IReadOnlyCollection<RecommendationDTO> recommendations)
		{
			await _context.GeneratedRecommendations
				.Where(r => r.TargetType == "Group" && r.GroupId == groupId)
				.ExecuteDeleteAsync();

			await SaveGeneratedRecommendationsAsync(
				recommendations,
				userId: null,
				groupId: groupId);
		}

		private async Task SaveGeneratedRecommendationsAsync(
			IReadOnlyCollection<RecommendationDTO> recommendations,
			string? userId,
			int? groupId)
		{
			if (recommendations.Count == 0)
			{
				await _context.SaveChangesAsync();
				return;
			}

			var createdAt = DateTime.UtcNow;

			var entities = recommendations.Select(r => new GeneratedRecommendation
			{
				TargetType = r.TargetType,
				UserId = userId,
				GroupId = groupId,
				KnowledgeAspectId = r.KnowledgeAspectId,
				Title = r.Title,
				Description = r.Description,
				Priority = r.Priority,
				TopicName = r.TopicName,
				GapScore = r.GapScore,
				RelatedErrorCount = r.RelatedErrorCount,
				AffectedStudentsCount = r.AffectedStudentsCount,
				CreatedAt = createdAt
			}).ToList();

			await _context.GeneratedRecommendations.AddRangeAsync(entities);
			await _context.SaveChangesAsync();
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

		private async Task<List<KnowledgeGap>> ApplyRecommendationFiltersAsync(
			List<KnowledgeGap> gaps,
			AnalyticsFilterDTO? filters)
		{
			var excludedAspectIds = await BuildExcludedKnowledgeAspectIdsAsync(filters);

			if (excludedAspectIds.Count == 0)
			{
				return gaps;
			}

			return gaps
				.Where(gap => !excludedAspectIds.Contains(gap.KnowledgeAspectId))
				.ToList();
		}

		private async Task<HashSet<int>> BuildExcludedKnowledgeAspectIdsAsync(AnalyticsFilterDTO? filters)
		{
			var excludedAspectIds = NormalizeIds(filters?.ExcludedKnowledgeAspectIds);
			var excludedErrorTypeIds = NormalizeIds(filters?.ExcludedErrorTypeIds);

			if (excludedErrorTypeIds.Count == 0)
			{
				return excludedAspectIds;
			}

			var linkedAspectIds = await _context.ErrorTypeAspects
				.AsNoTracking()
				.Where(link => excludedErrorTypeIds.Contains(link.ErrorTypeId))
				.Select(link => link.KnowledgeAspectId)
				.ToListAsync();

			foreach (var aspectId in linkedAspectIds)
			{
				excludedAspectIds.Add(aspectId);
			}

			return excludedAspectIds;
		}

		private static bool HasActiveFilters(AnalyticsFilterDTO? filters)
		{
			return NormalizeIds(filters?.ExcludedErrorTypeIds).Count > 0 ||
				NormalizeIds(filters?.ExcludedKnowledgeAspectIds).Count > 0;
		}

		private static HashSet<int> NormalizeIds(IEnumerable<int>? ids)
		{
			return ids?
				.Where(id => id > 0)
				.ToHashSet() ?? new HashSet<int>();
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
