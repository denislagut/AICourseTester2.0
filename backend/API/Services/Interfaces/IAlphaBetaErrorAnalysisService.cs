using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;

namespace AICourseTester.Services.Interfaces
{
	public interface IAlphaBetaErrorAnalysisService
	{
		ErrorAnalysisResult Analyze(
			ProblemTree<ABNode> problem,
			AlphaBetaSolutionDTO userSolution,
			AlphaBetaSolutionDTO correctSolution);
	}
}