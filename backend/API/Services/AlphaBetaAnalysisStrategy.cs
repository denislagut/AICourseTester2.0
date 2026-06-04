using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using NuGet.Protocol;

namespace AICourseTester.Services.Analysis
{
	public class AlphaBetaAnalysisStrategy : ITaskAnalysisStrategy
	{
		private readonly MainDbContext _context;
		private readonly IAlphaBetaErrorAnalysisService _alphaBetaErrorAnalysisService;

		public string TaskType => "AlphaBeta";

		public AlphaBetaAnalysisStrategy(
			MainDbContext context,
			IAlphaBetaErrorAnalysisService alphaBetaErrorAnalysisService)
		{
			_context = context;
			_alphaBetaErrorAnalysisService = alphaBetaErrorAnalysisService;
		}

		public async Task<ITaskAnalysisResult> AnalyzeAsync(
			int taskId,
			string userId,
			CancellationToken cancellationToken = default)
		{
			var task = await _context.AlphaBeta
				.FirstOrDefaultAsync(
					t => t.Id == taskId && t.UserId == userId,
					cancellationToken);

			if (task == null)
			{
				throw new InvalidOperationException("Задание AlphaBeta не найдено.");
			}

			if (task.Problem == null || task.UserSolution == null || task.Solution == null)
			{
				throw new InvalidOperationException("Недостаточно данных для анализа задания AlphaBeta.");
			}

			var problem = task.Problem.FromJson<ProblemTree<ABNode>>();

			var userSolution = new AlphaBetaSolutionDTO
			{
				Nodes = task.UserSolution.FromJson<List<ABNodeDTO>>(),
				Path = task.UserPath == null
					? Array.Empty<int>()
					: task.UserPath.FromJson<int[]>(),
				PrunedNodeIds = task.UserPrunedNodeIds == null
					? Array.Empty<int>()
					: task.UserPrunedNodeIds.FromJson<int[]>()
			};

			var correctSolution = new AlphaBetaSolutionDTO
			{
				Nodes = task.Solution.FromJson<List<ABNodeDTO>>(),
				Path = task.Path == null
					? Array.Empty<int>()
					: task.Path.FromJson<int[]>()
			};

			return _alphaBetaErrorAnalysisService.Analyze(
				problem,
				userSolution,
				correctSolution);
		}
	}
}