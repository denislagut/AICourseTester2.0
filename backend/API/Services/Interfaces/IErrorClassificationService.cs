namespace AICourseTester.Services.Interfaces
{
	public interface IErrorClassificationService
	{
		Task ClassifyErrorsAsync(int alphaBetaId);
		Task ClassifyFifteenPuzzleErrorsAsync(int fifteenPuzzleId);
	}
}