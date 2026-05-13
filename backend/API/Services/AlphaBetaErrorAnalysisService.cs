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
			AnalyzeSemanticAlphaBetaErrors(problem, userSolution, correctSolution, nodeMeta, result);
			AnalyzeConsistency(userSolution, correctSolution, nodeMeta, result);
			AggregatePatterns(result, nodeMeta);

			result.TotalErrors = result.Errors.Count;
			result.NodeErrorsCount = result.Errors.Count(e =>
				e.Code.StartsWith("NODE") ||
				e.Code == "MIN_LEVEL_CONFUSION" ||
				e.Code == "ROOT_MAX_CONFUSION" ||
				e.Code == "VALUE_AFFECTED_BY_WRONG_PRUNING");
			result.PathErrorsCount = result.Errors.Count(e =>
				e.Code.StartsWith("PATH") ||
				e.Code == "VALUE_CORRECT_PATH_WRONG" ||
				e.Code == "PATH_NOT_MAXIMIZING_ROOT_VALUE" ||
				e.Code == "VALUES_AND_PRUNING_CORRECT_PATH_WRONG");
			result.PruningRelatedCount = result.Errors.Count(e =>
				e.Code.StartsWith("PRUN") ||
				e.Code.Contains("PRUNE") ||
				e.Code == "EARLY_PRUNING_ERROR" ||
				e.Code == "MISSED_PRUNING_ERROR" ||
				e.Code == "VALUE_AFFECTED_BY_WRONG_PRUNING" ||
				e.Code == "VALUES_CORRECT_PRUNING_WRONG" ||
				e.Code == "PRUNING_CORRECT_RESULT_WRONG_REASON");

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
			var userPrunedIds = (userSolution.PrunedNodeIds ?? Array.Empty<int>()).ToHashSet();

			foreach (var correctPair in correctNodes)
			{
				var nodeId = correctPair.Key;
				var expected = correctPair.Value;
				var meta = nodeMeta.TryGetValue(nodeId, out var m) ? m : null;
				if (!userNodes.TryGetValue(nodeId, out var actual))
				{
					if (userPrunedIds.Contains(nodeId))
					{
						continue;
					}

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

			var allNodeIds = nodeMeta.Keys.ToHashSet();
			var correctNodeIds = correctNodes.Keys.ToHashSet();
			var expectedPrunedNodeIds = allNodeIds.Except(correctNodeIds).ToHashSet();

			foreach (var userPair in userNodes)
			{
				if (correctNodes.ContainsKey(userPair.Key))
					continue;

				var nodeId = userPair.Key;

				// Если узел отсутствует в correctSolution потому что он должен быть отсечён,
				// не создаём NODE_UNEXPECTED. Это будет обработано как FAILED_TO_PRUNE_NODE
				// и MISSED_PRUNING_ERROR в pruning-анализе.
				if (expectedPrunedNodeIds.Contains(nodeId))
					continue;

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
					IsExpectedPruned = false,
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

		private void AnalyzeSemanticAlphaBetaErrors(
		ProblemTree<ABNode> problem,
		AlphaBetaSolutionDTO userSolution,
		AlphaBetaSolutionDTO correctSolution,
		Dictionary<int, NodeMeta> nodeMeta,
		ErrorAnalysisResult result)
		{
			var userNodes = (userSolution.Nodes ?? new List<ABNodeDTO>())
				.GroupBy(n => n.Id)
				.Select(g => g.First())
				.ToDictionary(n => n.Id);

			var correctNodes = (correctSolution.Nodes ?? new List<ABNodeDTO>())
				.GroupBy(n => n.Id)
				.Select(g => g.First())
				.ToDictionary(n => n.Id);

			var userPath = userSolution.Path ?? Array.Empty<int>();
			var correctPath = correctSolution.Path ?? Array.Empty<int>();

			var userPrunedIds = (userSolution.PrunedNodeIds ?? Array.Empty<int>()).ToHashSet();
			var allNodeIds = nodeMeta.Keys.ToHashSet();
			var correctNodeIds = correctNodes.Keys.ToHashSet();
			var expectedPrunedIds = allNodeIds.Except(correctNodeIds).ToHashSet();

			AnalyzeMinLevelConfusion(problem.Head, userNodes, correctNodes, nodeMeta, result);
			AnalyzeRootMaxConfusion(problem.Head, userNodes, correctNodes, nodeMeta, result);
			AnalyzePathNotMaximizingRootValue(userPath, correctPath, userNodes, correctNodes, result);
			AnalyzePruningTiming(problem.Head, userPrunedIds, expectedPrunedIds, nodeMeta, result);
			AnalyzeValueAffectedByWrongPruning(problem.Head, userNodes, correctNodes, userPrunedIds, nodeMeta, result);
			AnalyzeCombinedSemanticCases(result);
		}
		private void AnalyzeMinLevelConfusion(
		ABNode root,
		Dictionary<int, ABNodeDTO> userNodes,
		Dictionary<int, ABNodeDTO> correctNodes,
		Dictionary<int, NodeMeta> nodeMeta,
		ErrorAnalysisResult result)
		{
			if (root.SubNodes == null)
				return;

			foreach (var minNode in root.SubNodes)
			{
				if (minNode.SubNodes == null || minNode.SubNodes.Count == 0)
					continue;

				if (!userNodes.TryGetValue(minNode.Id, out var userNode))
					continue;

				if (!correctNodes.TryGetValue(minNode.Id, out var correctNode))
					continue;

				var leafValues = minNode.SubNodes.Select(x => x.A).ToList();

				if (leafValues.Count == 0)
					continue;

				var expectedMin = leafValues.Min();
				var oppositeMax = leafValues.Max();

				// В интерфейсе внутренний MIN-узел обычно заполняется через B
				if (userNode.B == oppositeMax && correctNode.B == expectedMin && oppositeMax != expectedMin)
				{
					var meta = nodeMeta.TryGetValue(minNode.Id, out var m) ? m : null;

					result.Errors.Add(new AnalyzedError
					{
						Code = "MIN_LEVEL_CONFUSION",
						Message = $"В MIN-узле {minNode.Id} выбрано максимальное значение вместо минимального.",
						NodeId = minNode.Id,
						TreeLevel = meta?.Depth,
						RootBranchId = meta?.RootBranchId,
						ElementType = "InternalNode",
						ExpectedB = expectedMin,
						ActualB = userNode.B,
						SeverityScore = 3.5,
						GroupKey = "MIN_MAX_CONFUSION"
					});
				}
			}
		}

		private void AnalyzeRootMaxConfusion(
			ABNode root,
			Dictionary<int, ABNodeDTO> userNodes,
			Dictionary<int, ABNodeDTO> correctNodes,
			Dictionary<int, NodeMeta> nodeMeta,
			ErrorAnalysisResult result)
		{
			if (root.SubNodes == null || root.SubNodes.Count == 0)
				return;

			if (!userNodes.TryGetValue(root.Id, out var userRoot))
				return;

			if (!correctNodes.TryGetValue(root.Id, out var correctRoot))
				return;

			var minValues = root.SubNodes
				.Where(n => correctNodes.ContainsKey(n.Id))
				.Select(n => correctNodes[n.Id].B)
				.ToList();

			if (minValues.Count == 0)
				return;

			var expectedMax = minValues.Max();
			var oppositeMin = minValues.Min();

			if (userRoot.A == oppositeMin && correctRoot.A == expectedMax && oppositeMin != expectedMax)
			{
				var meta = nodeMeta.TryGetValue(root.Id, out var m) ? m : null;

				result.Errors.Add(new AnalyzedError
				{
					Code = "ROOT_MAX_CONFUSION",
					Message = "В корне MAX выбрано минимальное значение вместо максимального.",
					NodeId = root.Id,
					TreeLevel = meta?.Depth,
					ElementType = "RootNode",
					ExpectedA = expectedMax,
					ActualA = userRoot.A,
					SeverityScore = 3.5,
					GroupKey = "MIN_MAX_CONFUSION"
				});
			}
		}

		private void AnalyzePathNotMaximizingRootValue(
		int[] userPath,
		int[] correctPath,
		Dictionary<int, ABNodeDTO> userNodes,
		Dictionary<int, ABNodeDTO> correctNodes,
		ErrorAnalysisResult result)
		{
			if (userPath.Length == 0 || correctPath.Length == 0)
				return;

			var userFirstStep = userPath[0];
			var correctFirstStep = correctPath[0];

			if (userFirstStep == correctFirstStep)
				return;

			var hasNodeValueErrors = result.Errors.Any(e =>
				e.Code == "NODE_A_INCORRECT" ||
				e.Code == "NODE_B_INCORRECT" ||
				e.Code == "NODE_AB_INCORRECT");

			if (!hasNodeValueErrors)
			{
				result.Errors.Add(new AnalyzedError
				{
					Code = "VALUE_CORRECT_PATH_WRONG",
					Message = "Значения узлов рассчитаны верно, но выбран неверный оптимальный путь.",
					ElementType = "PathStep",
					PathStepIndex = 0,
					ExpectedPathNodeId = correctFirstStep,
					ActualPathNodeId = userFirstStep,
					SeverityScore = 3.5,
					GroupKey = "VALUE_CORRECT_PATH_WRONG"
				});
			}

			if (correctNodes.TryGetValue(userFirstStep, out var selectedNode) &&
				correctNodes.TryGetValue(correctFirstStep, out var expectedNode))
			{
				if (selectedNode.B < expectedNode.B)
				{
					result.Errors.Add(new AnalyzedError
					{
						Code = "PATH_NOT_MAXIMIZING_ROOT_VALUE",
						Message = $"Выбранная ветвь {userFirstStep} не даёт максимального значения корня.",
						ElementType = "PathStep",
						PathStepIndex = 0,
						ExpectedPathNodeId = correctFirstStep,
						ActualPathNodeId = userFirstStep,
						ExpectedA = expectedNode.B,
						ActualA = selectedNode.B,
						SeverityScore = 3.0,
						GroupKey = "PATH_NOT_MAXIMIZING_ROOT_VALUE"
					});
				}
			}
		}
		private void AnalyzePruningTiming(
		ABNode root,
		HashSet<int> userPrunedIds,
		HashSet<int> expectedPrunedIds,
		Dictionary<int, NodeMeta> nodeMeta,
		ErrorAnalysisResult result)
		{
			if (root.SubNodes == null)
				return;

			var alpha = int.MinValue;

			foreach (var minNode in root.SubNodes)
			{
				if (minNode.SubNodes == null || minNode.SubNodes.Count == 0)
					continue;

				var currentMin = int.MaxValue;
				var pruningAllowedFromThisPoint = false;

				foreach (var leaf in minNode.SubNodes)
				{
					currentMin = Math.Min(currentMin, leaf.A);

					var shouldBePruned = expectedPrunedIds.Contains(leaf.Id);
					var userPruned = userPrunedIds.Contains(leaf.Id);

					// Если пользователь отсёк лист до того, как условие currentMin <= alpha стало возможным
					if (userPruned && !pruningAllowedFromThisPoint && !shouldBePruned)
					{
						var meta = nodeMeta.TryGetValue(leaf.Id, out var m) ? m : null;

						result.Errors.Add(new AnalyzedError
						{
							Code = "EARLY_PRUNING_ERROR",
							Message = $"Лист {leaf.Id} отсечён слишком рано: условие alpha-beta отсечения ещё не было выполнено.",
							NodeId = leaf.Id,
							TreeLevel = meta?.Depth,
							RootBranchId = meta?.RootBranchId,
							ElementType = "Leaf",
							IsUserPruned = true,
							IsExpectedPruned = false,
							SeverityScore = 3.5,
							GroupKey = $"EARLY_PRUNING_B{meta?.RootBranchId}"
						});
					}

					if (currentMin <= alpha)
					{
						pruningAllowedFromThisPoint = true;
					}

					// Если лист должен быть отсечён, но пользователь его не отсёк
					if (shouldBePruned && !userPruned)
					{
						var meta = nodeMeta.TryGetValue(leaf.Id, out var m) ? m : null;

						result.Errors.Add(new AnalyzedError
						{
							Code = "MISSED_PRUNING_ERROR",
							Message = $"Лист {leaf.Id} должен быть отсечён после выполнения условия alpha-beta отсечения.",
							NodeId = leaf.Id,
							TreeLevel = meta?.Depth,
							RootBranchId = meta?.RootBranchId,
							ElementType = "Leaf",
							IsUserPruned = false,
							IsExpectedPruned = true,
							SeverityScore = 3.0,
							GroupKey = $"MISSED_PRUNING_B{meta?.RootBranchId}"
						});
					}
				}

				alpha = Math.Max(alpha, currentMin);
			}
		}
		private void AnalyzeValueAffectedByWrongPruning(
		ABNode root,
		Dictionary<int, ABNodeDTO> userNodes,
		Dictionary<int, ABNodeDTO> correctNodes,
		HashSet<int> userPrunedIds,
		Dictionary<int, NodeMeta> nodeMeta,
		ErrorAnalysisResult result)
		{
			if (root.SubNodes == null)
				return;

			foreach (var minNode in root.SubNodes)
			{
				if (minNode.SubNodes == null || minNode.SubNodes.Count == 0)
					continue;

				if (!userNodes.TryGetValue(minNode.Id, out var userNode))
					continue;

				if (!correctNodes.TryGetValue(minNode.Id, out var correctNode))
					continue;

				if (userNode.B == correctNode.B)
					continue;

				var trueMinLeaf = minNode.SubNodes.MinBy(x => x.A);

				if (trueMinLeaf == null)
					continue;

				if (userPrunedIds.Contains(trueMinLeaf.Id))
				{
					var meta = nodeMeta.TryGetValue(minNode.Id, out var m) ? m : null;

					result.Errors.Add(new AnalyzedError
					{
						Code = "VALUE_AFFECTED_BY_WRONG_PRUNING",
						Message = $"Значение узла {minNode.Id} стало неверным из-за отсечения листа {trueMinLeaf.Id}, влияющего на минимум.",
						NodeId = minNode.Id,
						TreeLevel = meta?.Depth,
						RootBranchId = meta?.RootBranchId,
						ElementType = "InternalNode",
						ExpectedB = correctNode.B,
						ActualB = userNode.B,
						IsUserPruned = true,
						SeverityScore = 4.0,
						GroupKey = $"VALUE_AFFECTED_BY_PRUNING_B{meta?.RootBranchId}"
					});
				}
			}
		}
		private void AnalyzeCombinedSemanticCases(ErrorAnalysisResult result)
		{
			var hasNodeValueErrors = result.Errors.Any(e =>
				e.Code == "NODE_A_INCORRECT" ||
				e.Code == "NODE_B_INCORRECT" ||
				e.Code == "NODE_AB_INCORRECT" ||
				e.Code == "MIN_LEVEL_CONFUSION" ||
				e.Code == "ROOT_MAX_CONFUSION" ||
				e.Code == "VALUE_AFFECTED_BY_WRONG_PRUNING");

			var hasPruningErrors = result.Errors.Any(e =>
				e.Code == "PRUNED_REQUIRED_NODE" ||
				e.Code == "FAILED_TO_PRUNE_NODE" ||
				e.Code == "EARLY_PRUNING_ERROR" ||
				e.Code == "MISSED_PRUNING_ERROR");

			var hasPathErrors = result.Errors.Any(e =>
				e.Code.StartsWith("PATH") ||
				e.Code == "VALUE_CORRECT_PATH_WRONG" ||
				e.Code == "PATH_NOT_MAXIMIZING_ROOT_VALUE");

			if (!hasNodeValueErrors && hasPruningErrors)
			{
				result.Errors.Add(new AnalyzedError
				{
					Code = "VALUES_CORRECT_PRUNING_WRONG",
					Message = "Минимаксные значения рассчитаны верно, но отсечения выполнены неправильно.",
					ElementType = "Pruning",
					SeverityScore = 3.5,
					GroupKey = "VALUES_CORRECT_PRUNING_WRONG"
				});
			}

			var hasAnyValueErrors = result.Errors.Any(e =>
				e.Code == "NODE_A_INCORRECT" ||
				e.Code == "NODE_B_INCORRECT" ||
				e.Code == "NODE_AB_INCORRECT" ||
				e.Code == "MIN_LEVEL_CONFUSION" ||
				e.Code == "ROOT_MAX_CONFUSION" ||
				e.Code == "VALUE_AFFECTED_BY_WRONG_PRUNING");

			if (!hasAnyValueErrors && !hasPruningErrors && hasPathErrors)
			{
				result.Errors.Add(new AnalyzedError
				{
					Code = "VALUES_AND_PRUNING_CORRECT_PATH_WRONG",
					Message = "Значения и отсечения не содержат ошибок, но оптимальный путь выбран неверно.",
					ElementType = "PathStep",
					SeverityScore = 3.0,
					GroupKey = "VALUES_AND_PRUNING_CORRECT_PATH_WRONG"
				});
			}

			var hasDirectPruningErrors = result.Errors.Any(e =>
				e.Code == "PRUNED_REQUIRED_NODE" ||
				e.Code == "FAILED_TO_PRUNE_NODE" ||
				e.Code == "EARLY_PRUNING_ERROR" ||
				e.Code == "MISSED_PRUNING_ERROR");

			if (!hasNodeValueErrors && !hasPathErrors && hasPruningErrors && !hasDirectPruningErrors)
			{
				result.Errors.Add(new AnalyzedError
				{
					Code = "PRUNING_CORRECT_RESULT_WRONG_REASON",
					Message = "Итоговые значения и путь могут быть верными, но логика отсечения применена неверно.",
					ElementType = "Pruning",
					SeverityScore = 3.0,
					GroupKey = "PRUNING_CORRECT_RESULT_WRONG_REASON"
				});
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