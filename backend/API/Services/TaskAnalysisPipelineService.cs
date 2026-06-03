using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AICourseTester.Services
{
	public class TaskAnalysisPipelineService : ITaskAnalysisPipelineService
	{
		private const string AnalyzerVersion = "1.0";

		private readonly MainDbContext _context;

		private readonly IErrorCausalityBuilder _errorCausalityBuilder;
		private readonly AnalysisStrategyRegistry _strategyRegistry;
		private readonly IFifteenPuzzleErrorAnalysisService _fifteenPuzzleErrorAnalysisService;
		private readonly IAlphaBetaErrorAnalysisService _alphaBetaErrorAnalysisService;
		private readonly IErrorClassificationService _errorClassificationService;
		private readonly IKnowledgeGapDetectionService _knowledgeGapDetectionService;

		public TaskAnalysisPipelineService(
			MainDbContext context,
			IErrorCausalityBuilder errorCausalityBuilder,
			AnalysisStrategyRegistry strategyRegistry,
			IFifteenPuzzleErrorAnalysisService fifteenPuzzleErrorAnalysisService,
			IAlphaBetaErrorAnalysisService alphaBetaErrorAnalysisService,
			IErrorClassificationService errorClassificationService,
			IKnowledgeGapDetectionService knowledgeGapDetectionService)
		{
			_context = context;
			_errorCausalityBuilder = errorCausalityBuilder;
			_strategyRegistry = strategyRegistry;
			_fifteenPuzzleErrorAnalysisService = fifteenPuzzleErrorAnalysisService;
			_alphaBetaErrorAnalysisService = alphaBetaErrorAnalysisService;
			_errorClassificationService = errorClassificationService;
			_knowledgeGapDetectionService = knowledgeGapDetectionService;
		}

		public async Task<ITaskAnalysisResult> AnalyzeAsync(
			string taskType,
			int taskId,
			string userId)
		{
			var strategy = _strategyRegistry.GetStrategy(taskType);

			var analysisRun = await CreateAnalysisRunAsync(
				taskType: taskType,
				userId: userId,
				alphaBetaId: taskType == "AlphaBeta" ? taskId : null,
				fifteenPuzzleId: taskType == "FifteenPuzzle" ? taskId : null);

			await using var transaction = await _context.Database.BeginTransactionAsync();

			try
			{
				var analysisResult = await strategy.AnalyzeAsync(taskId, userId);

				await SaveErrorsAsync(
					taskType: taskType,
					analysisRunId: analysisRun.Id,
					alphaBetaId: taskType == "AlphaBeta" ? taskId : null,
					fifteenPuzzleId: taskType == "FifteenPuzzle" ? taskId : null,
					analysisResult: analysisResult);

				if (taskType == "AlphaBeta")
				{
					await _errorClassificationService.ClassifyErrorsAsync(taskId);

					await _knowledgeGapDetectionService.DetectForAlphaBetaAsync(
						taskId,
						userId,
						analysisRun.Id);
				}
				else if (taskType == "FifteenPuzzle")
				{
					await _errorClassificationService.ClassifyFifteenPuzzleErrorsAsync(taskId);

					await _knowledgeGapDetectionService.DetectForFifteenPuzzleAsync(
						taskId,
						userId,
						analysisRun.Id);
				}

				analysisRun.Status = "Completed";
				analysisRun.CompletedAt = DateTime.UtcNow;

				await _context.SaveChangesAsync();
				await transaction.CommitAsync();

				return analysisResult;
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();

				await MarkAnalysisRunFailedAsync(analysisRun.Id, ex);

				throw;
			}
		}

		public async Task<ITaskAnalysisResult> AnalyzeFifteenPuzzleAsync(
			int fifteenPuzzleId,
			string userId,
			List<ANodeDTO> userSolution,
			List<ANodeDTO> correctSolution,
			int heuristic)
		{
			var analysisRun = await CreateAnalysisRunAsync(
				taskType: "FifteenPuzzle",
				userId: userId,
				alphaBetaId: null,
				fifteenPuzzleId: fifteenPuzzleId);

			await using var transaction = await _context.Database.BeginTransactionAsync();

			try
			{
				var analysisResult = _fifteenPuzzleErrorAnalysisService.Analyze(
					userSolution,
					correctSolution,
					heuristic);

				await SaveErrorsAsync(
					taskType: "FifteenPuzzle",
					analysisRunId: analysisRun.Id,
					alphaBetaId: null,
					fifteenPuzzleId: fifteenPuzzleId,
					analysisResult: analysisResult);

				await _errorClassificationService.ClassifyFifteenPuzzleErrorsAsync(fifteenPuzzleId);

				await _knowledgeGapDetectionService.DetectForFifteenPuzzleAsync(
					fifteenPuzzleId,
					userId,
					analysisRun.Id);

				analysisRun.Status = "Completed";
				analysisRun.CompletedAt = DateTime.UtcNow;

				await _context.SaveChangesAsync();
				await transaction.CommitAsync();

				return analysisResult;
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();

				await MarkAnalysisRunFailedAsync(analysisRun.Id, ex);

				throw;
			}
		}

		public async Task<ITaskAnalysisResult> AnalyzeAlphaBetaAsync(
			int alphaBetaId,
			string userId,
			ProblemTree<ABNode> problem,
			AlphaBetaSolutionDTO userSolution,
			AlphaBetaSolutionDTO correctSolution)
		{
			var analysisRun = await CreateAnalysisRunAsync(
				taskType: "AlphaBeta",
				userId: userId,
				alphaBetaId: alphaBetaId,
				fifteenPuzzleId: null);

			await using var transaction = await _context.Database.BeginTransactionAsync();

			try
			{
				var analysisResult = _alphaBetaErrorAnalysisService.Analyze(
					problem,
					userSolution,
					correctSolution);

				await SaveErrorsAsync(
					taskType: "AlphaBeta",
					analysisRunId: analysisRun.Id,
					alphaBetaId: alphaBetaId,
					fifteenPuzzleId: null,
					analysisResult: analysisResult);

				await _errorClassificationService.ClassifyErrorsAsync(alphaBetaId);

				await _knowledgeGapDetectionService.DetectForAlphaBetaAsync(
					alphaBetaId,
					userId,
					analysisRun.Id);

				analysisRun.Status = "Completed";
				analysisRun.CompletedAt = DateTime.UtcNow;

				await _context.SaveChangesAsync();
				await transaction.CommitAsync();

				return analysisResult;
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();

				await MarkAnalysisRunFailedAsync(analysisRun.Id, ex);

				throw;
			}
		}

		private async Task<AnalysisRun> CreateAnalysisRunAsync(
			string taskType,
			string userId,
			int? alphaBetaId,
			int? fifteenPuzzleId)
		{
			var analysisRun = new AnalysisRun
			{
				TaskType = taskType,
				AlphaBetaId = alphaBetaId,
				FifteenPuzzleId = fifteenPuzzleId,
				UserId = userId,
				StartedAt = DateTime.UtcNow,
				Status = "Started",
				AnalyzerVersion = AnalyzerVersion
			};

			_context.AnalysisRuns.Add(analysisRun);
			await _context.SaveChangesAsync();

			return analysisRun;
		}

		private async Task MarkAnalysisRunFailedAsync(int analysisRunId, Exception exception)
		{
			var analysisRun = await _context.AnalysisRuns
				.FirstOrDefaultAsync(r => r.Id == analysisRunId);

			if (analysisRun == null)
			{
				return;
			}

			analysisRun.Status = "Failed";
			analysisRun.CompletedAt = DateTime.UtcNow;
			analysisRun.ErrorMessage = exception.Message;

			await _context.SaveChangesAsync();
		}

		private async Task SaveErrorsAsync(
			string taskType,
			int analysisRunId,
			int? alphaBetaId,
			int? fifteenPuzzleId,
			ITaskAnalysisResult analysisResult)
		{
			var oldErrorsQuery = _context.ErrorRecords
				.Where(e => e.TaskType == taskType);

			if (alphaBetaId.HasValue)
			{
				oldErrorsQuery = oldErrorsQuery.Where(e => e.AlphaBetaId == alphaBetaId.Value);
			}

			if (fifteenPuzzleId.HasValue)
			{
				oldErrorsQuery = oldErrorsQuery.Where(e => e.FifteenPuzzleId == fifteenPuzzleId.Value);
			}

			var oldErrors = await oldErrorsQuery.ToListAsync();
			var oldErrorIds = oldErrors.Select(e => e.Id).ToList();

			if (oldErrorIds.Count > 0)
			{
				var oldLinks = await _context.CausalErrorLinks
					.Where(l =>
						oldErrorIds.Contains(l.SourceErrorId) ||
						oldErrorIds.Contains(l.TargetErrorId))
					.ToListAsync();

				_context.CausalErrorLinks.RemoveRange(oldLinks);
			}

			_context.ErrorRecords.RemoveRange(oldErrors);

			var errorEntities = analysisResult.Errors.Select(error => new ErrorRecord
			{
				TaskType = taskType,
				AnalysisRunId = analysisRunId,
				AlphaBetaId = alphaBetaId,
				FifteenPuzzleId = fifteenPuzzleId,

				Code = error.Code,
				Message = error.Message,
				NodeId = error.NodeId,
				TreeLevel = error.TreeLevel,
				ElementType = error.ElementType,

				ExpectedA = error.ExpectedA,
				ActualA = error.ActualA,
				ExpectedB = error.ExpectedB,
				ActualB = error.ActualB,

				PathStepIndex = error.PathStepIndex,
				ExpectedPathNodeId = error.ExpectedPathNodeId,
				ActualPathNodeId = error.ActualPathNodeId,

				IsPrimary =
					!ErrorCodes.IsSummary(error.Code) &&
					!ErrorCodes.IsDerived(error.Code) &&
					error.IsPrimary,

				IsSummary = ErrorCodes.IsSummary(error.Code),
				SeverityScore = error.SeverityScore,
				GroupKey = error.GroupKey,
				CreatedAt = DateTime.UtcNow,

				RootBranchId = error.RootBranchId,
				IsOnCorrectPath = error.IsOnCorrectPath,
				IsUserPruned = error.IsUserPruned,
				IsExpectedPruned = error.IsExpectedPruned,

				PatternType = error.PatternType,
				SimilarErrorCount = error.SimilarErrorCount,
				SimilarOpportunityCount = error.SimilarOpportunityCount,
				SimilarErrorRatio = error.SimilarErrorRatio
			}).ToList();

			await _context.ErrorRecords.AddRangeAsync(errorEntities);
			await _context.SaveChangesAsync();

			var links = _errorCausalityBuilder.Build(errorEntities);

			await _context.CausalErrorLinks.AddRangeAsync(links);
			await _context.SaveChangesAsync();
		}
	}
}