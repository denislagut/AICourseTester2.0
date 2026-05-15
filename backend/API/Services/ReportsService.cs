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

		private readonly MainDbContext _context;

		public ReportsService(MainDbContext context)
		{
			_context = context;
		}

		public async Task<GeneratedReportDTO?> GenerateStudentReportAsync(string userId)
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

			var recommendations = await LoadRecommendationsAsync("Student", userId, null);
			var createdAt = DateTime.UtcNow;

			var report = new GeneratedReport
			{
				ReportType = "Student",
				UserId = userId,
				GroupId = groupId,
				Title = "Отчет по студенту",
				SummaryJson = JsonSerializer.Serialize(BuildReportSummary("Student", "Отчет по студенту", userId, groupId, snapshot, recommendations, createdAt)),
				AnalyticsJson = JsonSerializer.Serialize(MapSnapshot(snapshot)),
				RecommendationsJson = JsonSerializer.Serialize(recommendations),
				Format = "Json",
				FilePath = null,
				CreatedAt = createdAt
			};

			_context.GeneratedReports.Add(report);
			await _context.SaveChangesAsync();

			return MapReport(report);
		}

		public async Task<GeneratedReportDTO?> GenerateGroupReportAsync(int groupId)
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

			var recommendations = await LoadRecommendationsAsync("Group", null, groupId);
			var createdAt = DateTime.UtcNow;

			var report = new GeneratedReport
			{
				ReportType = "Group",
				UserId = null,
				GroupId = groupId,
				Title = "Отчет по группе",
				SummaryJson = JsonSerializer.Serialize(BuildReportSummary("Group", "Отчет по группе", null, groupId, snapshot, recommendations, createdAt)),
				AnalyticsJson = JsonSerializer.Serialize(MapSnapshot(snapshot)),
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
			int? groupId)
		{
			return await _context.GeneratedRecommendations
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
		}

		private static object BuildReportSummary(
			string reportType,
			string title,
			string? userId,
			int? groupId,
			AnalyticsSnapshot snapshot,
			List<GeneratedRecommendationDTO> recommendations,
			DateTime createdAt)
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
				teacherActions = BuildTeacherActions(recommendations)
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
