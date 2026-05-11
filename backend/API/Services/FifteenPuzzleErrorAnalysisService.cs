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
				e.Code == "H_INCORRECT" ||
				e.Code == "G_INCORRECT" ||
				e.Code == "F_INCORRECT" ||
				e.Code == "F_FORMULA_INCONSISTENCY" ||
				e.Code == "F_DERIVED_FROM_INCORRECT_COMPONENTS");

			result.PathErrorsCount = result.Errors.Count(e =>
				e.Code == "OPEN_ORDER_INCORRECT" ||
				e.Code == "NODE_MISSING" ||
				e.Code == "NODE_UNEXPECTED");

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
						Code = "NODE_MISSING",
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

				// Проверяем именно формулу, введённую студентом:
				// если actual.F == actual.G + actual.H, то формула понята,
				// даже если h или g сами по себе неверны.
				var formulaWrong = actual.F != actual.G + actual.H;

				if (hWrong)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = "H_INCORRECT",
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
						Code = "G_INCORRECT",
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
						Code = "F_FORMULA_INCONSISTENCY",
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
						Code = "F_DERIVED_FROM_INCORRECT_COMPONENTS",
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
						Code = "F_INCORRECT",
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

			var incorrectlyOpenedOpenListNodeIds = new HashSet<int>();

			// Случай 1:
			// Узел есть в эталонном решении, но должен был остаться открытым (OpenOrder = -1),
			// а студент его раскрыл.
			foreach (var userNode in userNodes.Values.Where(n => n.OpenOrder >= 0))
			{
				if (correctNodes.TryGetValue(userNode.Id, out var correctNode) &&
					correctNode.OpenOrder < 0)
				{
					incorrectlyOpenedOpenListNodeIds.Add(userNode.Id);

					result.Errors.Add(new AnalyzedError
					{
						Code = "NODE_UNEXPECTED",
						Message = $"Лишнее раскрытие узла {userNode.Id}. Узел должен был остаться в списке открытых.",
						NodeId = userNode.Id,
						ActualPathNodeId = userNode.Id,
						ElementType = "PuzzleNode",
						SeverityScore = 3.0,
						GroupKey = "A_STAR_EXTRA_EXPANSION"
					});
				}
			}

			// Для сравнения порядка исключаем узлы, которые уже получили ошибку
			// "должен был остаться открытым", чтобы не было дублей NODE_UNEXPECTED.
			var userOpened = userNodes.Values
				.Where(n => n.OpenOrder >= 0)
				.Where(n => !incorrectlyOpenedOpenListNodeIds.Contains(n.Id))
				.Where(n =>
					!correctNodes.TryGetValue(n.Id, out var correctNode) ||
					correctNode.OpenOrder >= 0)
				.OrderBy(n => n.OpenOrder)
				.ToList();

			var maxCount = Math.Max(correctOpened.Count, userOpened.Count);

			for (int i = 0; i < maxCount; i++)
			{
				var expectedNode = i < correctOpened.Count ? correctOpened[i] : null;
				var actualNode = i < userOpened.Count ? userOpened[i] : null;

				if (expectedNode == null && actualNode != null)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = "NODE_UNEXPECTED",
						Message = $"Лишнее раскрытие узла {actualNode.Id}.",
						NodeId = actualNode.Id,
						PathStepIndex = i,
						ActualPathNodeId = actualNode.Id,
						ElementType = "PuzzleNode",
						SeverityScore = 3.0,
						GroupKey = "A_STAR_EXTRA_EXPANSION"
					});

					continue;
				}

				if (expectedNode != null && actualNode == null)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = "NODE_MISSING",
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

				if (expectedNode != null && actualNode != null && expectedNode.Id != actualNode.Id)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = "OPEN_ORDER_INCORRECT",
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