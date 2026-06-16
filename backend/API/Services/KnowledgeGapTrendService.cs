using AICourseTester.Data;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AICourseTester.Services
{
	public class KnowledgeGapTrendService : IKnowledgeGapTrendService
	{
		private readonly MainDbContext _context;

		public KnowledgeGapTrendService(MainDbContext context)
		{
			_context = context;
		}

		public async Task ApplyTrendAsync(
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
						g.TaskTypeId == LookupIds.TaskTypeId(taskType) &&
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
				gap.GapScoreDelta = Math.Round(gap.GapScore - previousGap.GapScore, 2);

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
	}
}