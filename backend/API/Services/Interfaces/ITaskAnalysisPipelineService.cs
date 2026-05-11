using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;

namespace AICourseTester.Services.Interfaces
{
	public interface ITaskAnalysisPipelineService
	{
		Task<ErrorAnalysisResult> AnalyzeFifteenPuzzleAsync(
			int fifteenPuzzleId,
			string userId,
			List<ANodeDTO> userSolution,
			List<ANodeDTO> correctSolution,
			int heuristic);

		Task<ErrorAnalysisResult> AnalyzeAlphaBetaAsync(
			int alphaBetaId,
			string userId,
			ProblemTree<ABNode> problem,
			AlphaBetaSolutionDTO userSolution,
			AlphaBetaSolutionDTO correctSolution);
	}
}