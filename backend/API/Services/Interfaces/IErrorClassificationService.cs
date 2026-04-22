namespace AICourseTester.Services.Interfaces
{
	public interface IErrorClassificationService
	{
		Task ClassifyErrorsAsync(int alphaBetaId);
	}
}