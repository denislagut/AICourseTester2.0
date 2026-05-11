using AICourseTester.Models;

namespace AICourseTester.Services.Interfaces
{
	public interface IKnowledgeGapDetectionService
	{
		Task<List<KnowledgeGap>> DetectForAlphaBetaAsync(int alphaBetaId, string userId);
		Task<List<KnowledgeGap>> DetectForFifteenPuzzleAsync(int fifteenPuzzleId, string userId);
	}
}