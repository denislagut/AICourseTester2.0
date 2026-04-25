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
			AnalyzePruning(userSolution, correctSolution, nodeMeta, result);
			AnalyzeConsistency(userSolution, correctSolution, nodeMeta, result);
			AggregatePatterns(result, nodeMeta);

			result.TotalErrors = result.Errors.Count;
			result.NodeErrorsCount = result.Errors.Count(e => e.Code.StartsWith("NODE"));
			result.PathErrorsCount = result.Errors.Count(e => e.Code.StartsWith("PATH"));
			result.PruningRelatedCount = result.Errors.Count(e =>
				e.Code.StartsWith("PRUN") || e.Code.Contains("PRUNE"));

			return result;
		}

		private Dictionary<int, NodeMeta> BuildNodeMeta(ABNode root)
		{
			var result = new Dictionary<int, NodeMeta>();
			Traverse(root, 0, null, null, result);
			return result;
		}

		private void Traverse(
			ABNode node,
			int depth,
			int? parentId,
			int? rootBranchId,
			Dictionary<int, NodeMeta> result)
		{
			var currentRootBranchId = rootBranchId;

			if (depth == 1)
			{
				currentRootBranchId = node.Id;
			}

			result[node.Id] = new NodeMeta
			{
				NodeId = node.Id,
				Depth = depth,
				ParentId = parentId,
				IsLeaf = node.SubNodes == null || node.SubNodes.Count == 0,
				RootBranchId = currentRootBranchId
			};

			if (node.SubNodes == null)
				return;

			foreach (var child in node.SubNodes)
			{
				Traverse(child, depth + 1, node.Id, currentRootBranchId, result);
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
						RootBranchId = meta?.RootBranchId,
						ElementType = meta?.IsLeaf == true ? "Leaf" : "InternalNode",
						ExpectedA = expected.A,
						ExpectedB = expected.B,
						IsOnCorrectPath = (correctSolution.Path ?? Array.Empty<int>()).Contains(nodeId),
						IsExpectedPruned = false,
						IsUserPruned = false,
						SeverityScore = 1.0,
						GroupKey = $"NODE_MISSING_L{meta?.Depth}"
					});
					continue;
				}

				var isRoot = nodeId == 0;
				var isLeaf = meta?.IsLeaf == true;

				// ВАЖНО:
				// - Корень: пользователь вводит A
				// - Внутренние не-корневые узлы: пользователь вводит B
				// - Листья: используем A как значение узла
				bool compareA;
				bool compareB;

				if (isRoot)
				{
					compareA = true;
					compareB = false;
				}
				else if (isLeaf)
				{
					compareA = true;
					compareB = false;
				}
				else
				{
					compareA = false;
					compareB = true;
				}

				var aWrong = compareA && actual.A != expected.A;
				var bWrong = compareB && actual.B != expected.B;

				if (aWrong && bWrong)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = "NODE_AB_INCORRECT",
						Message = $"В узле {nodeId} неверны значения A и B.",
						NodeId = nodeId,
						TreeLevel = meta?.Depth,
						RootBranchId = meta?.RootBranchId,
						ElementType = isLeaf ? "Leaf" : (isRoot ? "RootNode" : "InternalNode"),
						ExpectedA = compareA ? expected.A : null,
						ActualA = compareA ? actual.A : null,
						ExpectedB = compareB ? expected.B : null,
						ActualB = compareB ? actual.B : null,
						IsOnCorrectPath = (correctSolution.Path ?? Array.Empty<int>()).Contains(nodeId),
						IsExpectedPruned = false,
						IsUserPruned = false,
						SeverityScore = isLeaf ? 1.0 : 2.5,
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
						RootBranchId = meta?.RootBranchId,
						ElementType = isLeaf ? "Leaf" : (isRoot ? "RootNode" : "InternalNode"),
						ExpectedA = expected.A,
						ActualA = actual.A,
						IsOnCorrectPath = (correctSolution.Path ?? Array.Empty<int>()).Contains(nodeId),
						IsExpectedPruned = false,
						IsUserPruned = false,
						SeverityScore = isLeaf ? 1.0 : 2.5,
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
						RootBranchId = meta?.RootBranchId,
						ElementType = isLeaf ? "Leaf" : (isRoot ? "RootNode" : "InternalNode"),
						ExpectedB = expected.B,
						ActualB = actual.B,
						IsOnCorrectPath = (correctSolution.Path ?? Array.Empty<int>()).Contains(nodeId),
						IsExpectedPruned = false,
						IsUserPruned = false,
						SeverityScore = isLeaf ? 1.0 : 2.5,
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
					RootBranchId = meta?.RootBranchId,
					ElementType = meta?.IsLeaf == true ? "Leaf" : "InternalNode",
					ActualA = userPair.Value.A,
					ActualB = userPair.Value.B,
					IsOnCorrectPath = false,
					IsExpectedPruned = true,
					IsUserPruned = false,
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

		private void AnalyzePruning(
	AlphaBetaSolutionDTO userSolution,
	AlphaBetaSolutionDTO correctSolution,
	Dictionary<int, NodeMeta> nodeMeta,
	ErrorAnalysisResult result)
		{
			var userPrunedNodeIds = (userSolution.PrunedNodeIds ?? Array.Empty<int>()).ToHashSet();
			var userNodeIds = (userSolution.Nodes ?? new List<ABNodeDTO>()).Select(n => n.Id).ToHashSet();
			var correctNodeIds = (correctSolution.Nodes ?? new List<ABNodeDTO>()).Select(n => n.Id).ToHashSet();
			var correctPathIds = (correctSolution.Path ?? Array.Empty<int>()).ToHashSet();

			var allNodeIds = nodeMeta.Keys.ToHashSet();
			var expectedPrunedNodeIds = allNodeIds.Except(correctNodeIds).ToHashSet();

			// 1. Пользователь отсёк обязательный узел
			foreach (var nodeId in userPrunedNodeIds)
			{
				if (correctNodeIds.Contains(nodeId))
				{
					var meta = nodeMeta.TryGetValue(nodeId, out var m) ? m : null;

					result.Errors.Add(new AnalyzedError
					{
						Code = "PRUNED_REQUIRED_NODE",
						Message = $"Узел {nodeId} был ошибочно отсечён, хотя должен участвовать в решении.",
						NodeId = nodeId,
						TreeLevel = meta?.Depth,
						RootBranchId = meta?.RootBranchId,
						ElementType = meta?.IsLeaf == true ? "Leaf" : "InternalNode",
						IsOnCorrectPath = correctPathIds.Contains(nodeId),
						IsUserPruned = true,
						IsExpectedPruned = false,
						SeverityScore = correctPathIds.Contains(nodeId) ? 4.0 : 3.0,
						GroupKey = $"PRUNE_REQUIRED_B{meta?.RootBranchId}"
					});
				}
			}

			// 2. Пользователь не отсёк узел, который должен быть отсечён
			foreach (var nodeId in expectedPrunedNodeIds)
			{
				if (!userPrunedNodeIds.Contains(nodeId) && userNodeIds.Contains(nodeId))
				{
					var meta = nodeMeta.TryGetValue(nodeId, out var m) ? m : null;

					result.Errors.Add(new AnalyzedError
					{
						Code = "FAILED_TO_PRUNE_NODE",
						Message = $"Узел {nodeId} не был отсечён, хотя должен быть исключён из решения.",
						NodeId = nodeId,
						TreeLevel = meta?.Depth,
						RootBranchId = meta?.RootBranchId,
						ElementType = meta?.IsLeaf == true ? "Leaf" : "InternalNode",
						IsOnCorrectPath = false,
						IsUserPruned = false,
						IsExpectedPruned = true,
						SeverityScore = 3.0,
						GroupKey = $"FAILED_PRUNE_B{meta?.RootBranchId}"
					});
				}
			}

			// 3. Пользователь сам отсёк узел, но всё равно обработал его
			foreach (var nodeId in userPrunedNodeIds)
			{
				if (userNodeIds.Contains(nodeId))
				{
					var meta = nodeMeta.TryGetValue(nodeId, out var m) ? m : null;

					result.Errors.Add(new AnalyzedError
					{
						Code = "PROCESSED_PRUNED_NODE",
						Message = $"Узел {nodeId} был помечен как отсечённый, но при этом обработан пользователем.",
						NodeId = nodeId,
						TreeLevel = meta?.Depth,
						RootBranchId = meta?.RootBranchId,
						ElementType = meta?.IsLeaf == true ? "Leaf" : "InternalNode",
						IsUserPruned = true,
						SeverityScore = 3.5,
						GroupKey = $"PROCESSED_PRUNED_B{meta?.RootBranchId}"
					});
				}
			}

			// 4. Путь идёт через отсечённую ветвь
			foreach (var nodeId in userSolution.Path ?? Array.Empty<int>())
			{
				if (userPrunedNodeIds.Contains(nodeId))
				{
					var meta = nodeMeta.TryGetValue(nodeId, out var m) ? m : null;

					result.Errors.Add(new AnalyzedError
					{
						Code = "PATH_THROUGH_PRUNED_BRANCH",
						Message = $"Выбран путь через узел {nodeId}, который был помечен как отсечённый.",
						NodeId = nodeId,
						TreeLevel = meta?.Depth,
						RootBranchId = meta?.RootBranchId,
						ElementType = "PathStep",
						IsUserPruned = true,
						SeverityScore = 4.0,
						GroupKey = $"PATH_PRUNE_CONFLICT_B{meta?.RootBranchId}"
					});
				}
			}
		}
		private void AnalyzeConsistency(
	AlphaBetaSolutionDTO userSolution,
	AlphaBetaSolutionDTO correctSolution,
	Dictionary<int, NodeMeta> nodeMeta,
	ErrorAnalysisResult result)
		{
			var nodeValueErrors = result.Errors.Where(e =>
				e.Code == "NODE_A_INCORRECT" ||
				e.Code == "NODE_B_INCORRECT" ||
				e.Code == "NODE_AB_INCORRECT").ToList();

			var pathErrors = result.Errors.Where(e => e.Code.StartsWith("PATH")).ToList();
			var pruningErrors = result.Errors.Where(e => e.Code.StartsWith("PRUN") || e.Code.Contains("PRUNE")).ToList();

			if (nodeValueErrors.Count == 0 && pathErrors.Count > 0)
			{
				result.Errors.Add(new AnalyzedError
				{
					Code = "VALUE_PATH_INCONSISTENCY",
					Message = "Значения узлов в целом согласованы, однако выбранный путь не соответствует решению.",
					ElementType = "PathStep",
					SeverityScore = 3.5,
					GroupKey = "VALUE_PATH_INCONSISTENCY"
				});
			}

			if (pruningErrors.Any(e => e.Code == "PATH_THROUGH_PRUNED_BRANCH"))
			{
				result.Errors.Add(new AnalyzedError
				{
					Code = "PRUNING_PATH_INCONSISTENCY",
					Message = "Выбор пути противоречит действиям по отсечению.",
					ElementType = "PathStep",
					SeverityScore = 4.0,
					GroupKey = "PRUNING_PATH_INCONSISTENCY"
				});
			}

			if (pruningErrors.Any(e => e.Code == "PROCESSED_PRUNED_NODE"))
			{
				result.Errors.Add(new AnalyzedError
				{
					Code = "VALUE_PRUNING_INCONSISTENCY",
					Message = "Пользователь одновременно отсёк и обработал часть ветвей.",
					ElementType = "InternalNode",
					SeverityScore = 3.5,
					GroupKey = "VALUE_PRUNING_INCONSISTENCY"
				});
			}
		}
		private void AggregatePatterns(
	ErrorAnalysisResult result,
	Dictionary<int, NodeMeta>? nodeMeta = null)
		{
			if (result.Errors.Count == 0)
			{
				return;
			}

			var groups = result.Errors
				.Where(e => !string.IsNullOrWhiteSpace(e.GroupKey))
				.GroupBy(e => e.GroupKey!);

			foreach (var group in groups)
			{
				var errors = group.ToList();
				var errorCount = errors.Count;

				var opportunityCount = EstimateOpportunityCount(group.Key, errors, nodeMeta);
				var ratio = opportunityCount == 0
					? 1.0
					: (double)errorCount / opportunityCount;

				var patternType = DeterminePatternType(errorCount, ratio);

				foreach (var error in errors)
				{
					error.SimilarErrorCount = errorCount;
					error.SimilarOpportunityCount = opportunityCount;
					error.SimilarErrorRatio = Math.Round(ratio, 2);
					error.PatternType = patternType;

					error.SeverityScore = AdjustSeverityByPattern(
						error.SeverityScore,
						patternType);
				}
			}

			result.HasMassNodeErrors = result.Errors.Any(e =>
				e.PatternType == "SYSTEMATIC_MISUNDERSTANDING" &&
				e.Code.StartsWith("NODE"));

			result.HasPathErrors = result.Errors.Any(e =>
				e.Code.StartsWith("PATH"));
		}
		private int EstimateOpportunityCount(
	string groupKey,
	List<AnalyzedError> errors,
	Dictionary<int, NodeMeta>? nodeMeta)
		{
			if (nodeMeta == null || nodeMeta.Count == 0)
			{
				return Math.Max(errors.Count, 1);
			}

			if (groupKey.StartsWith("NODE_A_L") ||
				groupKey.StartsWith("NODE_B_L") ||
				groupKey.StartsWith("NODE_VALUE_L"))
			{
				var level = errors.FirstOrDefault()?.TreeLevel;
				if (level == null)
				{
					return Math.Max(errors.Count, 1);
				}

				return nodeMeta.Values.Count(m =>
					m.Depth == level &&
					!m.IsLeaf);
			}

			if (groupKey.StartsWith("NODE_MISSING_L") ||
				groupKey.StartsWith("NODE_UNEXPECTED_L"))
			{
				var level = errors.FirstOrDefault()?.TreeLevel;
				if (level == null)
				{
					return Math.Max(errors.Count, 1);
				}

				return nodeMeta.Values.Count(m => m.Depth == level);
			}

			if (groupKey.StartsWith("FAILED_PRUNE_B") ||
				groupKey.StartsWith("PRUNE_REQUIRED_B") ||
				groupKey.StartsWith("PROCESSED_PRUNED_B"))
			{
				var rootBranchId = errors.FirstOrDefault()?.RootBranchId;
				if (rootBranchId == null)
				{
					return Math.Max(errors.Count, 1);
				}

				return nodeMeta.Values.Count(m =>
					m.RootBranchId == rootBranchId &&
					m.Depth > 1);
			}

			return Math.Max(errors.Count, 1);
		}

		private string DeterminePatternType(int errorCount, double ratio)
		{
			if (errorCount <= 1 && ratio <= 0.25)
			{
				return "CARELESS_MISTAKE";
			}

			if (ratio >= 0.6 || errorCount >= 3)
			{
				return "SYSTEMATIC_MISUNDERSTANDING";
			}

			return "PARTIAL_MISUNDERSTANDING";
		}

		private double AdjustSeverityByPattern(double severity, string patternType)
		{
			return patternType switch
			{
				"CARELESS_MISTAKE" => Math.Round(severity * 0.75, 2),
				"PARTIAL_MISUNDERSTANDING" => severity,
				"SYSTEMATIC_MISUNDERSTANDING" => Math.Round(severity * 1.35, 2),
				_ => severity
			};
		}
	}
}