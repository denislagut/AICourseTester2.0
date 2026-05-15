using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AICourseTester.Services
{
	public class AnalyticsService : IAnalyticsService
	{
		private const double HighSeverityThreshold = 3.0;
		private const int TopItemsLimit = 10;

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

			var totalKnowledgeGaps = await _context.KnowledgeGaps
				.AsNoTracking()
				.CountAsync();

			var averageGapScore = totalKnowledgeGaps == 0
				? 0
				: await _context.KnowledgeGaps
					.AsNoTracking()
					.AverageAsync(g => g.GapScore);

			return new AnalyticsSummaryDTO
			{
				TotalStudents = totalStudents,
				TotalGroups = await _context.Groups.AsNoTracking().CountAsync(),
				TotalErrors = await _context.ErrorRecords.AsNoTracking().CountAsync(),
				TotalKnowledgeGaps = totalKnowledgeGaps,
				AverageGapScore = Math.Round(averageGapScore, 2),
				HighSeverityErrorsCount = await _context.ErrorRecords
					.AsNoTracking()
					.CountAsync(e => e.SeverityScore >= HighSeverityThreshold)
			};
		}

		public async Task<List<TopErrorTypeDTO>> GetTopErrorTypesAsync()
		{
			var errors = await _context.ErrorRecords
				.AsNoTracking()
				.Where(e => e.ErrorTypeId != null)
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
			var gaps = await _context.KnowledgeGaps
				.AsNoTracking()
				.Include(g => g.KnowledgeAspect)
				.ToListAsync();

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

		public async Task<StudentAnalyticsDTO?> GetStudentAnalyticsAsync(string userId)
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
			var gaps = await LoadKnowledgeGapsForUsersAsync(new[] { userId });

			return new StudentAnalyticsDTO
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
				TopKnowledgeGaps = BuildTopKnowledgeGaps(gaps)
			};
		}

		public async Task<GroupAnalyticsDTO?> GetGroupAnalyticsAsync(int groupId)
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

			var userIds = await _context.UserGroups
				.AsNoTracking()
				.Where(ug => ug.GroupId == groupId)
				.Select(ug => ug.UserId)
				.ToListAsync();

			var errors = userIds.Count == 0
				? new List<Models.ErrorRecord>()
				: await LoadErrorsForUsersAsync(userIds);

			var gaps = userIds.Count == 0
				? new List<Models.KnowledgeGap>()
				: await LoadKnowledgeGapsForUsersAsync(userIds);

			return new GroupAnalyticsDTO
			{
				GroupId = group.Id,
				GroupName = group.Name,
				StudentsCount = userIds.Count,
				TotalErrors = errors.Count,
				TotalKnowledgeGaps = gaps.Count,
				AverageGapScore = CalculateAverageGapScore(gaps),
				HighSeverityErrorsCount = errors.Count(e => e.SeverityScore >= HighSeverityThreshold),
				TopErrorTypes = BuildTopErrorTypes(errors),
				TopKnowledgeGaps = BuildTopKnowledgeGaps(gaps)
			};
		}

		private async Task<List<Models.ErrorRecord>> LoadErrorsForUsersAsync(IReadOnlyCollection<string> userIds)
		{
			return await _context.ErrorRecords
				.AsNoTracking()
				.Include(e => e.ErrorType)
				.Include(e => e.AlphaBeta)
				.Include(e => e.FifteenPuzzle)
				.Where(e =>
					(e.AlphaBeta != null && userIds.Contains(e.AlphaBeta.UserId)) ||
					(e.FifteenPuzzle != null && userIds.Contains(e.FifteenPuzzle.UserId)))
				.ToListAsync();
		}

		private async Task<List<Models.KnowledgeGap>> LoadKnowledgeGapsForUsersAsync(IReadOnlyCollection<string> userIds)
		{
			return await _context.KnowledgeGaps
				.AsNoTracking()
				.Include(g => g.KnowledgeAspect)
				.Where(g => userIds.Contains(g.UserId))
				.ToListAsync();
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

		private static string? BuildFullName(params string?[] parts)
		{
			var fullName = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

			return string.IsNullOrWhiteSpace(fullName)
				? null
				: fullName;
		}
	}
}
