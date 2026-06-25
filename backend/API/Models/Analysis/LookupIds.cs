namespace AICourseTester.Models.Analysis
{
	public static class LookupIds
	{
		public static int TaskTypeId(string code) => code switch
		{
			"AlphaBeta" => 1,
			"FifteenPuzzle" => 2,
			_ => throw new InvalidOperationException($"Unknown task type: {code}")
		};

		public static int AnalysisStatusId(string code) => code switch
		{
			"Started" => 1,
			"Completed" => 2,
			"Failed" => 3,
			_ => throw new InvalidOperationException($"Unknown analysis status: {code}")
		};

		public static int GapLevelId(string code) => code switch
		{
			"Low" => 1,
			"Medium" => 2,
			"High" => 3,
			"Critical" => 4,
			_ => 1
		};

		public static string GapLevelCode(int id) => id switch
		{
			1 => "Low",
			2 => "Medium",
			3 => "High",
			4 => "Critical",
			_ => "Low"
		};

		public static int GapTrendId(string code) => code switch
		{
			"Stable" => 1,
			"Improved" => 2,
			"Worsened" => 3,
			"New" => 4,
			_ => 1
		};

		public static string GapTrendCode(int id) => id switch
		{
			1 => "Stable",
			2 => "Improved",
			3 => "Worsened",
			4 => "New",
			_ => "Stable"
		};

		public static int CausalRelationTypeId(string code) => code switch
		{
			"CAUSES" => 1,
			"EXPLAINS" => 2,
			"MAY_CAUSE" => 3,
			"CONTEXT_FOR" => 4,
			"SUMMARIZES" => 5,
			_ => 1
		};

		public static string CausalRelationTypeCode(int id) => id switch
		{
			1 => "CAUSES",
			2 => "EXPLAINS",
			3 => "MAY_CAUSE",
			4 => "CONTEXT_FOR",
			5 => "SUMMARIZES",
			_ => "CAUSES"
		};
	}
}
