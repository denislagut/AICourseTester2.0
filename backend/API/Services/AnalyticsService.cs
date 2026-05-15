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
			var studentRoleId = await _context.Roles
				.AsNoTracking()
				.Where(r => r.Name == "Student")
				.Select(r => r.Id)
				.FirstOrDefaultAsync();

			var totalStudents = string.IsNullOrEmpty(studentRoleId)
				? 0
				: await _context.UserRoles
					.AsNoTracking()
					.CountAsync(ur => ur.RoleId == studentRoleId);

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
	}
}
