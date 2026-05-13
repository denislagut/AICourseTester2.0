using System.Text.Json.Serialization;

namespace AICourseTester.Models
{
	public class AlphaBeta
	{
		public int Id { get; set; }

		[JsonIgnore]
		public string UserId { get; set; } = null!;

		[JsonIgnore]
		public ApplicationUser User { get; set; } = null!;

		public string? Problem { get; set; }
		public string? Solution { get; set; }
		public string? Path { get; set; }

		public string? UserPrunedNodeIds { get; set; }
		public string? UserSolution { get; set; }
		public string? UserPath { get; set; }

		public int TreeHeight { get; set; } = 3;
		public int? MaxValue { get; set; }
		public int? Template { get; set; }
		public bool IsSolved { get; set; } = false;

		public DateTime Date { get; set; }

		[JsonIgnore]
		public List<ErrorRecord> Errors { get; set; } = new();
	}
}