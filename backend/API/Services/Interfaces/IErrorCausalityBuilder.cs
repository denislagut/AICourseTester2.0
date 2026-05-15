using AICourseTester.Models;
using AICourseTester.Models.Analysis;

namespace AICourseTester.Services.Interfaces
{
	public interface IErrorCausalityBuilder
	{
		List<CausalErrorLink> Build(List<ErrorRecord> errors);
	}
}