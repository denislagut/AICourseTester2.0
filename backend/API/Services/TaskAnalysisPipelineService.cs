using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services.Analysis;
using AICourseTester.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AICourseTester.Services
{
	public class TaskAnalysisPipelineService : ITaskAnalysisPipelineService
	{
		private readonly MainDbContext _context;
		private readonly IErrorCausalityBuilder _errorCausalityBuilder;
		private readonly IFifteenPuzzleErrorAnalysisService _fifteenPuzzleErrorAnalysisService;
		private readonly IAlphaBetaErrorAnalysisService _alphaBetaErrorAnalysisService;
		private readonly IErrorClassificationService _errorClassificationService;
		private readonly IKnowledgeGapDetectionService _knowledgeGapDetectionService;

		public TaskAnalysisPipelineService(
			MainDbContext context,
			IErrorCausalityBuilder errorCausalityBuilder,
			IFifteenPuzzleErrorAnalysisService fifteenPuzzleErrorAnalysisService,
			IAlphaBetaErrorAnalysisService alphaBetaErrorAnalysisService,
			IErrorClassificationService errorClassificationService,
			IKnowledgeGapDetectionService knowledgeGapDetectionService)
		{
			_context = context;
			_errorCausalityBuilder = errorCausalityBuilder;
			_fifteenPuzzleErrorAnalysisService = fifteenPuzzleErrorAnalysisService;
			_alphaBetaErrorAnalysisService = alphaBetaErrorAnalysisService;
			_errorClassificationService = errorClassificationService;
			_knowledgeGapDetectionService = knowledgeGapDetectionService;
		}

		public async Task<ITaskAnalysisResult> AnalyzeFifteenPuzzleAsync(
			int fifteenPuzzleId,
			string userId,
			List<ANodeDTO> userSolution,
			List<ANodeDTO> correctSolution,
			int heuristic)
		{
			await using var transaction = await _context.Database.BeginTransactionAsync();

			try
			{
				var analysisResult = _fifteenPuzzleErrorAnalysisService.Analyze(
					userSolution,
					correctSolution,
					heuristic);

				await SaveErrorsAsync(
					taskType: "FifteenPuzzle",
					alphaBetaId: null,
					fifteenPuzzleId: fifteenPuzzleId,
					analysisResult: analysisResult);

				await _errorClassificationService.ClassifyFifteenPuzzleErrorsAsync(fifteenPuzzleId);

				await _knowledgeGapDetectionService.DetectForFifteenPuzzleAsync(
					fifteenPuzzleId,
					userId);

				await transaction.CommitAsync();

				return analysisResult;
			}
			catch
			{
				await transaction.RollbackAsync();
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
			await using var transaction = await _context.Database.BeginTransactionAsync();

			try
			{
				var analysisResult = _alphaBetaErrorAnalysisService.Analyze(
					problem,
					userSolution,
					correctSolution);

				await SaveErrorsAsync(
					taskType: "AlphaBeta",
					alphaBetaId: alphaBetaId,
					fifteenPuzzleId: null,
					analysisResult: analysisResult);

				await _errorClassificationService.ClassifyErrorsAsync(alphaBetaId);

				await _knowledgeGapDetectionService.DetectForAlphaBetaAsync(
					alphaBetaId,
					userId);

				await transaction.CommitAsync();

				return analysisResult;
			}
			catch
			{
				await transaction.RollbackAsync();
				throw;
			}
		}

		private async Task SaveErrorsAsync(
			string taskType,
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

			var oldLinks = await _context.CausalErrorLinks
				.Where(l => oldErrorIds.Contains(l.SourceErrorId) ||
							oldErrorIds.Contains(l.TargetErrorId))
				.ToListAsync();

			_context.CausalErrorLinks.RemoveRange(oldLinks);
			_context.ErrorRecords.RemoveRange(oldErrors);

			var errorEntities = analysisResult.Errors.Select(error => new ErrorRecord
			{
				TaskType = taskType,
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

				IsPrimary = error.IsPrimary,
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