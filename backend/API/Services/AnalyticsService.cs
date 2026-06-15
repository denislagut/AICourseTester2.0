using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AICourseTester.Services
{
	public class AnalyticsService : IAnalyticsService
	{
		private const double HighSeverityThreshold = 3.0;
		private const int TopItemsLimit = 10;
		private const int ComparisonTimeOffsetHours = 6;

		private readonly MainDbContext _context;

		public AnalyticsService(MainDbContext context)
		{
			_context = context;
		}

		public async Task<AnalyticsSummaryDTO> GetSummaryAsync()
		{
			var studentRoleUserIds =
				from userRole in _context.UserRoles.AsNoTracking()
				join role in _context.Roles.AsNoTracking()
					on userRole.RoleId equals role.Id
				where role.Name == "Student" || role.NormalizedName == "STUDENT"
				select userRole.UserId;

			var groupedStudentUserIds = _context.UserGroups
				.AsNoTracking()
				.Select(ug => ug.UserId);

			var totalStudents = await groupedStudentUserIds
				.Union(studentRoleUserIds)
				.CountAsync();

			var currentKnowledgeGaps = await LoadCurrentKnowledgeGapsAsync();
			var totalKnowledgeGaps = currentKnowledgeGaps.Count;

			var averageGapScore = totalKnowledgeGaps == 0
				? 0
				: currentKnowledgeGaps.Average(g => g.GapScore);

			var summary = new AnalyticsSummaryDTO
			{
				TotalStudents = totalStudents,
				TotalGroups = await _context.Groups.AsNoTracking().CountAsync(),
				TotalErrors = await _context.ErrorRecords.AsNoTracking().CountAsync(e => e.IsPrimary && e.IsSummary != true),
				TotalKnowledgeGaps = totalKnowledgeGaps,
				AverageGapScore = Math.Round(averageGapScore, 2),
				HighSeverityErrorsCount = await _context.ErrorRecords
					.AsNoTracking()
					.CountAsync(e => e.IsPrimary && e.IsSummary != true && e.SeverityScore >= HighSeverityThreshold)
			};

			await SaveGlobalSnapshotAsync(
				summary,
				await GetTopErrorTypesAsync(),
				await GetTopKnowledgeGapsAsync());

			return summary;
		}

		public async Task<List<TopErrorTypeDTO>> GetTopErrorTypesAsync()
		{
			var errors = await _context.ErrorRecords
				.AsNoTracking()
				.Where(e => e.IsPrimary && e.IsSummary != true && e.ErrorTypeId != null)
				.Include(e => e.ErrorType)
				.ToListAsync();

			return errors
				.Where(e => e.ErrorType != null)
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

		public async Task<List<TopKnowledgeGapDTO>> GetTopKnowledgeGapsAsync()
		{
			var gaps = await LoadCurrentKnowledgeGapsAsync();

			return gaps
				.Where(g => g.KnowledgeAspect != null)
				.GroupBy(g => new
				{
					g.KnowledgeAspectId,
					AspectName = g.KnowledgeAspect.Name,
					g.KnowledgeAspect.TopicName
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

		public async Task<List<ErrorTypeReferenceDTO>> GetErrorTypesAsync()
		{
			return await _context.ErrorTypes
				.AsNoTracking()
				.OrderBy(e => e.Name)
				.Select(e => new ErrorTypeReferenceDTO
				{
					Id = e.Id,
					Code = e.Code,
					Name = e.Name,
					Description = e.Description
				})
				.ToListAsync();
		}

		public async Task<List<KnowledgeAspectReferenceDTO>> GetKnowledgeAspectsAsync()
		{
			return await _context.KnowledgeAspects
				.AsNoTracking()
				.Where(a => a.IsActive)
				.OrderBy(a => a.TopicName)
				.ThenBy(a => a.Name)
				.Select(a => new KnowledgeAspectReferenceDTO
				{
					Id = a.Id,
					Name = a.Name,
					Topic = a.TopicName,
					Description = a.Description
				})
				.ToListAsync();
		}

		public async Task<StudentAnalyticsDTO?> GetStudentAnalyticsAsync(string userId, AnalyticsFilterDTO? filters = null)
		{
			var user = await _context.Users
				.AsNoTracking()
				.Where(u => u.Id == userId)
				.Select(u => new
				{
					u.Id,
					u.UserName,
					u.Name,
					u.SecondName,
					u.Patronymic
				})
				.FirstOrDefaultAsync();

			if (user == null)
			{
				return null;
			}

			var group = await _context.UserGroups
				.AsNoTracking()
				.Where(ug => ug.UserId == userId)
				.Include(ug => ug.Group)
				.Select(ug => new
				{
					ug.GroupId,
					GroupName = ug.Group.Name
				})
				.FirstOrDefaultAsync();

			var errors = await LoadErrorsForUsersAsync(new[] { userId });
			var gapHistory = await LoadKnowledgeGapHistoryForUsersAsync(new[] { userId });
			var gaps = SelectLatestKnowledgeGaps(gapHistory);
			(errors, gaps) = await ApplyAnalyticsFiltersAsync(errors, gaps, filters);
			FillLearningProgressFromHistory(gaps, gapHistory);
			await FillLearningProgressFromPreviousSnapshotAsync(
				gaps,
				scopeType: "Student",
				userId: userId,
				groupId: null,
				userIds: new[] { userId });

			var analytics = new StudentAnalyticsDTO
			{
				UserId = user.Id,
				UserName = user.UserName,
				FullName = BuildFullName(user.SecondName, user.Name, user.Patronymic),
				GroupId = group?.GroupId,
				GroupName = group?.GroupName,
				TotalErrors = errors.Count,
				TotalKnowledgeGaps = gaps.Count,
				AverageGapScore = CalculateAverageGapScore(gaps),
				HighSeverityErrorsCount = errors.Count(e => e.SeverityScore >= HighSeverityThreshold),
				TopErrorTypes = BuildTopErrorTypes(errors),
				TopKnowledgeGaps = BuildTopKnowledgeGaps(gaps),
				StudentProgress = BuildLearningProgress(gaps)
			};

			if (!HasActiveFilters(filters))
			{
				await SaveStudentSnapshotAsync(analytics);
			}

			return analytics;
		}

		public async Task<GroupAnalyticsDTO?> GetGroupAnalyticsAsync(int groupId, AnalyticsFilterDTO? filters = null)
		{
			var group = await _context.Groups
				.AsNoTracking()
				.Where(g => g.Id == groupId)
				.Select(g => new
				{
					g.Id,
					g.Name
				})
				.FirstOrDefaultAsync();

			if (group == null)
			{
				return null;
			}

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

			var errors = userIds.Count == 0
				? new List<Models.ErrorRecord>()
				: await LoadErrorsForUsersAsync(userIds);

			var gapHistory = userIds.Count == 0
				? new List<Models.KnowledgeGap>()
				: await LoadKnowledgeGapHistoryForUsersAsync(userIds);
			var gaps = SelectLatestKnowledgeGaps(gapHistory);
			(errors, gaps) = await ApplyAnalyticsFiltersAsync(errors, gaps, filters);
			FillLearningProgressFromHistory(gaps, gapHistory);
			await FillLearningProgressFromPreviousSnapshotAsync(
				gaps,
				scopeType: "Group",
				userId: null,
				groupId: groupId,
				userIds: userIds);

			var analytics = new GroupAnalyticsDTO
			{
				GroupId = group.Id,
				GroupName = group.Name,
				StudentsCount = userIds.Count,
				TotalErrors = errors.Count,
				TotalKnowledgeGaps = gaps.Count,
				AverageGapScore = CalculateAverageGapScore(gaps),
				HighSeverityErrorsCount = errors.Count(e => e.SeverityScore >= HighSeverityThreshold),
				TopErrorTypes = BuildTopErrorTypes(errors),
				TopKnowledgeGaps = BuildTopKnowledgeGaps(gaps),
				GroupProgress = BuildLearningProgress(gaps),
				StudentsStatistics = groupStudents
					.Select(student => BuildStudentGroupStatistics(
						student.UserId,
						student.UserName,
						BuildFullName(student.SecondName, student.Name, student.Patronymic),
						errors,
						gaps))
					.OrderBy(student => student.FullName ?? student.UserName ?? student.UserId)
					.ToList()
			};

			if (!HasActiveFilters(filters))
			{
				await SaveGroupSnapshotAsync(analytics);
			}

			return analytics;
		}

		public async Task<List<AnalyticsSnapshotDTO>> GetGlobalSnapshotsAsync()
		{
			return await _context.AnalyticsSnapshots
				.AsNoTracking()
				.Where(s => s.ScopeType == "Global")
				.OrderByDescending(s => s.CreatedAt)
				.Select(s => new AnalyticsSnapshotDTO
				{
					Id = s.Id,
					ScopeType = s.ScopeType,
					UserId = s.UserId,
					GroupId = s.GroupId,
					TotalStudents = s.TotalStudents,
					TotalGroups = s.TotalGroups,
					TotalErrors = s.TotalErrors,
					TotalKnowledgeGaps = s.TotalKnowledgeGaps,
					AverageGapScore = s.AverageGapScore,
					HighSeverityErrorsCount = s.HighSeverityErrorsCount,
					TopErrorTypesJson = s.TopErrorTypesJson,
					TopKnowledgeGapsJson = s.TopKnowledgeGapsJson,
					CreatedAt = s.CreatedAt
				})
				.ToListAsync();
		}

		public async Task<List<AnalyticsSnapshotDTO>?> GetStudentSnapshotsAsync(string userId)
		{
			var studentExists = await _context.Users
				.AsNoTracking()
				.AnyAsync(u => u.Id == userId);

			if (!studentExists)
			{
				return null;
			}

			return await _context.AnalyticsSnapshots
				.AsNoTracking()
				.Where(s => s.ScopeType == "Student" && s.UserId == userId)
				.OrderByDescending(s => s.CreatedAt)
				.Select(s => new AnalyticsSnapshotDTO
				{
					Id = s.Id,
					ScopeType = s.ScopeType,
					UserId = s.UserId,
					GroupId = s.GroupId,
					TotalStudents = s.TotalStudents,
					TotalGroups = s.TotalGroups,
					TotalErrors = s.TotalErrors,
					TotalKnowledgeGaps = s.TotalKnowledgeGaps,
					AverageGapScore = s.AverageGapScore,
					HighSeverityErrorsCount = s.HighSeverityErrorsCount,
					TopErrorTypesJson = s.TopErrorTypesJson,
					TopKnowledgeGapsJson = s.TopKnowledgeGapsJson,
					CreatedAt = s.CreatedAt
				})
				.ToListAsync();
		}

		public async Task<List<AnalyticsSnapshotDTO>?> GetGroupSnapshotsAsync(int groupId)
		{
			var groupExists = await _context.Groups
				.AsNoTracking()
				.AnyAsync(g => g.Id == groupId);

			if (!groupExists)
			{
				return null;
			}

			return await _context.AnalyticsSnapshots
				.AsNoTracking()
				.Where(s => s.ScopeType == "Group" && s.GroupId == groupId)
				.OrderByDescending(s => s.CreatedAt)
				.Select(s => new AnalyticsSnapshotDTO
				{
					Id = s.Id,
					ScopeType = s.ScopeType,
					UserId = s.UserId,
					GroupId = s.GroupId,
					TotalStudents = s.TotalStudents,
					TotalGroups = s.TotalGroups,
					TotalErrors = s.TotalErrors,
					TotalKnowledgeGaps = s.TotalKnowledgeGaps,
					AverageGapScore = s.AverageGapScore,
					HighSeverityErrorsCount = s.HighSeverityErrorsCount,
					TopErrorTypesJson = s.TopErrorTypesJson,
					TopKnowledgeGapsJson = s.TopKnowledgeGapsJson,
					CreatedAt = s.CreatedAt
				})
				.ToListAsync();
		}

		public async Task<List<AnalyticsSnapshotDTO>?> GetStudentSnapshotsForPeriodAsync(
			string userId,
			DateTime? from,
			DateTime? to)
		{
			var studentExists = await _context.Users
				.AsNoTracking()
				.AnyAsync(u => u.Id == userId);

			if (!studentExists)
			{
				return null;
			}

			var query = _context.AnalyticsSnapshots
				.AsNoTracking()
				.Where(s => s.ScopeType == "Student" && s.UserId == userId);

			query = ApplyPeriod(query, from, to);

			var snapshots = await query
				.OrderByDescending(s => s.CreatedAt)
				.ToListAsync();

			return snapshots.Select(MapSnapshot).ToList();
		}

		public async Task<List<AnalyticsSnapshotDTO>?> GetGroupSnapshotsForPeriodAsync(
			int groupId,
			DateTime? from,
			DateTime? to)
		{
			var groupExists = await _context.Groups
				.AsNoTracking()
				.AnyAsync(g => g.Id == groupId);

			if (!groupExists)
			{
				return null;
			}

			var query = _context.AnalyticsSnapshots
				.AsNoTracking()
				.Where(s => s.ScopeType == "Group" && s.GroupId == groupId);

			query = ApplyPeriod(query, from, to);

			var snapshots = await query
				.OrderByDescending(s => s.CreatedAt)
				.ToListAsync();

			return snapshots.Select(MapSnapshot).ToList();
		}

		public async Task<AnalyticsCompareDTO?> CompareStudentPeriodsAsync(
			string userId,
			DateTime? beforeFrom,
			DateTime? beforeTo,
			DateTime? afterFrom,
			DateTime? afterTo,
			AnalyticsFilterDTO? filters = null)
		{
			var studentExists = await _context.Users
				.AsNoTracking()
				.AnyAsync(u => u.Id == userId);

			if (!studentExists)
			{
				return null;
			}

			var before = await BuildSourceAnalyticsForPeriodAsync(
				new[] { userId },
				beforeFrom,
				beforeTo,
				filters,
				"Student",
				userId,
				null);
			var after = await BuildSourceAnalyticsForPeriodAsync(
				new[] { userId },
				afterFrom,
				afterTo,
				filters,
				"Student",
				userId,
				null);

			return BuildCompareResult(before, after);
		}

		public async Task<AnalyticsCompareDTO?> CompareGroupPeriodsAsync(
			int groupId,
			DateTime? beforeFrom,
			DateTime? beforeTo,
			DateTime? afterFrom,
			DateTime? afterTo,
			AnalyticsFilterDTO? filters = null)
		{
			var groupExists = await _context.Groups
				.AsNoTracking()
				.AnyAsync(g => g.Id == groupId);

			if (!groupExists)
			{
				return null;
			}

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

			var before = await BuildSourceAnalyticsForPeriodAsync(
				userIds,
				beforeFrom,
				beforeTo,
				filters,
				"Group",
				null,
				groupId);
			var after = await BuildSourceAnalyticsForPeriodAsync(
				userIds,
				afterFrom,
				afterTo,
				filters,
				"Group",
				null,
				groupId);

			return BuildCompareResult(before, after);
		}

		public async Task<List<string>?> GetStudentActivityDatesAsync(string userId)
		{
			var studentExists = await _context.Users
				.AsNoTracking()
				.AnyAsync(u => u.Id == userId);

			if (!studentExists)
			{
				return null;
			}

			return await GetActivityDatesForUsersAsync(new[] { userId });
		}

		public async Task<List<string>?> GetGroupActivityDatesAsync(int groupId)
		{
			var groupExists = await _context.Groups
				.AsNoTracking()
				.AnyAsync(g => g.Id == groupId);

			if (!groupExists)
			{
				return null;
			}

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

			return await GetActivityDatesForUsersAsync(userIds);
		}

		private async Task<AnalyticsSnapshot?> BuildSourceAnalyticsForPeriodAsync(
			IReadOnlyCollection<string> userIds,
			DateTime? from,
			DateTime? to,
			AnalyticsFilterDTO? filters,
			string scopeType,
			string? userId,
			int? groupId)
		{
			if (userIds.Count == 0)
			{
				return null;
			}

			var (fromUtc, toUtc) = BuildUtcPeriod(from, to);
			var analysisRuns = await _context.AnalysisRuns
				.AsNoTracking()
				.Where(r => userIds.Contains(r.UserId) && r.CompletedAt.HasValue)
				.ToListAsync();
			var periodAnalysisRuns = analysisRuns
				.Where(r => IsInPeriod(EnsureUtc(r.CompletedAt!.Value), fromUtc, toUtc))
				.ToList();
			var periodAnalysisRunIds = periodAnalysisRuns
				.Select(r => r.Id)
				.ToList();

			var periodErrors = periodAnalysisRunIds.Count == 0
				? new List<Models.ErrorRecord>()
				: await _context.ErrorRecords
					.AsNoTracking()
					.Include(e => e.ErrorType)
					.Where(e =>
						e.IsPrimary &&
						e.IsSummary != true &&
						e.AnalysisRunId.HasValue &&
						periodAnalysisRunIds.Contains(e.AnalysisRunId.Value))
					.ToListAsync();
			var periodGaps = periodAnalysisRunIds.Count == 0
				? new List<Models.KnowledgeGap>()
				: await _context.KnowledgeGaps
					.AsNoTracking()
					.Include(g => g.KnowledgeAspect)
					.Where(g => g.AnalysisRunId.HasValue && periodAnalysisRunIds.Contains(g.AnalysisRunId.Value))
					.ToListAsync();

			var fallbackErrors = await _context.ErrorRecords
				.AsNoTracking()
				.Include(e => e.ErrorType)
				.Include(e => e.AlphaBeta)
				.Include(e => e.FifteenPuzzle)
				.Where(e =>
					e.IsPrimary &&
					e.IsSummary != true &&
					!e.AnalysisRunId.HasValue &&
					((e.AlphaBeta != null && userIds.Contains(e.AlphaBeta.UserId)) ||
					(e.FifteenPuzzle != null && userIds.Contains(e.FifteenPuzzle.UserId))))
				.ToListAsync();
			periodErrors.AddRange(fallbackErrors
				.Where(e => IsInPeriod(EnsureUtc(e.CreatedAt), fromUtc, toUtc)));

			var fallbackGaps = await _context.KnowledgeGaps
				.AsNoTracking()
				.Include(g => g.KnowledgeAspect)
				.Where(g => !g.AnalysisRunId.HasValue && userIds.Contains(g.UserId))
				.ToListAsync();
			periodGaps.AddRange(fallbackGaps
				.Where(g => IsInPeriod(EnsureUtc(g.CreatedAt), fromUtc, toUtc)));
			(periodErrors, periodGaps) = await ApplyAnalyticsFiltersAsync(periodErrors, periodGaps, filters);

			if (!HasActiveFilters(filters) &&
				periodAnalysisRuns.Count > 0 &&
				periodErrors.Count == 0 &&
				periodGaps.Count == 0)
			{
				var fallbackSnapshot = await GetSnapshotFallbackForActivityPeriodAsync(
					scopeType,
					userId,
					groupId,
					userIds,
					periodAnalysisRuns);

				if (fallbackSnapshot != null)
				{
					return fallbackSnapshot;
				}
			}

			var activityDates = periodAnalysisRuns
				.Select(r => EnsureUtc(r.CompletedAt!.Value))
				.Concat(periodErrors
					.Where(e => !e.AnalysisRunId.HasValue)
					.Select(e => EnsureUtc(e.CreatedAt)))
				.Concat(periodGaps
					.Where(g => !g.AnalysisRunId.HasValue)
					.Select(g => EnsureUtc(g.CreatedAt)))
				.ToList();

			if (activityDates.Count == 0)
			{
				return null;
			}

			return new AnalyticsSnapshot
			{
				TotalErrors = periodErrors.Count,
				TotalKnowledgeGaps = periodGaps.Count,
				AverageGapScore = CalculateAverageGapScore(periodGaps),
				HighSeverityErrorsCount = periodErrors.Count(e => e.SeverityScore >= HighSeverityThreshold),
				TopErrorTypesJson = JsonSerializer.Serialize(BuildTopErrorTypes(periodErrors)),
				TopKnowledgeGapsJson = JsonSerializer.Serialize(BuildTopKnowledgeGaps(periodGaps)),
				CreatedAt = activityDates.DefaultIfEmpty(DateTime.UtcNow).Max()
			};
		}

		private async Task<AnalyticsSnapshot?> GetSnapshotFallbackForActivityPeriodAsync(
			string scopeType,
			string? userId,
			int? groupId,
			IReadOnlyCollection<string> userIds,
			IReadOnlyCollection<Models.Analysis.AnalysisRun> periodAnalysisRuns)
		{
			if (periodAnalysisRuns.Count == 0)
			{
				return null;
			}

			var latestActivityAt = periodAnalysisRuns
				.Select(run => EnsureUtc(run.CompletedAt!.Value))
				.Max();

			var nextActivityAt = await _context.AnalysisRuns
				.AsNoTracking()
				.Where(run =>
					userIds.Contains(run.UserId) &&
					run.CompletedAt.HasValue &&
					run.CompletedAt.Value > latestActivityAt)
				.OrderBy(run => run.CompletedAt)
				.Select(run => run.CompletedAt)
				.FirstOrDefaultAsync();

			var snapshotQuery = _context.AnalyticsSnapshots
				.AsNoTracking()
				.Where(snapshot =>
					snapshot.ScopeType == scopeType &&
					snapshot.CreatedAt >= latestActivityAt);

			if (!string.IsNullOrWhiteSpace(userId))
			{
				snapshotQuery = snapshotQuery.Where(snapshot => snapshot.UserId == userId);
			}

			if (groupId.HasValue)
			{
				snapshotQuery = snapshotQuery.Where(snapshot => snapshot.GroupId == groupId);
			}

			if (nextActivityAt.HasValue)
			{
				snapshotQuery = snapshotQuery.Where(snapshot => snapshot.CreatedAt < nextActivityAt.Value);
			}

			var snapshot = await snapshotQuery
				.OrderByDescending(snapshot => snapshot.CreatedAt)
				.FirstOrDefaultAsync();

			return snapshot == null
				? null
				: CopySnapshotForActivityDate(snapshot, latestActivityAt);
		}

		private static AnalyticsSnapshot CopySnapshotForActivityDate(AnalyticsSnapshot snapshot, DateTime activityDate)
		{
			return new AnalyticsSnapshot
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
				CreatedAt = activityDate
			};
		}

		private async Task<List<string>> GetActivityDatesForUsersAsync(IReadOnlyCollection<string> userIds)
		{
			if (userIds.Count == 0)
			{
				return new List<string>();
			}

			var analysisRuns = await _context.AnalysisRuns
				.AsNoTracking()
				.Where(r => userIds.Contains(r.UserId) && r.CompletedAt.HasValue)
				.ToListAsync();

			return analysisRuns
				.Select(r => EnsureUtc(r.CompletedAt!.Value))
				.Select(FormatComparisonDate)
				.Distinct()
				.OrderByDescending(date => date)
				.ToList();
		}

		private static bool IsInPeriod(DateTime activityDate, DateTime? fromUtc, DateTime? toUtc)
		{
			return (!fromUtc.HasValue || activityDate >= fromUtc.Value) &&
				(!toUtc.HasValue || activityDate <= toUtc.Value);
		}

		private static DateTime GetErrorActivityDate(Models.ErrorRecord error)
		{
			return GetAnalysisRunDate(error.AnalysisRun) ??
				GetTaskDate(error.AlphaBeta?.Date) ??
				GetTaskDate(error.FifteenPuzzle?.Date) ??
				EnsureUtc(error.CreatedAt);
		}

		private static DateTime GetKnowledgeGapActivityDate(
			Models.KnowledgeGap gap,
			IReadOnlyDictionary<int, Models.Analysis.AnalysisRun> analysisRuns)
		{
			var analysisRunDate = gap.AnalysisRunId.HasValue && analysisRuns.TryGetValue(gap.AnalysisRunId.Value, out var analysisRun)
				? GetAnalysisRunDate(analysisRun)
				: null;

			return analysisRunDate ??
				GetTaskDate(gap.AlphaBeta?.Date) ??
				GetTaskDate(gap.FifteenPuzzle?.Date) ??
				EnsureUtc(gap.CreatedAt);
		}

		private static DateTime? GetAnalysisRunDate(Models.Analysis.AnalysisRun? analysisRun)
		{
			if (analysisRun == null)
			{
				return null;
			}

			return GetTaskDate(analysisRun.CompletedAt) ??
				GetTaskDate(analysisRun.StartedAt);
		}

		private static DateTime GetAnalysisRunActivityDate(Models.Analysis.AnalysisRun analysisRun)
		{
			return GetAnalysisRunDate(analysisRun) ?? EnsureUtc(analysisRun.StartedAt);
		}

		private static DateTime? GetTaskDate(DateTime? date)
		{
			return date.HasValue && date.Value != default
				? EnsureUtc(date.Value)
				: null;
		}

		private static DateTime EnsureUtc(DateTime date)
		{
			return date.Kind == DateTimeKind.Utc
				? date
				: DateTime.SpecifyKind(date, DateTimeKind.Utc);
		}

		private static string FormatComparisonDate(DateTime date)
		{
			return EnsureUtc(date)
				.AddHours(ComparisonTimeOffsetHours)
				.ToString("yyyy-MM-dd");
		}

		private async Task<List<Models.ErrorRecord>> LoadErrorsForUsersAsync(IReadOnlyCollection<string> userIds)
		{
			return await _context.ErrorRecords
				.AsNoTracking()
				.Include(e => e.AnalysisRun)
				.Include(e => e.ErrorType)
				.Include(e => e.AlphaBeta)
				.Include(e => e.FifteenPuzzle)
				.Where(e =>
					(e.AnalysisRun != null && userIds.Contains(e.AnalysisRun.UserId)) ||
					(e.AlphaBeta != null && userIds.Contains(e.AlphaBeta.UserId)) ||
					(e.FifteenPuzzle != null && userIds.Contains(e.FifteenPuzzle.UserId)))
				.Where(e => e.IsPrimary && e.IsSummary != true)
				.ToListAsync();
		}

		private async Task<(List<Models.ErrorRecord> Errors, List<Models.KnowledgeGap> Gaps)> ApplyAnalyticsFiltersAsync(
			List<Models.ErrorRecord> errors,
			List<Models.KnowledgeGap> gaps,
			AnalyticsFilterDTO? filters)
		{
			var excludedErrorTypeIds = await BuildExcludedErrorTypeIdsAsync(filters);
			var excludedErrorTypeCodes = await BuildExcludedErrorTypeCodesAsync(excludedErrorTypeIds);
			var excludedKnowledgeAspectIds = NormalizeIds(filters?.ExcludedKnowledgeAspectIds);

			var filteredErrors = excludedErrorTypeIds.Count == 0 && excludedErrorTypeCodes.Count == 0
				? errors
				: errors
					.Where(error => !ShouldExcludeError(error, excludedErrorTypeIds, excludedErrorTypeCodes))
					.ToList();
			var filteredGaps = excludedKnowledgeAspectIds.Count == 0
				? gaps
				: gaps
					.Where(gap => !excludedKnowledgeAspectIds.Contains(gap.KnowledgeAspectId))
					.ToList();

			return (filteredErrors, filteredGaps);
		}

		private static bool ShouldExcludeError(
			Models.ErrorRecord error,
			HashSet<int> excludedErrorTypeIds,
			HashSet<string> excludedErrorTypeCodes)
		{
			return (error.ErrorTypeId.HasValue && excludedErrorTypeIds.Contains(error.ErrorTypeId.Value)) ||
				(!string.IsNullOrWhiteSpace(error.Code) && excludedErrorTypeCodes.Contains(error.Code));
		}

		private async Task<HashSet<int>> BuildExcludedErrorTypeIdsAsync(AnalyticsFilterDTO? filters)
		{
			var excludedErrorTypeIds = NormalizeIds(filters?.ExcludedErrorTypeIds);
			var excludedKnowledgeAspectIds = NormalizeIds(filters?.ExcludedKnowledgeAspectIds);

			if (excludedKnowledgeAspectIds.Count == 0)
			{
				return excludedErrorTypeIds;
			}

			var linkedErrorTypeIds = await _context.ErrorTypeAspects
				.AsNoTracking()
				.Where(link => excludedKnowledgeAspectIds.Contains(link.KnowledgeAspectId))
				.Select(link => link.ErrorTypeId)
				.ToListAsync();

			foreach (var errorTypeId in linkedErrorTypeIds)
			{
				excludedErrorTypeIds.Add(errorTypeId);
			}

			return excludedErrorTypeIds;
		}

		private async Task<HashSet<string>> BuildExcludedErrorTypeCodesAsync(HashSet<int> excludedErrorTypeIds)
		{
			if (excludedErrorTypeIds.Count == 0)
			{
				return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			}

			var codes = await _context.ErrorTypes
				.AsNoTracking()
				.Where(errorType => excludedErrorTypeIds.Contains(errorType.Id))
				.Select(errorType => errorType.Code)
				.ToListAsync();

			return codes
				.Where(code => !string.IsNullOrWhiteSpace(code))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
		}

		private static HashSet<int> NormalizeIds(IEnumerable<int>? ids)
		{
			return ids?
				.Where(id => id > 0)
				.ToHashSet() ?? new HashSet<int>();
		}

		private static bool HasActiveFilters(AnalyticsFilterDTO? filters)
		{
			return NormalizeIds(filters?.ExcludedErrorTypeIds).Count > 0 ||
				NormalizeIds(filters?.ExcludedKnowledgeAspectIds).Count > 0;
		}

		private async Task<List<Models.KnowledgeGap>> LoadKnowledgeGapsForUsersAsync(IReadOnlyCollection<string> userIds)
		{
			var gaps = await LoadKnowledgeGapHistoryForUsersAsync(userIds);
			return SelectLatestKnowledgeGaps(gaps);
		}

		private async Task<List<Models.KnowledgeGap>> LoadCurrentKnowledgeGapsAsync()
		{
			var gaps = await _context.KnowledgeGaps
				.AsNoTracking()
				.Include(g => g.KnowledgeAspect)
				.Include(g => g.AlphaBeta)
				.Include(g => g.FifteenPuzzle)
				.ToListAsync();

			return SelectLatestKnowledgeGaps(gaps);
		}

		private async Task<List<Models.KnowledgeGap>> LoadKnowledgeGapHistoryForUsersAsync(IReadOnlyCollection<string> userIds)
		{
			return await _context.KnowledgeGaps
				.AsNoTracking()
				.Include(g => g.KnowledgeAspect)
				.Include(g => g.AlphaBeta)
				.Include(g => g.FifteenPuzzle)
				.Where(g => userIds.Contains(g.UserId))
				.ToListAsync();
		}

		private static List<Models.KnowledgeGap> SelectLatestKnowledgeGaps(IEnumerable<Models.KnowledgeGap> gaps)
		{
			return gaps
				.GroupBy(g => new
				{
					g.UserId,
					g.TaskType,
					g.AlphaBetaId,
					g.FifteenPuzzleId,
					g.KnowledgeAspectId
				})
				.Select(group => group
					.OrderByDescending(g => g.AnalysisRunId.HasValue)
					.ThenByDescending(g => g.CreatedAt)
					.ThenByDescending(g => g.AnalysisRunId ?? 0)
					.ThenByDescending(g => g.Id)
					.First())
				.ToList();
		}

		private static void FillLearningProgressFromHistory(
			List<Models.KnowledgeGap> currentGaps,
			IReadOnlyCollection<Models.KnowledgeGap> gapHistory)
		{
			foreach (var gap in currentGaps)
			{
				var previousGap = gapHistory
					.Where(previous =>
						previous.Id != gap.Id &&
						previous.UserId == gap.UserId &&
						previous.TaskType == gap.TaskType &&
						previous.KnowledgeAspectId == gap.KnowledgeAspectId &&
						(!gap.AnalysisRunId.HasValue ||
						 !previous.AnalysisRunId.HasValue ||
						 previous.AnalysisRunId.Value != gap.AnalysisRunId.Value))
					.OrderByDescending(previous => previous.CreatedAt)
					.ThenByDescending(previous => previous.AnalysisRunId ?? 0)
					.ThenByDescending(previous => previous.Id)
					.FirstOrDefault();

				if (previousGap == null)
				{
					continue;
				}

				gap.PreviousGapScore = previousGap.GapScore;
				gap.GapScoreDelta = Math.Round(gap.GapScore - previousGap.GapScore, 2);
				gap.Trend = BuildTrend(gap.GapScoreDelta.Value);
			}
		}

		private async Task FillLearningProgressFromPreviousSnapshotAsync(
			List<Models.KnowledgeGap> currentGaps,
			string scopeType,
			string? userId,
			int? groupId,
			IReadOnlyCollection<string> userIds)
		{
			if (currentGaps.Count == 0 || currentGaps.All(gap => gap.PreviousGapScore.HasValue))
			{
				return;
			}

			var latestActivityAt = await _context.AnalysisRuns
				.AsNoTracking()
				.Where(run =>
					userIds.Contains(run.UserId) &&
					run.CompletedAt.HasValue &&
					run.Status == "Completed")
				.OrderByDescending(run => run.CompletedAt)
				.Select(run => run.CompletedAt)
				.FirstOrDefaultAsync();

			var snapshotQuery = _context.AnalyticsSnapshots
				.AsNoTracking()
				.Where(snapshot => snapshot.ScopeType == scopeType);

			if (userId != null)
			{
				snapshotQuery = snapshotQuery.Where(snapshot => snapshot.UserId == userId);
			}

			if (groupId.HasValue)
			{
				snapshotQuery = snapshotQuery.Where(snapshot => snapshot.GroupId == groupId);
			}

			if (latestActivityAt.HasValue)
			{
				snapshotQuery = snapshotQuery.Where(snapshot => snapshot.CreatedAt < latestActivityAt.Value);
			}

			var snapshot = await snapshotQuery
				.OrderByDescending(snapshot => snapshot.CreatedAt)
				.FirstOrDefaultAsync();

			var previousScores = ParseSnapshotGapScores(snapshot?.TopKnowledgeGapsJson);
			if (previousScores.Count == 0)
			{
				return;
			}

			foreach (var gap in currentGaps)
			{
				if (gap.PreviousGapScore.HasValue ||
					!previousScores.TryGetValue(gap.KnowledgeAspectId, out var previousScore))
				{
					continue;
				}

				gap.PreviousGapScore = previousScore;
				gap.GapScoreDelta = Math.Round(gap.GapScore - previousScore, 2);
				gap.Trend = BuildTrend(gap.GapScoreDelta.Value);
			}
		}

		private static Dictionary<int, double> ParseSnapshotGapScores(string? topKnowledgeGapsJson)
		{
			if (string.IsNullOrWhiteSpace(topKnowledgeGapsJson))
			{
				return new Dictionary<int, double>();
			}

			try
			{
				var gaps = JsonSerializer.Deserialize<List<TopKnowledgeGapDTO>>(topKnowledgeGapsJson);
				return gaps?
					.GroupBy(gap => gap.KnowledgeAspectId)
					.ToDictionary(
						group => group.Key,
						group => group.First().AverageGapScore) ??
					new Dictionary<int, double>();
			}
			catch (JsonException)
			{
				return new Dictionary<int, double>();
			}
		}

		private static StudentGroupStatisticsDTO BuildStudentGroupStatistics(
			string userId,
			string? userName,
			string? fullName,
			IEnumerable<Models.ErrorRecord> groupErrors,
			IEnumerable<Models.KnowledgeGap> groupGaps)
		{
			var studentErrors = groupErrors
				.Where(error => ErrorBelongsToUser(error, userId))
				.ToList();
			var studentGaps = groupGaps
				.Where(gap => gap.UserId == userId)
				.ToList();

			var learningProgress = BuildLearningProgress(studentGaps);

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
				TopKnowledgeGaps = BuildTopKnowledgeGaps(studentGaps),
				CurrentAverageGapScore = learningProgress.CurrentAverageGapScore,
				PreviousAverageGapScore = learningProgress.PreviousAverageGapScore,
				AverageGapScoreDelta = learningProgress.AverageGapScoreDelta,
				TrendSummary = learningProgress.TrendSummary,
				LearningProgress = learningProgress
			};
		}

		private static bool ErrorBelongsToUser(Models.ErrorRecord error, string userId)
		{
			return error.AnalysisRun?.UserId == userId ||
				error.AlphaBeta?.UserId == userId ||
				error.FifteenPuzzle?.UserId == userId;
		}

		private static List<TopErrorTypeDTO> BuildTopErrorTypes(IEnumerable<Models.ErrorRecord> errors)
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

		private static List<TopKnowledgeGapDTO> BuildTopKnowledgeGaps(IEnumerable<Models.KnowledgeGap> gaps)
		{
			return gaps
				.Where(g => g.KnowledgeAspect != null)
				.GroupBy(g => new
				{
					g.KnowledgeAspectId,
					AspectName = g.KnowledgeAspect.Name,
					g.KnowledgeAspect.TopicName
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

		private static double CalculateAverageGapScore(IReadOnlyCollection<Models.KnowledgeGap> gaps)
		{
			return gaps.Count == 0
				? 0
				: Math.Round(gaps.Average(g => g.GapScore), 2);
		}

		private static LearningProgressDTO BuildLearningProgress(IReadOnlyCollection<Models.KnowledgeGap> gaps)
		{
			var progressItems = gaps
				.Where(gap => gap.KnowledgeAspect != null)
				.GroupBy(gap => new
				{
					gap.KnowledgeAspectId,
					AspectName = gap.KnowledgeAspect.Name,
					gap.KnowledgeAspect.TopicName
				})
				.Select(group =>
				{
					var current = group.Average(gap => gap.GapScore);
					var previousValues = group
						.Where(gap => gap.PreviousGapScore.HasValue)
						.Select(gap => gap.PreviousGapScore!.Value)
						.ToList();
					double? previous = previousValues.Count == 0
						? null
						: previousValues.Average();
					var deltaValues = group
						.Where(gap => gap.GapScoreDelta.HasValue)
						.Select(gap => gap.GapScoreDelta!.Value)
						.ToList();
					var delta = deltaValues.Count > 0
						? deltaValues.Average()
						: previous.HasValue
							? current - previous.Value
							: 0;

					return new KnowledgeGapProgressDTO
					{
						KnowledgeAspectId = group.Key.KnowledgeAspectId,
						AspectName = group.Key.AspectName,
						Topic = group.Key.TopicName,
						PreviousGapScore = previous.HasValue ? Math.Round(previous.Value, 2) : null,
						CurrentGapScore = Math.Round(current, 2),
						GapScoreDelta = Math.Round(delta, 2),
						Trend = GetDominantTrend(group),
						Level = GetDominantLevel(group)
					};
				})
				.OrderByDescending(item => Math.Abs(item.GapScoreDelta))
				.ThenByDescending(item => item.CurrentGapScore)
				.ToList();

			var previousScores = gaps
				.Where(gap => gap.PreviousGapScore.HasValue)
				.Select(gap => gap.PreviousGapScore!.Value)
				.ToList();
			var currentAverage = gaps.Count == 0 ? 0 : gaps.Average(gap => gap.GapScore);
			double? previousAverage = previousScores.Count == 0
				? null
				: previousScores.Average();
			var averageDelta = previousAverage.HasValue
				? currentAverage - previousAverage.Value
				: 0;

			return new LearningProgressDTO
			{
				CurrentAverageGapScore = Math.Round(currentAverage, 2),
				PreviousAverageGapScore = previousAverage.HasValue ? Math.Round(previousAverage.Value, 2) : null,
				AverageGapScoreDelta = Math.Round(averageDelta, 2),
				ImprovedGapsCount = gaps.Count(IsImprovedGap),
				WorsenedGapsCount = gaps.Count(IsWorsenedGap),
				StableGapsCount = gaps.Count(IsStableGap),
				NewGapsCount = gaps.Count(IsNewGap),
				TrendSummary = BuildTrendSummary(averageDelta, previousAverage.HasValue, gaps.Count),
				GapsProgress = progressItems
			};
		}

		private static bool IsImprovedGap(Models.KnowledgeGap gap)
		{
			return IsTrend(gap.Trend, "Improved") ||
				(gap.GapScoreDelta.HasValue && gap.GapScoreDelta.Value < 0);
		}

		private static bool IsWorsenedGap(Models.KnowledgeGap gap)
		{
			return IsTrend(gap.Trend, "Worsened") ||
				(gap.GapScoreDelta.HasValue && gap.GapScoreDelta.Value > 0);
		}

		private static bool IsNewGap(Models.KnowledgeGap gap)
		{
			return IsTrend(gap.Trend, "New") || !gap.PreviousGapScore.HasValue;
		}

		private static bool IsStableGap(Models.KnowledgeGap gap)
		{
			return !IsImprovedGap(gap) && !IsWorsenedGap(gap) && !IsNewGap(gap);
		}

		private static bool IsTrend(string? trend, string expected)
		{
			return string.Equals(trend, expected, StringComparison.OrdinalIgnoreCase);
		}

		private static string BuildTrend(double gapScoreDelta)
		{
			if (gapScoreDelta <= -5)
			{
				return "Improved";
			}

			if (gapScoreDelta >= 5)
			{
				return "Worsened";
			}

			return "Stable";
		}

		private static string BuildTrendSummary(double averageDelta, bool hasPreviousData, int gapsCount)
		{
			if (gapsCount == 0 || !hasPreviousData)
			{
				return "InsufficientData";
			}

			if (averageDelta < -0.01)
			{
				return "Improved";
			}

			if (averageDelta > 0.01)
			{
				return "Worsened";
			}

			return "Stable";
		}

		private static string GetDominantTrend(IEnumerable<Models.KnowledgeGap> gaps)
		{
			return gaps
				.Select(gap => string.IsNullOrWhiteSpace(gap.Trend) ? "Stable" : gap.Trend)
				.GroupBy(trend => trend, StringComparer.OrdinalIgnoreCase)
				.OrderByDescending(group => group.Count())
				.Select(group => group.Key)
				.FirstOrDefault() ?? "Stable";
		}

		private static string GetDominantLevel(IEnumerable<Models.KnowledgeGap> gaps)
		{
			return gaps
				.Select(gap => string.IsNullOrWhiteSpace(gap.Level) ? "Low" : gap.Level)
				.OrderByDescending(GetLevelRank)
				.FirstOrDefault() ?? "Low";
		}

		private static int GetLevelRank(string? level)
		{
			return level?.ToLowerInvariant() switch
			{
				"critical" => 4,
				"high" => 3,
				"medium" => 2,
				_ => 1
			};
		}

		private async Task<AnalyticsSnapshot?> GetLatestSnapshotForPeriodAsync(
			string scopeType,
			string? userId,
			int? groupId,
			DateTime? from,
			DateTime? to)
		{
			var query = _context.AnalyticsSnapshots
				.AsNoTracking()
				.Where(s => s.ScopeType == scopeType);

			if (!string.IsNullOrWhiteSpace(userId))
			{
				query = query.Where(s => s.UserId == userId);
			}

			if (groupId.HasValue)
			{
				query = query.Where(s => s.GroupId == groupId);
			}

			query = ApplyPeriod(query, from, to);

			return await query
				.OrderByDescending(s => s.CreatedAt)
				.FirstOrDefaultAsync();
		}

		private static IQueryable<AnalyticsSnapshot> ApplyPeriod(
			IQueryable<AnalyticsSnapshot> query,
			DateTime? from,
			DateTime? to)
		{
			var (fromUtc, toUtc) = BuildUtcPeriod(from, to);

			if (fromUtc.HasValue)
			{
				query = query.Where(s => s.CreatedAt >= fromUtc.Value);
			}

			if (toUtc.HasValue)
			{
				query = query.Where(s => s.CreatedAt <= toUtc.Value);
			}

			return query;
		}

		private static (DateTime? FromUtc, DateTime? ToUtc) BuildUtcPeriod(DateTime? from, DateTime? to)
		{
			DateTime? fromUtc = null;
			DateTime? toUtc = null;

			if (from.HasValue)
			{
				fromUtc = ConvertComparisonDateToUtc(from.Value.Date);
			}

			if (to.HasValue)
			{
				toUtc = ConvertComparisonDateToUtc(to.Value.Date.AddDays(1).AddTicks(-1));
			}

			return (fromUtc, toUtc);
		}

		private static DateTime ConvertComparisonDateToUtc(DateTime comparisonDate)
		{
			return DateTime.SpecifyKind(comparisonDate, DateTimeKind.Unspecified)
				.AddHours(-ComparisonTimeOffsetHours);
		}

		private static AnalyticsCompareDTO BuildCompareResult(AnalyticsSnapshot? before, AnalyticsSnapshot? after)
		{
			if (before == null || after == null)
			{
				throw new InvalidOperationException("Недостаточно данных для сравнения выбранных периодов");
			}

			var averageGapScoreDifference = Math.Round(after.AverageGapScore - before.AverageGapScore, 2);

			return new AnalyticsCompareDTO
			{
				Before = MapComparePeriod(before),
				After = MapComparePeriod(after),
				Difference = new AnalyticsCompareDifferenceDTO
				{
					TotalErrors = after.TotalErrors - before.TotalErrors,
					TotalKnowledgeGaps = after.TotalKnowledgeGaps - before.TotalKnowledgeGaps,
					AverageGapScore = averageGapScoreDifference,
					HighSeverityErrorsCount = after.HighSeverityErrorsCount - before.HighSeverityErrorsCount
				},
				Interpretation = averageGapScoreDifference < 0
					? "Показатель проблемности снизился"
					: averageGapScoreDifference > 0
						? "Показатель проблемности увеличился"
						: "Существенных изменений не выявлено"
			};
		}

		private static AnalyticsComparePeriodDTO MapComparePeriod(AnalyticsSnapshot snapshot)
		{
			return new AnalyticsComparePeriodDTO
			{
				TotalErrors = snapshot.TotalErrors,
				TotalKnowledgeGaps = snapshot.TotalKnowledgeGaps,
				AverageGapScore = snapshot.AverageGapScore,
				HighSeverityErrorsCount = snapshot.HighSeverityErrorsCount,
				CreatedAt = snapshot.CreatedAt
			};
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

		private async Task SaveGlobalSnapshotAsync(
			AnalyticsSummaryDTO summary,
			List<TopErrorTypeDTO> topErrorTypes,
			List<TopKnowledgeGapDTO> topKnowledgeGaps)
		{
			_context.AnalyticsSnapshots.Add(new AnalyticsSnapshot
			{
				ScopeType = "Global",
				TotalStudents = summary.TotalStudents,
				TotalGroups = summary.TotalGroups,
				TotalErrors = summary.TotalErrors,
				TotalKnowledgeGaps = summary.TotalKnowledgeGaps,
				AverageGapScore = summary.AverageGapScore,
				HighSeverityErrorsCount = summary.HighSeverityErrorsCount,
				TopErrorTypesJson = JsonSerializer.Serialize(topErrorTypes),
				TopKnowledgeGapsJson = JsonSerializer.Serialize(topKnowledgeGaps),
				CreatedAt = DateTime.UtcNow
			});

			await _context.SaveChangesAsync();
		}

		private async Task SaveStudentSnapshotAsync(StudentAnalyticsDTO analytics)
		{
			_context.AnalyticsSnapshots.Add(new AnalyticsSnapshot
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
				CreatedAt = DateTime.UtcNow
			});

			await _context.SaveChangesAsync();
		}

		private async Task SaveGroupSnapshotAsync(GroupAnalyticsDTO analytics)
		{
			_context.AnalyticsSnapshots.Add(new AnalyticsSnapshot
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
				CreatedAt = DateTime.UtcNow
			});

			await _context.SaveChangesAsync();
		}

		private static string? BuildFullName(params string?[] parts)
		{
			var fullName = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

			return string.IsNullOrWhiteSpace(fullName)
				? null
				: fullName;
		}
	}
}
