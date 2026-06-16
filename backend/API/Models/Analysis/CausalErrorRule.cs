using AICourseTester.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace AICourseTester.Models.Analysis
{
	public class CausalErrorRule
	{
		private string? _taskTypeCode;
		private string? _sourceErrorCode;
		private string? _targetErrorCode;
		private string? _relationTypeCode;

		public int Id { get; set; }

		public int TaskTypeId { get; set; }
		public TaskType TaskTypeRef { get; set; } = null!;

		[NotMapped]
		public string TaskType
		{
			get => TaskTypeRef?.Code ?? _taskTypeCode ?? string.Empty;
			set
			{
				_taskTypeCode = value;
				TaskTypeId = LookupIds.TaskTypeId(value);
			}
		}

		public int SourceErrorTypeId { get; set; }
		public ErrorType SourceErrorType { get; set; } = null!;

		[NotMapped]
		public string SourceErrorCode
		{
			get => SourceErrorType?.Code ?? _sourceErrorCode ?? string.Empty;
			set => _sourceErrorCode = value;
		}

		public int TargetErrorTypeId { get; set; }
		public ErrorType TargetErrorType { get; set; } = null!;

		[NotMapped]
		public string TargetErrorCode
		{
			get => TargetErrorType?.Code ?? _targetErrorCode ?? string.Empty;
			set => _targetErrorCode = value;
		}

		public int RelationTypeId { get; set; }
		public CausalRelationType RelationTypeRef { get; set; } = null!;

		[NotMapped]
		public string RelationType
		{
			get => RelationTypeRef?.Code ?? _relationTypeCode ?? string.Empty;
			set
			{
				_relationTypeCode = value;
				RelationTypeId = LookupIds.CausalRelationTypeId(value);
			}
		}

		public double Weight { get; set; }
		public bool SameNodeRequired { get; set; }
		public bool SameRootBranchRequired { get; set; }
		public bool IsActive { get; set; } = true;
	}
}
