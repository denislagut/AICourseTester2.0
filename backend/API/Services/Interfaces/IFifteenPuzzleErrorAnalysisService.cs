using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;

namespace AICourseTester.Services.Interfaces
{
	public interface IFifteenPuzzleErrorAnalysisService
	{
		ErrorAnalysisResult Analyze(
			List<ANodeDTO> userSolution,
			List<ANodeDTO> correctSolution,
			int heuristic);
	}
}