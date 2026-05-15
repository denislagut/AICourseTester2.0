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
	}
}
