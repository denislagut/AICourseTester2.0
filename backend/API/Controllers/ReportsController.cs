using AICourseTester.DTO;
using AICourseTester.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AICourseTester.Controllers
{
	[Authorize(Roles = "Administrator")]
	[Route("api/[controller]")]
	[ApiController]
	public class ReportsController : ControllerBase
	{
		private readonly IReportsService _reportsService;
		private readonly IReportExportService _reportExportService;

		public ReportsController(IReportsService reportsService, IReportExportService reportExportService)
		{
			_reportsService = reportsService;
			_reportExportService = reportExportService;
		}

		[HttpPost("Students/{userId}/Generate")]
		public async Task<ActionResult<GeneratedReportDTO>> GenerateStudentReport(
			string userId,
			[FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] GenerateReportRequestDTO? request = null)
		{
			try
			{
				var report = await _reportsService.GenerateStudentReportAsync(userId, request);

				if (report == null)
				{
					return NotFound();
				}

				return Ok(report);
			}
			catch (InvalidOperationException ex)
			{
				return BadRequest(ex.Message);
			}
		}

		[HttpPost("Groups/{groupId:int}/Generate")]
		public async Task<ActionResult<GeneratedReportDTO>> GenerateGroupReport(
			int groupId,
			[FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] GenerateReportRequestDTO? request = null)
		{
			try
			{
				var report = await _reportsService.GenerateGroupReportAsync(groupId, request);

				if (report == null)
				{
					return NotFound();
				}

				return Ok(report);
			}
			catch (InvalidOperationException ex)
			{
				return BadRequest(ex.Message);
			}
		}

		[HttpGet("Students/{userId}/History")]
		public async Task<ActionResult<List<GeneratedReportDTO>>> GetStudentReportsHistory(string userId)
		{
			var reports = await _reportsService.GetStudentReportsHistoryAsync(userId);

			if (reports == null)
			{
				return NotFound();
			}

			return Ok(reports);
		}

		[HttpGet("Groups/{groupId:int}/History")]
		public async Task<ActionResult<List<GeneratedReportDTO>>> GetGroupReportsHistory(int groupId)
		{
			var reports = await _reportsService.GetGroupReportsHistoryAsync(groupId);

			if (reports == null)
			{
				return NotFound();
			}

			return Ok(reports);
		}

		[HttpGet("{reportId:int}/Export/Pdf")]
		public async Task<IActionResult> ExportPdf(int reportId)
		{
			var file = await _reportExportService.ExportPdfAsync(reportId);

			if (file == null)
			{
				return NotFound();
			}

			return File(file, "application/pdf", $"report-{reportId}.pdf");
		}

		[HttpGet("{reportId:int}/Export/Excel")]
		public async Task<IActionResult> ExportExcel(int reportId)
		{
			var file = await _reportExportService.ExportExcelAsync(reportId);

			if (file == null)
			{
				return NotFound();
			}

			return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"report-{reportId}.xlsx");
		}
	}
}
