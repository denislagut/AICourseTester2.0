using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AICourseTester.Services
{
	public class ReportsService : IReportsService
	{
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true
		};
		private const double HighSeverityThreshold = 3.0;
		private const int TopItemsLimit = 10;

		private readonly MainDbContext _context;
		private readonly IAnalyticsService _analyticsService;

		public ReportsService(MainDbContext context, IAnalyticsService analyticsService)
		{
			_context = context;
			_analyticsService = analyticsService;
		}

		public async Task<GeneratedReportDTO?> GenerateStudentReportAsync(string userId, AnalyticsFilterDTO? filters = null)
		{
			var userExists = await _context.Users
				.AsNoTracking()
				.AnyAsync(u => u.Id == userId);

			if (!userExists)
			{
				return null;
			}

			var snapshot = await _context.AnalyticsSnapshots
				.AsNoTracking()
				.Where(s => s.ScopeType == "Student" && s.UserId == userId)
				.OrderByDescending(s => s.CreatedAt)
				.FirstOrDefaultAsync();

			if (snapshot == null)
			{
				throw new InvalidOperationException("Сначала необходимо сформировать статистику студента");
			}

			var groupId = await _context.UserGroups
				.AsNoTracking()
				.Where(ug => ug.UserId == userId)
				.Select(ug => (int?)ug.GroupId)
				.FirstOrDefaultAsync();

			var createdAt = DateTime.UtcNow;
			var analytics = await _analyticsService.GetStudentAnalyticsAsync(userId, filters);
			if (analytics == null)
			{
				return null;
			}

			var reportSnapshot = BuildStudentSnapshot(analytics, createdAt);
			var recommendations = await LoadRecommendationsAsync("Student", userId, null, filters);

			var report = new GeneratedReport
			{
				ReportType = "Student",
				UserId = userId,
				GroupId = groupId,
				Title = "Отчет по студенту",
				SummaryJson = JsonSerializer.Serialize(BuildReportSummary("Student", "Отчет по студенту", userId, groupId, reportSnapshot, recommendations, createdAt, filters)),
				AnalyticsJson = JsonSerializer.Serialize(MapSnapshotWithProgress(reportSnapshot, analytics.StudentProgress)),
				RecommendationsJson = JsonSerializer.Serialize(recommendations),
				Format = "Json",
				FilePath = null,
				CreatedAt = createdAt
			};

			_context.GeneratedReports.Add(report);
			await _context.SaveChangesAsync();

			return MapReport(report);
		}

		public async Task<GeneratedReportDTO?> GenerateGroupReportAsync(int groupId, AnalyticsFilterDTO? filters = null)
		{
			var groupExists = await _context.Groups
				.AsNoTracking()
				.AnyAsync(g => g.Id == groupId);

			if (!groupExists)
			{
				return null;
			}

			var snapshot = await _context.AnalyticsSnapshots
				.AsNoTracking()
				.Where(s => s.ScopeType == "Group" && s.GroupId == groupId)
				.OrderByDescending(s => s.CreatedAt)
				.FirstOrDefaultAsync();

			if (snapshot == null)
			{
				throw new InvalidOperationException("Сначала необходимо сформировать статистику группы");
			}

			var createdAt = DateTime.UtcNow;
			var analytics = await _analyticsService.GetGroupAnalyticsAsync(groupId, filters);
			if (analytics == null)
			{
				return null;
			}

			var reportSnapshot = BuildGroupSnapshot(analytics, createdAt);
			var recommendations = await LoadRecommendationsAsync("Group", null, groupId, filters);

			var report = new GeneratedReport
			{
				ReportType = "Group",
				UserId = null,
				GroupId = groupId,
				Title = "Отчет по группе",
				SummaryJson = JsonSerializer.Serialize(BuildReportSummary("Group", "Отчет по группе", null, groupId, reportSnapshot, recommendations, createdAt, filters)),
				AnalyticsJson = JsonSerializer.Serialize(MapSnapshotWithStudents(reportSnapshot, analytics.StudentsStatistics, analytics.GroupProgress)),
				RecommendationsJson = JsonSerializer.Serialize(recommendations),
				Format = "Json",
				FilePath = null,
				CreatedAt = createdAt
			};

			_context.GeneratedReports.Add(report);
			await _context.SaveChangesAsync();

			return MapReport(report);
		}

		public async Task<List<GeneratedReportDTO>?> GetStudentReportsHistoryAsync(string userId)
		{
			var userExists = await _context.Users
				.AsNoTracking()
				.AnyAsync(u => u.Id == userId);

			if (!userExists)
			{
				return null;
			}

			return await _context.GeneratedReports
				.AsNoTracking()
				.Where(r => r.ReportType == "Student" && r.UserId == userId)
				.OrderByDescending(r => r.CreatedAt)
				.Select(r => new GeneratedReportDTO
				{
					Id = r.Id,
					ReportType = r.ReportType,
					UserId = r.UserId,
					GroupId = r.GroupId,
					Title = r.Title,
					SummaryJson = r.SummaryJson,
					AnalyticsJson = r.AnalyticsJson,
					RecommendationsJson = r.RecommendationsJson,
					Format = r.Format,
					FilePath = r.FilePath,
					CreatedAt = r.CreatedAt
				})
				.ToListAsync();
		}

		public async Task<List<GeneratedReportDTO>?> GetGroupReportsHistoryAsync(int groupId)
		{
			var groupExists = await _context.Groups
				.AsNoTracking()
				.AnyAsync(g => g.Id == groupId);

			if (!groupExists)
			{
				return null;
			}

			return await _context.GeneratedReports
				.AsNoTracking()
				.Where(r => r.ReportType == "Group" && r.GroupId == groupId)
				.OrderByDescending(r => r.CreatedAt)
				.Select(r => new GeneratedReportDTO
				{
					Id = r.Id,
					ReportType = r.ReportType,
					UserId = r.UserId,
					GroupId = r.GroupId,
					Title = r.Title,
					SummaryJson = r.SummaryJson,
					AnalyticsJson = r.AnalyticsJson,
					RecommendationsJson = r.RecommendationsJson,
					Format = r.Format,
					FilePath = r.FilePath,
					CreatedAt = r.CreatedAt
				})
				.ToListAsync();
		}

		private async Task<List<GeneratedRecommendationDTO>> LoadRecommendationsAsync(
			string targetType,
			string? userId,
			int? groupId,
			AnalyticsFilterDTO? filters)
		{
			var recommendations = await _context.GeneratedRecommendations
				.AsNoTracking()
				.Where(r =>
					r.TargetType == targetType &&
					(userId == null || r.UserId == userId) &&
					(groupId == null || r.GroupId == groupId))
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

			var excludedAspectIds = await BuildExcludedRecommendationAspectIdsAsync(filters);
			return excludedAspectIds.Count == 0
				? recommendations
				: recommendations
					.Where(recommendation =>
						!recommendation.KnowledgeAspectId.HasValue ||
						!excludedAspectIds.Contains(recommendation.KnowledgeAspectId.Value))
					.ToList();
		}

		private async Task<HashSet<int>> BuildExcludedRecommendationAspectIdsAsync(AnalyticsFilterDTO? filters)
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

		private async Task<List<StudentGroupStatisticsDTO>> BuildGroupStudentsStatisticsAsync(int groupId)
		{
			var groupStudents = await _context.UserGroups
				.AsNoTracking()
				.Where(ug => ug.GroupId == groupId)
				.Include(ug => ug.User)
				.Select(ug => new
				{
					ug.UserId,
					ug.User.UserName,
					ug.User.Name,
					ug.User.SecondName,
					ug.User.Patronymic
				})
				.ToListAsync();
			var userIds = groupStudents.Select(student => student.UserId).ToList();

			if (userIds.Count == 0)
			{
				return new List<StudentGroupStatisticsDTO>();
			}

			var errors = await _context.ErrorRecords
				.AsNoTracking()
				.Include(e => e.AnalysisRun)
				.Include(e => e.ErrorType)
				.Include(e => e.AlphaBeta)
				.Include(e => e.FifteenPuzzle)
				.Where(e =>
					e.IsPrimary &&
					e.IsSummary != true &&
					((e.AnalysisRun != null && userIds.Contains(e.AnalysisRun.UserId)) ||
					(e.AlphaBeta != null && userIds.Contains(e.AlphaBeta.UserId)) ||
					(e.FifteenPuzzle != null && userIds.Contains(e.FifteenPuzzle.UserId))))
				.ToListAsync();

			var gaps = await _context.KnowledgeGaps
				.AsNoTracking()
				.Include(g => g.KnowledgeAspect)
					.ThenInclude(ka => ka.Topic)
				.Include(g => g.AlphaBeta)
				.Include(g => g.FifteenPuzzle)
				.Where(g => userIds.Contains(g.UserId))
				.ToListAsync();

			return groupStudents
				.Select(student => BuildStudentGroupStatistics(
					student.UserId,
					student.UserName,
					BuildFullName(student.SecondName, student.Name, student.Patronymic),
					errors,
					gaps))
				.OrderBy(student => student.FullName ?? student.UserName ?? student.UserId)
				.ToList();
		}

		private static StudentGroupStatisticsDTO BuildStudentGroupStatistics(
			string userId,
			string? userName,
			string? fullName,
			IEnumerable<ErrorRecord> groupErrors,
			IEnumerable<KnowledgeGap> groupGaps)
		{
			var studentErrors = groupErrors
				.Where(error => ErrorBelongsToUser(error, userId))
				.ToList();
			var studentGaps = groupGaps
				.Where(gap => gap.UserId == userId)
				.ToList();

			return new StudentGroupStatisticsDTO
			{
				UserId = userId,
				UserName = userName,
				FullName = fullName,
				TotalErrors = studentErrors.Count,
				TotalKnowledgeGaps = studentGaps.Count,
				AverageGapScore = CalculateAverageGapScore(studentGaps),
				HighSeverityErrorsCount = studentErrors.Count(e => e.SeverityScore >= HighSeverityThreshold),
				TopErrorTypes = BuildTopErrorTypes(studentErrors),
				TopKnowledgeGaps = BuildTopKnowledgeGaps(studentGaps)
			};
		}

		private static bool ErrorBelongsToUser(ErrorRecord error, string userId)
		{
			return error.AnalysisRun?.UserId == userId ||
				error.AlphaBeta?.UserId == userId ||
				error.FifteenPuzzle?.UserId == userId;
		}

		private static List<TopErrorTypeDTO> BuildTopErrorTypes(IEnumerable<ErrorRecord> errors)
		{
			return errors
				.Where(e => e.ErrorTypeId != null && e.ErrorType != null)
				.GroupBy(e => new
				{
					ErrorTypeId = e.ErrorTypeId!.Value,
					e.ErrorType!.Code,
					e.ErrorType.Name
				})
				.Select(g => new TopErrorTypeDTO
				{
					ErrorTypeId = g.Key.ErrorTypeId,
					Code = g.Key.Code,
					Name = g.Key.Name,
					Count = g.Count(),
					AverageSeverity = Math.Round(g.Average(e => e.SeverityScore), 2)
				})
				.OrderByDescending(x => x.Count)
				.ThenByDescending(x => x.AverageSeverity)
				.Take(TopItemsLimit)
				.ToList();
		}

		private static List<TopKnowledgeGapDTO> BuildTopKnowledgeGaps(IEnumerable<KnowledgeGap> gaps)
		{
			return gaps
				.Where(g => g.KnowledgeAspect != null)
				.GroupBy(g => new
				{
					g.KnowledgeAspectId,
					AspectName = g.KnowledgeAspect.Name,
					TopicName = g.KnowledgeAspect.Topic == null ? null : g.KnowledgeAspect.Topic.Name
				})
				.Select(g => new TopKnowledgeGapDTO
				{
					KnowledgeAspectId = g.Key.KnowledgeAspectId,
					AspectName = g.Key.AspectName,
					TopicName = g.Key.TopicName,
					Count = g.Count(),
					AverageGapScore = Math.Round(g.Average(x => x.GapScore), 2),
					MaxGapScore = Math.Round(g.Max(x => x.GapScore), 2)
				})
				.OrderByDescending(x => x.Count)
				.ThenByDescending(x => x.AverageGapScore)
				.Take(TopItemsLimit)
				.ToList();
		}

		private static double CalculateAverageGapScore(IReadOnlyCollection<KnowledgeGap> gaps)
		{
			return gaps.Count == 0
				? 0
				: Math.Round(gaps.Average(g => g.GapScore), 2);
		}

		private static object BuildReportSummary(
			string reportType,
			string title,
			string? userId,
			int? groupId,
			AnalyticsSnapshot snapshot,
			List<GeneratedRecommendationDTO> recommendations,
			DateTime createdAt,
			AnalyticsFilterDTO? filters)
		{
			var topGaps = ParseTopKnowledgeGaps(snapshot.TopKnowledgeGapsJson);
			var riskLevel = GetRiskLevel(snapshot.AverageGapScore, snapshot.HighSeverityErrorsCount);
			var mainProblems = topGaps
				.Select(gap =>
				{
					var score = gap.AverageGapScore;
					var level = GetProblemLevel(score);

					return new
					{
						aspectName = gap.AspectName,
						topicName = gap.TopicName,
						score,
						level,
						explanation = GetProblemExplanation(level)
					};
				})
				.ToList();

			return new
			{
				reportType,
				title,
				userId,
				groupId,
				recommendationsCount = recommendations.Count,
				createdAt,
				riskLevel,
				conclusion = BuildConclusion(reportType, riskLevel, topGaps),
				mainProblems,
				teacherActions = BuildTeacherActions(recommendations),
				filters = BuildFiltersSummary(filters)
			};
		}

		private static object BuildFiltersSummary(AnalyticsFilterDTO? filters)
		{
			return new
			{
				excludedErrorTypeIds = NormalizeIds(filters?.ExcludedErrorTypeIds).OrderBy(id => id).ToList(),
				excludedKnowledgeAspectIds = NormalizeIds(filters?.ExcludedKnowledgeAspectIds).OrderBy(id => id).ToList()
			};
		}

		private static List<TopKnowledgeGapDTO> ParseTopKnowledgeGaps(string? json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return new List<TopKnowledgeGapDTO>();
			}

			try
			{
				return JsonSerializer.Deserialize<List<TopKnowledgeGapDTO>>(json, JsonOptions) ?? new List<TopKnowledgeGapDTO>();
			}
			catch
			{
				return new List<TopKnowledgeGapDTO>();
			}
		}

		private static string GetRiskLevel(double averageGapScore, int highSeverityErrorsCount)
		{
			if (averageGapScore >= 75 || highSeverityErrorsCount >= 10)
			{
				return "High";
			}

			if (averageGapScore >= 50 || highSeverityErrorsCount >= 5)
			{
				return "Medium";
			}

			return "Low";
		}

		private static string GetProblemLevel(double score)
		{
			if (score >= 80)
			{
				return "High";
			}

			if (score >= 50)
			{
				return "Medium";
			}

			return "Low";
		}

		private static string GetProblemExplanation(string level)
		{
			return level switch
			{
				"High" => "Выраженный пробел, требующий повторного изучения темы.",
				"Medium" => "Заметное затруднение, рекомендуется дополнительное закрепление.",
				_ => "Небольшое затруднение, достаточно точечной коррекции."
			};
		}

		private static string BuildConclusion(string reportType, string riskLevel, List<TopKnowledgeGapDTO> topGaps)
		{
			if (topGaps.Count == 0)
			{
				return reportType == "Group"
					? "У группы не выявлены выраженные системные затруднения по текущим данным."
					: "У студента не выявлены выраженные пробелы по текущим данным.";
			}

			var riskText = riskLevel switch
			{
				"High" => "высокий",
				"Medium" => "средний",
				_ => "низкий"
			};

			var topic = topGaps.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.TopicName))?.TopicName;
			var aspects = string.Join(", ", topGaps.Take(3).Select(g => g.AspectName).Where(name => !string.IsNullOrWhiteSpace(name)));
			var topicText = string.IsNullOrWhiteSpace(topic) ? "изучаемого материала" : $"темы \"{topic}\"";

			return reportType == "Group"
				? $"У группы выявлен {riskText} уровень риска и системные затруднения по нескольким аспектам {topicText}. Основные проблемы связаны с аспектами: {aspects}. Рекомендуется провести повторное объяснение материала и разобрать типовые ошибки."
				: $"У студента выявлен {riskText} уровень риска и пробелы по {topicText}. Основные затруднения связаны с аспектами: {aspects}. Рекомендуется закрепить материал и выполнить дополнительные задания.";
		}

		private static List<string> BuildTeacherActions(List<GeneratedRecommendationDTO> recommendations)
		{
			if (recommendations.Count == 0)
			{
				return new List<string> { "Дополнительные действия не требуются." };
			}

			return recommendations
				.SelectMany(r =>
				{
					var topic = string.IsNullOrWhiteSpace(r.TopicName) ? "соответствующей теме" : $"теме \"{r.TopicName}\"";
					var aspect = r.KnowledgeAspectId.HasValue ? $"#{r.KnowledgeAspectId}" : topic;

					return new[]
					{
						string.IsNullOrWhiteSpace(r.Title) ? $"Повторить материал по {topic}." : r.Title,
						$"Разобрать типовые ошибки по аспекту: {aspect}.",
						$"Выдать дополнительное задание по {topic}."
					};
				})
				.Distinct()
				.Take(12)
				.ToList();
		}

		private static AnalyticsSnapshotDTO MapSnapshot(AnalyticsSnapshot snapshot)
		{
			return new AnalyticsSnapshotDTO
			{
				Id = snapshot.Id,
				ScopeType = snapshot.ScopeType,
				UserId = snapshot.UserId,
				GroupId = snapshot.GroupId,
				TotalStudents = snapshot.TotalStudents,
				TotalGroups = snapshot.TotalGroups,
				TotalErrors = snapshot.TotalErrors,
				TotalKnowledgeGaps = snapshot.TotalKnowledgeGaps,
				AverageGapScore = snapshot.AverageGapScore,
				HighSeverityErrorsCount = snapshot.HighSeverityErrorsCount,
				TopErrorTypesJson = snapshot.TopErrorTypesJson,
				TopKnowledgeGapsJson = snapshot.TopKnowledgeGapsJson,
				CreatedAt = snapshot.CreatedAt
			};
		}

		private static object MapSnapshotWithProgress(
			AnalyticsSnapshot snapshot,
			LearningProgressDTO? learningProgress)
		{
			var mapped = MapSnapshot(snapshot);

			return new
			{
				mapped.Id,
				mapped.ScopeType,
				mapped.UserId,
				mapped.GroupId,
				mapped.TotalStudents,
				mapped.TotalGroups,
				mapped.TotalErrors,
				mapped.TotalKnowledgeGaps,
				mapped.AverageGapScore,
				mapped.HighSeverityErrorsCount,
				mapped.TopErrorTypesJson,
				mapped.TopKnowledgeGapsJson,
				mapped.CreatedAt,
				StudentProgress = learningProgress
			};
		}

		private static AnalyticsSnapshot BuildStudentSnapshot(StudentAnalyticsDTO analytics, DateTime createdAt)
		{
			return new AnalyticsSnapshot
			{
				ScopeType = "Student",
				UserId = analytics.UserId,
				GroupId = analytics.GroupId,
				TotalErrors = analytics.TotalErrors,
				TotalKnowledgeGaps = analytics.TotalKnowledgeGaps,
				AverageGapScore = analytics.AverageGapScore,
				HighSeverityErrorsCount = analytics.HighSeverityErrorsCount,
				TopErrorTypesJson = JsonSerializer.Serialize(analytics.TopErrorTypes),
				TopKnowledgeGapsJson = JsonSerializer.Serialize(analytics.TopKnowledgeGaps),
				CreatedAt = createdAt
			};
		}

		private static AnalyticsSnapshot BuildGroupSnapshot(GroupAnalyticsDTO analytics, DateTime createdAt)
		{
			return new AnalyticsSnapshot
			{
				ScopeType = "Group",
				GroupId = analytics.GroupId,
				TotalStudents = analytics.StudentsCount,
				TotalErrors = analytics.TotalErrors,
				TotalKnowledgeGaps = analytics.TotalKnowledgeGaps,
				AverageGapScore = analytics.AverageGapScore,
				HighSeverityErrorsCount = analytics.HighSeverityErrorsCount,
				TopErrorTypesJson = JsonSerializer.Serialize(analytics.TopErrorTypes),
				TopKnowledgeGapsJson = JsonSerializer.Serialize(analytics.TopKnowledgeGaps),
				CreatedAt = createdAt
			};
		}

		private static object MapSnapshotWithStudents(
			AnalyticsSnapshot snapshot,
			List<StudentGroupStatisticsDTO> studentsStatistics,
			LearningProgressDTO? groupProgress)
		{
			var mapped = MapSnapshot(snapshot);

			return new
			{
				mapped.Id,
				mapped.ScopeType,
				mapped.UserId,
				mapped.GroupId,
				mapped.TotalStudents,
				mapped.TotalGroups,
				mapped.TotalErrors,
				mapped.TotalKnowledgeGaps,
				mapped.AverageGapScore,
				mapped.HighSeverityErrorsCount,
				mapped.TopErrorTypesJson,
				mapped.TopKnowledgeGapsJson,
				mapped.CreatedAt,
				GroupProgress = groupProgress,
				StudentsStatistics = studentsStatistics
			};
		}

		private static string? BuildFullName(params string?[] parts)
		{
			var fullName = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

			return string.IsNullOrWhiteSpace(fullName)
				? null
				: fullName;
		}

		private static HashSet<int> NormalizeIds(IEnumerable<int>? ids)
		{
			return ids?
				.Where(id => id > 0)
				.ToHashSet() ?? new HashSet<int>();
		}

		private static GeneratedReportDTO MapReport(GeneratedReport report)
		{
			return new GeneratedReportDTO
			{
				Id = report.Id,
				ReportType = report.ReportType,
				UserId = report.UserId,
				GroupId = report.GroupId,
				Title = report.Title,
				SummaryJson = report.SummaryJson,
				AnalyticsJson = report.AnalyticsJson,
				RecommendationsJson = report.RecommendationsJson,
				Format = report.Format,
				FilePath = report.FilePath,
				CreatedAt = report.CreatedAt
			};
		}
	}
}
