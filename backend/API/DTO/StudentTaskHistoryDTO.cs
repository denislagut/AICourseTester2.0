namespace AICourseTester.DTO
{
	public class StudentTaskHistoryDTO
	{
		public int Id { get; set; }
		public int? TaskId { get; set; }
		public string TaskType { get; set; } = null!;
		public string TaskName { get; set; } = null!;
		public string TeacherName { get; set; } = "Преподаватель";
		public DateTime Date { get; set; }
		public string Status { get; set; } = "Проверено";
		public bool IsSolved { get; set; } = true;
		public bool CanOpen { get; set; }
	}
}
