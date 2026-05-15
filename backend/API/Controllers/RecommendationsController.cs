using AICourseTester.DTO;
using AICourseTester.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AICourseTester.Controllers
{
	[Authorize]
	[Route("api/[controller]")]
	[ApiController]
	public class RecommendationsController : ControllerBase
	{
		private readonly IRecommendationService _recommendationService;

		public RecommendationsController(IRecommendationService recommendationService)
		{
			_recommendationService = recommendationService;
		}

		[HttpGet("Students/{userId}")]
		public async Task<ActionResult<List<RecommendationDTO>>> GetStudentRecommendations(string userId)
		{
			var recommendations = await _recommendationService.GetStudentRecommendationsAsync(userId);

			if (recommendations == null)
			{
				return NotFound();
			}

			return Ok(recommendations);
		}

		[HttpGet("Students/{userId}/History")]
		public async Task<ActionResult<List<GeneratedRecommendationDTO>>> GetStudentRecommendationHistory(string userId)
		{
			var recommendations = await _recommendationService.GetStudentRecommendationHistoryAsync(userId);

			if (recommendations == null)
			{
				return NotFound();
			}

			return Ok(recommendations);
		}

		[HttpGet("Groups/{groupId:int}")]
		public async Task<ActionResult<List<RecommendationDTO>>> GetGroupRecommendations(int groupId)
		{
			var recommendations = await _recommendationService.GetGroupRecommendationsAsync(groupId);

			if (recommendations == null)
			{
				return NotFound();
			}

			return Ok(recommendations);
		}

		[HttpGet("Groups/{groupId:int}/History")]
		public async Task<ActionResult<List<GeneratedRecommendationDTO>>> GetGroupRecommendationHistory(int groupId)
		{
			var recommendations = await _recommendationService.GetGroupRecommendationHistoryAsync(groupId);

			if (recommendations == null)
			{
				return NotFound();
			}

			return Ok(recommendations);
		}
	}
}
