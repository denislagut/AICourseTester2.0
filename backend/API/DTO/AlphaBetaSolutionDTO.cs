using AICourseTester.Models;

namespace AICourseTester.DTO
{
	public class AlphaBetaSolutionDTO
	{
		public List<ABNodeDTO>? Nodes { get; set; }
		public int[]? Path { get; set; }

		// Узлы, в которые ведут явно отсечённые пользователем ветви
		public int[]? PrunedNodeIds { get; set; }
	}
}