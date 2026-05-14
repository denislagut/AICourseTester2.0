namespace AICourseTester.Models.Analysis
{
	public static class ErrorCodes
	{
		// Common
		public const string NodeMissing = "NODE_MISSING";
		public const string NodeUnexpected = "NODE_UNEXPECTED";

		// FifteenPuzzle / A*
		public const string HIncorrect = "H_INCORRECT";
		public const string GIncorrect = "G_INCORRECT";
		public const string FIncorrect = "F_INCORRECT";
		public const string FFormulaInconsistency = "F_FORMULA_INCONSISTENCY";
		public const string FDerivedFromIncorrectComponents = "F_DERIVED_FROM_INCORRECT_COMPONENTS";
		public const string OpenOrderIncorrect = "OPEN_ORDER_INCORRECT";

		// Alpha-Beta values
		public const string NodeAIncorrect = "NODE_A_INCORRECT";
		public const string NodeBIncorrect = "NODE_B_INCORRECT";
		public const string NodeABIncorrect = "NODE_AB_INCORRECT";
		public const string MinLevelConfusion = "MIN_LEVEL_CONFUSION";
		public const string RootMaxConfusion = "ROOT_MAX_CONFUSION";

		// Alpha-Beta path
		public const string PathStepIncorrect = "PATH_STEP_INCORRECT";
		public const string PathIncomplete = "PATH_INCOMPLETE";
		public const string PathRedundant = "PATH_REDUNDANT";
		public const string PathMissing = "PATH_MISSING";
		public const string ValueCorrectPathWrong = "VALUE_CORRECT_PATH_WRONG";
		public const string PathNotMaximizingRootValue = "PATH_NOT_MAXIMIZING_ROOT_VALUE";
		public const string ValuesAndPruningCorrectPathWrong = "VALUES_AND_PRUNING_CORRECT_PATH_WRONG";

		// Alpha-Beta pruning
		public const string PrunedRequiredNode = "PRUNED_REQUIRED_NODE";
		public const string FailedToPruneNode = "FAILED_TO_PRUNE_NODE";
		public const string ProcessedPrunedNode = "PROCESSED_PRUNED_NODE";
		public const string PathThroughPrunedBranch = "PATH_THROUGH_PRUNED_BRANCH";
		public const string EarlyPruningError = "EARLY_PRUNING_ERROR";
		public const string MissedPruningError = "MISSED_PRUNING_ERROR";
		public const string ValueAffectedByWrongPruning = "VALUE_AFFECTED_BY_WRONG_PRUNING";
		public const string ValuesCorrectPruningWrong = "VALUES_CORRECT_PRUNING_WRONG";
		public const string PruningCorrectResultWrongReason = "PRUNING_CORRECT_RESULT_WRONG_REASON";

		// Alpha-Beta consistency
		public const string PruningPathInconsistency = "PRUNING_PATH_INCONSISTENCY";
		public const string ValuePruningInconsistency = "VALUE_PRUNING_INCONSISTENCY";
		public const string ValuePathInconsistency = "VALUE_PATH_INCONSISTENCY";

		// Older / optional pruning structure
		public const string OverPruningSubtree = "OVER_PRUNING_SUBTREE";
		public const string UnderPruningSubtree = "UNDER_PRUNING_SUBTREE";
	}
}