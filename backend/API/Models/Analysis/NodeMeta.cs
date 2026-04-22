namespace AICourseTester.Models.Analysis
{
	public class NodeMeta
	{
		public int NodeId { get; set; }
		public int Depth { get; set; }
		public int? ParentId { get; set; }
		public bool IsLeaf { get; set; }

		// Идентификатор корневого поддерева первого уровня
		public int? RootBranchId { get; set; }
	}
}