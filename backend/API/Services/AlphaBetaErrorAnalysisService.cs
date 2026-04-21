using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services.Interfaces;

namespace AICourseTester.Services
{
	public class AlphaBetaErrorAnalysisService : IAlphaBetaErrorAnalysisService
	{
		public ErrorAnalysisResult Analyze(
			ProblemTree<ABNode> problem,
			AlphaBetaSolutionDTO userSolution,
			AlphaBetaSolutionDTO correctSolution)
		{
			var result = new ErrorAnalysisResult();

			var nodeMeta = BuildNodeMeta(problem.Head);

			AnalyzeNodes(userSolution, correctSolution, nodeMeta, result);
			AnalyzePath(userSolution, correctSolution, result);
			AggregatePatterns(result);

			result.TotalErrors = result.Errors.Count;
			result.NodeErrorsCount = result.Errors.Count(e => e.Code.StartsWith("NODE"));
			result.PathErrorsCount = result.Errors.Count(e => e.Code.StartsWith("PATH"));
			result.PruningRelatedCount = result.Errors.Count(e => e.Code.StartsWith("PRUNING"));

			return result;
		}

		private Dictionary<int, NodeMeta> BuildNodeMeta(ABNode root)
		{
			var result = new Dictionary<int, NodeMeta>();
			Traverse(root, 0, null, result);
			return result;
		}

		private void Traverse(
			ABNode node,
			int depth,
			int? parentId,
			Dictionary<int, NodeMeta> result)
		{
			result[node.Id] = new NodeMeta
			{
				NodeId = node.Id,
				Depth = depth,
				ParentId = parentId,
				IsLeaf = node.SubNodes == null || node.SubNodes.Count == 0
			};

			if (node.SubNodes == null)
				return;

			foreach (var child in node.SubNodes)
			{
				Traverse(child, depth + 1, node.Id, result);
			}
		}

		private void AnalyzeNodes(
			AlphaBetaSolutionDTO userSolution,
			AlphaBetaSolutionDTO correctSolution,
			Dictionary<int, NodeMeta> nodeMeta,
			ErrorAnalysisResult result)
		{
			var correctNodes = (correctSolution.Nodes ?? new List<ABNodeDTO>())
				.GroupBy(x => x.Id)
				.Select(g => g.First())
				.ToDictionary(x => x.Id);

			var userNodes = (userSolution.Nodes ?? new List<ABNodeDTO>())
				.GroupBy(x => x.Id)
				.Select(g => g.First())
				.ToDictionary(x => x.Id);

			foreach (var correctPair in correctNodes)
			{
				var nodeId = correctPair.Key;
				var expected = correctPair.Value;
				var meta = nodeMeta.TryGetValue(nodeId, out var m) ? m : null;

				if (!userNodes.TryGetValue(nodeId, out var actual))
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = "NODE_MISSING",
						Message = $"Не заполнен узел {nodeId}.",
						NodeId = nodeId,
						TreeLevel = meta?.Depth,
						ElementType = meta?.IsLeaf == true ? "Leaf" : "InternalNode",
						ExpectedA = expected.A,
						ExpectedB = expected.B,
						SeverityScore = 1.0,
						GroupKey = $"NODE_MISSING_L{meta?.Depth}"
					});
					continue;
				}

				var aWrong = actual.A != expected.A;
				var bWrong = actual.B != expected.B;

				if (aWrong && bWrong)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = "NODE_AB_INCORRECT",
						Message = $"В узле {nodeId} неверны значения A и B.",
						NodeId = nodeId,
						TreeLevel = meta?.Depth,
						ElementType = meta?.IsLeaf == true ? "Leaf" : "InternalNode",
						ExpectedA = expected.A,
						ActualA = actual.A,
						ExpectedB = expected.B,
						ActualB = actual.B,
						SeverityScore = meta?.IsLeaf == true ? 1.0 : 2.0,
						GroupKey = $"NODE_VALUE_L{meta?.Depth}"
					});
				}
				else if (aWrong)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = "NODE_A_INCORRECT",
						Message = $"В узле {nodeId} неверно значение A.",
						NodeId = nodeId,
						TreeLevel = meta?.Depth,
						ElementType = meta?.IsLeaf == true ? "Leaf" : "InternalNode",
						ExpectedA = expected.A,
						ActualA = actual.A,
						SeverityScore = meta?.IsLeaf == true ? 1.0 : 2.0,
						GroupKey = $"NODE_A_L{meta?.Depth}"
					});
				}
				else if (bWrong)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = "NODE_B_INCORRECT",
						Message = $"В узле {nodeId} неверно значение B.",
						NodeId = nodeId,
						TreeLevel = meta?.Depth,
						ElementType = meta?.IsLeaf == true ? "Leaf" : "InternalNode",
						ExpectedB = expected.B,
						ActualB = actual.B,
						SeverityScore = meta?.IsLeaf == true ? 1.0 : 2.0,
						GroupKey = $"NODE_B_L{meta?.Depth}"
					});
				}
			}

			foreach (var userPair in userNodes)
			{
				if (correctNodes.ContainsKey(userPair.Key))
					continue;

				var nodeId = userPair.Key;
				var meta = nodeMeta.TryGetValue(nodeId, out var m) ? m : null;

				result.Errors.Add(new AnalyzedError
				{
					Code = "NODE_UNEXPECTED",
					Message = $"Узел {nodeId} не ожидался в ответе.",
					NodeId = nodeId,
					TreeLevel = meta?.Depth,
					ElementType = meta?.IsLeaf == true ? "Leaf" : "InternalNode",
					ActualA = userPair.Value.A,
					ActualB = userPair.Value.B,
					SeverityScore = 1.0,
					GroupKey = $"NODE_UNEXPECTED_L{meta?.Depth}"
				});
			}
		}

		private void AnalyzePath(
			AlphaBetaSolutionDTO userSolution,
			AlphaBetaSolutionDTO correctSolution,
			ErrorAnalysisResult result)
		{
			var expectedPath = correctSolution.Path ?? Array.Empty<int>();
			var actualPath = userSolution.Path ?? Array.Empty<int>();

			if (actualPath.Length == 0 && expectedPath.Length > 0)
			{
				result.Errors.Add(new AnalyzedError
				{
					Code = "PATH_MISSING",
					Message = "Оптимальный путь не указан.",
					ElementType = "PathStep",
					SeverityScore = 3.0,
					GroupKey = "PATH"
				});
				return;
			}

			var commonLength = Math.Min(expectedPath.Length, actualPath.Length);

			for (int i = 0; i < commonLength; i++)
			{
				if (expectedPath[i] != actualPath[i])
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = "PATH_STEP_INCORRECT",
						Message = $"На шаге {i + 1} выбран неверный узел пути.",
						ElementType = "PathStep",
						PathStepIndex = i,
						ExpectedPathNodeId = expectedPath[i],
						ActualPathNodeId = actualPath[i],
						SeverityScore = 3.0,
						GroupKey = "PATH"
					});
				}
			}

			if (actualPath.Length < expectedPath.Length)
			{
				result.Errors.Add(new AnalyzedError
				{
					Code = "PATH_INCOMPLETE",
					Message = "Путь указан не полностью.",
					ElementType = "PathStep",
					SeverityScore = 2.0,
					GroupKey = "PATH"
				});
			}

			if (actualPath.Length > expectedPath.Length)
			{
				result.Errors.Add(new AnalyzedError
				{
					Code = "PATH_REDUNDANT",
					Message = "Путь содержит лишние шаги.",
					ElementType = "PathStep",
					SeverityScore = 2.0,
					GroupKey = "PATH"
				});
			}
		}

		private void AggregatePatterns(ErrorAnalysisResult result)
		{
			var grouped = result.Errors
				.Where(e => !string.IsNullOrWhiteSpace(e.GroupKey))
				.GroupBy(e => e.GroupKey!);

			foreach (var group in grouped)
			{
				if (group.Count() >= 3)
				{
					foreach (var error in group)
					{
						error.SeverityScore += 0.5;
					}
				}
			}

			result.HasMassNodeErrors = result.Errors.Count(e => e.Code.StartsWith("NODE")) >= 3;
			result.HasPathErrors = result.Errors.Any(e => e.Code.StartsWith("PATH"));
		}
	}
}