using AICourseTester.DTO;
using AICourseTester.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AICourseTester.Controllers
{
	[Authorize]
	[Route("api/[controller]")]
	[ApiController]
	public class AnalyticsController : ControllerBase
	{
		private readonly IAnalyticsService _analyticsService;

		public AnalyticsController(IAnalyticsService analyticsService)
		{
			_analyticsService = analyticsService;
		}

		[HttpGet("Summary")]
		public async Task<ActionResult<AnalyticsSummaryDTO>> GetSummary()
		{
			return Ok(await _analyticsService.GetSummaryAsync());
		}

		[HttpGet("TopErrorTypes")]
		public async Task<ActionResult<List<TopErrorTypeDTO>>> GetTopErrorTypes()
		{
			return Ok(await _analyticsService.GetTopErrorTypesAsync());
		}

		[HttpGet("TopKnowledgeGaps")]
		public async Task<ActionResult<List<TopKnowledgeGapDTO>>> GetTopKnowledgeGaps()
		{
			return Ok(await _analyticsService.GetTopKnowledgeGapsAsync());
		}

		[HttpGet("Students/{userId}")]
		public async Task<ActionResult<StudentAnalyticsDTO>> GetStudentAnalytics(string userId)
		{
			var analytics = await _analyticsService.GetStudentAnalyticsAsync(userId);

			if (analytics == null)
			{
				return NotFound();
			}

			return Ok(analytics);
		}

		[HttpGet("Groups/{groupId:int}")]
		public async Task<ActionResult<GroupAnalyticsDTO>> GetGroupAnalytics(int groupId)
		{
			var analytics = await _analyticsService.GetGroupAnalyticsAsync(groupId);

			if (analytics == null)
			{
				return NotFound();
			}

			return Ok(analytics);
		}

		[HttpGet("Snapshots/Global")]
		public async Task<ActionResult<List<AnalyticsSnapshotDTO>>> GetGlobalSnapshots()
		{
			return Ok(await _analyticsService.GetGlobalSnapshotsAsync());
		}

		[HttpGet("Snapshots/Students/{userId}")]
		public async Task<ActionResult<List<AnalyticsSnapshotDTO>>> GetStudentSnapshots(string userId)
		{
			var snapshots = await _analyticsService.GetStudentSnapshotsAsync(userId);

			if (snapshots == null)
			{
				return NotFound();
			}

			return Ok(snapshots);
		}

		[HttpGet("Snapshots/Groups/{groupId:int}")]
		public async Task<ActionResult<List<AnalyticsSnapshotDTO>>> GetGroupSnapshots(int groupId)
		{
			var snapshots = await _analyticsService.GetGroupSnapshotsAsync(groupId);

			if (snapshots == null)
			{
				return NotFound();
			}

			return Ok(snapshots);
		}
	}
}
