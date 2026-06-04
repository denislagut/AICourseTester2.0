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

		[HttpGet("Snapshots/Students/{userId}/Period")]
		public async Task<ActionResult<List<AnalyticsSnapshotDTO>>> GetStudentSnapshotsForPeriod(
			string userId,
			[FromQuery] DateTime? from,
			[FromQuery] DateTime? to)
		{
			var snapshots = await _analyticsService.GetStudentSnapshotsForPeriodAsync(userId, from, to);

			if (snapshots == null)
			{
				return NotFound();
			}

			return Ok(snapshots);
		}

		[HttpGet("Snapshots/Groups/{groupId:int}/Period")]
		public async Task<ActionResult<List<AnalyticsSnapshotDTO>>> GetGroupSnapshotsForPeriod(
			int groupId,
			[FromQuery] DateTime? from,
			[FromQuery] DateTime? to)
		{
			var snapshots = await _analyticsService.GetGroupSnapshotsForPeriodAsync(groupId, from, to);

			if (snapshots == null)
			{
				return NotFound();
			}

			return Ok(snapshots);
		}

		[HttpGet("Students/{userId}/Compare")]
		public async Task<ActionResult<AnalyticsCompareDTO>> CompareStudentPeriods(
			string userId,
			[FromQuery] DateTime? beforeFrom,
			[FromQuery] DateTime? beforeTo,
			[FromQuery] DateTime? afterFrom,
			[FromQuery] DateTime? afterTo)
		{
			try
			{
				var comparison = await _analyticsService.CompareStudentPeriodsAsync(userId, beforeFrom, beforeTo, afterFrom, afterTo);

				if (comparison == null)
				{
					return NotFound();
				}

				return Ok(comparison);
			}
			catch (InvalidOperationException exception)
			{
				return BadRequest(exception.Message);
			}
		}

		[HttpGet("Groups/{groupId:int}/Compare")]
		public async Task<ActionResult<AnalyticsCompareDTO>> CompareGroupPeriods(
			int groupId,
			[FromQuery] DateTime? beforeFrom,
			[FromQuery] DateTime? beforeTo,
			[FromQuery] DateTime? afterFrom,
			[FromQuery] DateTime? afterTo)
		{
			try
			{
				var comparison = await _analyticsService.CompareGroupPeriodsAsync(groupId, beforeFrom, beforeTo, afterFrom, afterTo);

				if (comparison == null)
				{
					return NotFound();
				}

				return Ok(comparison);
			}
			catch (InvalidOperationException exception)
			{
				return BadRequest(exception.Message);
			}
		}
	}
}
