using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AICourseTester.Controllers
{
	[Authorize(AuthenticationSchemes = "Identity.Bearer")]
	[Route("api/Analytics")]
	[ApiController]
	public class StudentAnalyticsController : ControllerBase
	{
		private readonly IAnalyticsService _analyticsService;
		private readonly UserManager<ApplicationUser> _userManager;

		public StudentAnalyticsController(
			IAnalyticsService analyticsService,
			UserManager<ApplicationUser> userManager)
		{
			_analyticsService = analyticsService;
			_userManager = userManager;
		}

		[HttpGet("Me")]
		public async Task<ActionResult<StudentAnalyticsDTO>> GetMyAnalytics()
		{
			var userId = _userManager.GetUserId(User);

			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized();
			}

			var analytics = await _analyticsService.GetStudentAnalyticsAsync(userId);

			if (analytics == null)
			{
				return NotFound();
			}

			return Ok(analytics);
		}
	}
}
