namespace AICourseTester.DTO
{
	public class TeacherTaskHistoryDTO
	{
		public int Id { get; set; }
		public int? TaskId { get; set; }
		public string TaskType { get; set; } = null!;
		public string TaskName { get; set; } = null!;
		public string UserId { get; set; } = null!;
		public string UserName { get; set; } = null!;
		public int? GroupId { get; set; }
		public string? GroupName { get; set; }
		public DateTime Date { get; set; }
		public string Status { get; set; } = "Проверено";
		public bool IsSolved { get; set; } = true;
		public bool CanOpen { get; set; }
	}
}
