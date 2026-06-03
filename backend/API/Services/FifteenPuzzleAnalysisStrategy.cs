using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using NuGet.Protocol;

namespace AICourseTester.Services.Analysis
{
	public class FifteenPuzzleAnalysisStrategy : ITaskAnalysisStrategy
	{
		private readonly MainDbContext _context;
		private readonly IFifteenPuzzleErrorAnalysisService _fifteenPuzzleErrorAnalysisService;

		public string TaskType => "FifteenPuzzle";

		public FifteenPuzzleAnalysisStrategy(
			MainDbContext context,
			IFifteenPuzzleErrorAnalysisService fifteenPuzzleErrorAnalysisService)
		{
			_context = context;
			_fifteenPuzzleErrorAnalysisService = fifteenPuzzleErrorAnalysisService;
		}

		public async Task<ITaskAnalysisResult> AnalyzeAsync(
			int taskId,
			string userId,
			CancellationToken cancellationToken = default)
		{
			var task = await _context.Fifteens
				.FirstOrDefaultAsync(
					t => t.Id == taskId && t.UserId == userId,
					cancellationToken);

			if (task == null)
			{
				throw new InvalidOperationException("Задание FifteenPuzzle не найдено.");
			}

			if (task.UserSolution == null || task.Solution == null)
			{
				throw new InvalidOperationException("Недостаточно данных для анализа задания FifteenPuzzle.");
			}

			var userSolution = task.UserSolution.FromJson<List<ANodeDTO>>();
			var correctSolution = task.Solution.FromJson<List<ANodeDTO>>();

			return _fifteenPuzzleErrorAnalysisService.Analyze(
				userSolution,
				correctSolution,
				task.Heuristic ?? 0);
		}
	}
}