using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;

namespace AICourseTester.Services.Interfaces
{
	public interface ITaskAnalysisPipelineService
	{
		Task<ITaskAnalysisResult> AnalyzeAsync(
		string taskType,
		int taskId,
		string userId);

		Task<ITaskAnalysisResult> AnalyzeFifteenPuzzleAsync(
			int fifteenPuzzleId,
			string userId,
			List<ANodeDTO> userSolution,
			List<ANodeDTO> correctSolution,
			int heuristic);

		Task<ITaskAnalysisResult> AnalyzeAlphaBetaAsync(
			int alphaBetaId,
			string userId,
			ProblemTree<ABNode> problem,
			AlphaBetaSolutionDTO userSolution,
			AlphaBetaSolutionDTO correctSolution);
	}
}