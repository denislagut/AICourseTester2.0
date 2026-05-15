using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services.Interfaces;

namespace AICourseTester.Services
{
	public class FifteenPuzzleErrorAnalysisService : IFifteenPuzzleErrorAnalysisService
	{
		public ErrorAnalysisResult Analyze(
			List<ANodeDTO> userSolution,
			List<ANodeDTO> correctSolution,
			int heuristic)
		{
			var result = new ErrorAnalysisResult();

			var userNodes = userSolution
				.GroupBy(n => n.Id)
				.Select(g => g.First())
				.ToDictionary(n => n.Id);

			var correctNodes = correctSolution
				.GroupBy(n => n.Id)
				.Select(g => g.First())
				.ToDictionary(n => n.Id);

			AnalyzeValues(userNodes, correctNodes, heuristic, result);
			AnalyzeOpenOrder(userNodes, correctNodes, result);
			AggregatePatterns(result);

			result.TotalErrors = result.Errors.Count;

			result.NodeErrorsCount = result.Errors.Count(e =>
				e.Code == ErrorCodes.HIncorrect ||
				e.Code == ErrorCodes.GIncorrect ||
				e.Code == ErrorCodes.FIncorrect ||
				e.Code == ErrorCodes.FFormulaInconsistency ||
				e.Code == ErrorCodes.FDerivedFromIncorrectComponents);

			result.PathErrorsCount = result.Errors.Count(e =>
				e.Code == ErrorCodes.OpenOrderIncorrect ||
				e.Code == ErrorCodes.NodeMissing ||
				e.Code == ErrorCodes.NodeUnexpected);

			result.PruningRelatedCount = 0;

			return result;
		}

		private void AnalyzeValues(
			Dictionary<int, ANodeDTO> userNodes,
			Dictionary<int, ANodeDTO> correctNodes,
			int heuristic,
			ErrorAnalysisResult result)
		{
			foreach (var pair in correctNodes)
			{
				var nodeId = pair.Key;
				var expected = pair.Value;

				if (!userNodes.TryGetValue(nodeId, out var actual))
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = ErrorCodes.NodeMissing,
						Message = $"Узел {nodeId} отсутствует в пользовательском решении.",
						NodeId = nodeId,
						ElementType = "PuzzleNode",
						ExpectedA = expected.F,
						SeverityScore = 2.0,
						GroupKey = "A_STAR_NODE_MISSING" 
					});

					continue;
				}

				var hWrong = expected.H != actual.H;
				var gWrong = expected.G != actual.G;
				var fWrong = expected.F != actual.F;
				var formulaWrong = actual.F != actual.G + actual.H;

				if (hWrong)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = ErrorCodes.HIncorrect,
						Message = $"В узле {nodeId} неверное значение h.",
						NodeId = nodeId,
						ElementType = "PuzzleNode",
						ExpectedA = expected.H,
						ActualA = actual.H,
						SeverityScore = heuristic == 2 ? 3.0 : 2.5,
						GroupKey = heuristic == 2 ? "MANHATTAN_H_VALUE" : "HAMMING_H_VALUE"
					});
				}

				if (gWrong)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = ErrorCodes.GIncorrect,
						Message = $"В узле {nodeId} неверное значение g.",
						NodeId = nodeId,
						ElementType = "PuzzleNode",
						ExpectedA = expected.G,
						ActualA = actual.G,
						SeverityScore = 2.5,
						GroupKey = "A_STAR_G_VALUE"
					});
				}

				if (formulaWrong)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = ErrorCodes.FFormulaInconsistency,
						Message = $"В узле {nodeId} нарушена формула f = g + h.",
						NodeId = nodeId,
						ElementType = "PuzzleNode",
						ExpectedA = actual.G + actual.H,
						ActualA = actual.F,
						SeverityScore = 3.5,
						GroupKey = "A_STAR_F_FORMULA"
					});
				}
				else if (fWrong && (hWrong || gWrong))
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = ErrorCodes.FDerivedFromIncorrectComponents,
						Message = $"В узле {nodeId} значение f отличается от эталона из-за ошибки в h или g.",
						NodeId = nodeId,
						ElementType = "PuzzleNode",
						ExpectedA = expected.F,
						ActualA = actual.F,
						SeverityScore = 1.0,
						GroupKey = "A_STAR_F_DERIVED"
					});
				}
				else if (fWrong)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = ErrorCodes.FIncorrect,
						Message = $"В узле {nodeId} неверное значение f.",
						NodeId = nodeId,
						ElementType = "PuzzleNode",
						ExpectedA = expected.F,
						ActualA = actual.F,
						SeverityScore = 2.5,
						GroupKey = "A_STAR_F_VALUE"
					});
				}
			}
		}

		private void AnalyzeOpenOrder(
			Dictionary<int, ANodeDTO> userNodes,
			Dictionary<int, ANodeDTO> correctNodes,
			ErrorAnalysisResult result)
		{
			var correctOpened = correctNodes.Values
				.Where(n => n.OpenOrder >= 0)
				.OrderBy(n => n.OpenOrder)
				.ToList();

			var userOpened = userNodes.Values
				.Where(n => n.OpenOrder >= 0)
				.OrderBy(n => n.OpenOrder)
				.ToList();

			var maxCount = Math.Max(correctOpened.Count, userOpened.Count);

			for (int i = 0; i < maxCount; i++)
			{
				var expectedNode = i < correctOpened.Count ? correctOpened[i] : null;
				var actualNode = i < userOpened.Count ? userOpened[i] : null;

				if (expectedNode == null && actualNode != null)
				{
					// Эталонный solver мог остановиться раньше.
					// Дополнительное раскрытие не считаем ошибкой само по себе.
					continue;
				}

				if (expectedNode != null && actualNode == null)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = ErrorCodes.NodeMissing,
						Message = $"На шаге {i} должен быть раскрыт узел {expectedNode.Id}.",
						NodeId = expectedNode.Id,
						PathStepIndex = i,
						ExpectedPathNodeId = expectedNode.Id,
						ElementType = "PuzzleNode",
						SeverityScore = 3.0,
						GroupKey = "A_STAR_MISSING_EXPANSION"
					});

					continue;
				}

				if (expectedNode == null || actualNode == null)
					continue;

				if (expectedNode.Id == actualNode.Id)
					continue;

				if (IsEquivalentExpansion(actualNode, expectedNode))
					continue;

				result.Errors.Add(new AnalyzedError
				{
					Code = ErrorCodes.OpenOrderIncorrect ,
					Message = $"На шаге {i} раскрыт неверный узел. Ожидался узел {expectedNode.Id}, получен узел {actualNode.Id}.",
					NodeId = actualNode.Id,
					PathStepIndex = i,
					ExpectedPathNodeId = expectedNode.Id,
					ActualPathNodeId = actualNode.Id,
					ElementType = "PuzzleNode",
					SeverityScore = 3.5,
					GroupKey = "A_STAR_OPEN_ORDER"
				});
			}
		}

		private bool IsEquivalentExpansion(ANodeDTO actualNode, ANodeDTO expectedNode)
		{
			return actualNode.F == expectedNode.F;
		}

		private void AggregatePatterns(ErrorAnalysisResult result)
		{
			var groups = result.Errors
				.Where(e => !string.IsNullOrWhiteSpace(e.GroupKey))
				.GroupBy(e => e.GroupKey!);

			foreach (var group in groups)
			{
				var errors = group.ToList();
				var errorCount = errors.Count;

				var opportunityCount = Math.Max(errorCount, 1);
				var ratio = (double)errorCount / opportunityCount;

				var patternType = DeterminePatternType(errorCount, ratio);

				foreach (var error in errors)
				{
					error.SimilarErrorCount = errorCount;
					error.SimilarOpportunityCount = opportunityCount;
					error.SimilarErrorRatio = Math.Round(ratio, 2);
					error.PatternType = patternType;
					error.SeverityScore = AdjustSeverityByPattern(error.SeverityScore, patternType);
				}
			}
		}

		private string DeterminePatternType(int errorCount, double ratio)
		{
			if (errorCount <= 1 && ratio <= 0.25)
				return "CARELESS_MISTAKE";

			if (ratio >= 0.6 || errorCount >= 3)
				return "SYSTEMATIC_MISUNDERSTANDING";

			return "PARTIAL_MISUNDERSTANDING";
		}

		private double AdjustSeverityByPattern(double severity, string patternType)
		{
			return patternType switch
			{
				"CARELESS_MISTAKE" => Math.Round(severity * 0.75, 2),
				"SYSTEMATIC_MISUNDERSTANDING" => Math.Round(severity * 1.35, 2),
				_ => severity
			};
		}
	}
}