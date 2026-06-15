using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services.Interfaces;

namespace AICourseTester.Services.Analysis
{
	public class ErrorCausalityBuilder : IErrorCausalityBuilder
	{
		public List<CausalErrorLink> Build(
			List<ErrorRecord> errors,
			List<CausalErrorRule> rules)
		{
			var links = new List<CausalErrorLink>();

			foreach (var rule in rules.Where(r => r.IsActive))
			{
				var sources = errors.Where(e => e.Code == rule.SourceErrorCode);
				var targets = errors.Where(e => e.Code == rule.TargetErrorCode);

				foreach (var source in sources)
				{
					foreach (var target in targets)
					{
						if (source.Id == target.Id)
							continue;

						if (rule.SameNodeRequired && source.NodeId != target.NodeId)
							continue;

						if (rule.SameRootBranchRequired &&
							source.RootBranchId != target.RootBranchId)
							continue;

						links.Add(new CausalErrorLink
						{
							SourceErrorId = source.Id,
							TargetErrorId = target.Id,
							RelationType = rule.RelationType,
							Weight = rule.Weight
						});
					}
				}
			}

			Console.WriteLine($"Errors count: {errors.Count}");
			Console.WriteLine($"Rules count: {rules.Count}");
			Console.WriteLine($"Links count: {links.Count}");

			foreach (var error in errors)
			{
				Console.WriteLine($"Error: {error.Code}, NodeId={error.NodeId}, RootBranchId={error.RootBranchId}");
			}

			foreach (var rule in rules)
			{
				Console.WriteLine($"Rule: {rule.SourceErrorCode} -> {rule.TargetErrorCode}, {rule.RelationType}");
			}

			return RemoveDuplicates(links);
		}


		private static void AddFifteenPuzzleLinks(
					List<ErrorRecord> errors,
					List<CausalErrorLink> links)
		{
			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.FDerivedFromIncorrectComponents))
			{
				foreach (var source in errors.Where(e =>
					e.NodeId == target.NodeId &&
					(e.Code == ErrorCodes.HIncorrect ||
					 e.Code == ErrorCodes.GIncorrect)))
				{
					AddLink(links, source, target, "CAUSES", 1.0);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.FFormulaInconsistency))
			{
				foreach (var source in errors.Where(e =>
					e.NodeId == target.NodeId &&
					(e.Code == ErrorCodes.HIncorrect ||
					 e.Code == ErrorCodes.GIncorrect)))
				{
					AddLink(links, source, target, "CONTEXT_FOR", 0.4);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.OpenOrderIncorrect))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.FIncorrect ||
					e.Code == ErrorCodes.FFormulaInconsistency ||
					e.Code == ErrorCodes.FDerivedFromIncorrectComponents ||
					e.Code == ErrorCodes.HIncorrect ||
					e.Code == ErrorCodes.GIncorrect))
				{
					AddLink(links, source, target, "MAY_CAUSE", 0.5);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.NodeMissing ||
				e.Code == ErrorCodes.NodeUnexpected))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.OpenOrderIncorrect))
				{
					AddLink(links, source, target, "MAY_CAUSE", 0.6);
				}
			}
		}

		private static void AddAlphaBetaLinks(
			List<ErrorRecord> errors,
			List<CausalErrorLink> links)
		{
			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.NodeBIncorrect))
			{
				foreach (var source in errors.Where(e =>
					e.NodeId == target.NodeId &&
					(e.Code == ErrorCodes.MinLevelConfusion ||
					 e.Code == ErrorCodes.ValueAffectedByWrongPruning)))
				{
					AddLink(links, source, target, "EXPLAINS", 1.0);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.NodeAIncorrect))
			{
				foreach (var source in errors.Where(e =>
					e.NodeId == target.NodeId &&
					e.Code == ErrorCodes.RootMaxConfusion))
				{
					AddLink(links, source, target, "EXPLAINS", 1.0);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.ValueAffectedByWrongPruning))
			{
				foreach (var source in errors.Where(e =>
					e.RootBranchId == target.RootBranchId &&
					(e.Code == ErrorCodes.PrunedRequiredNode ||
					 e.Code == ErrorCodes.EarlyPruningError)))
				{
					AddLink(links, source, target, "CAUSES", 1.0);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.EarlyPruningError))
			{
				foreach (var source in errors.Where(e =>
					e.NodeId == target.NodeId &&
					e.Code == ErrorCodes.PrunedRequiredNode))
				{
					AddLink(links, source, target, "EXPLAINS", 0.9);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.MissedPruningError))
			{
				foreach (var source in errors.Where(e =>
					e.NodeId == target.NodeId &&
					e.Code == ErrorCodes.FailedToPruneNode))
				{
					AddLink(links, source, target, "EXPLAINS", 0.9);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.PathStepIncorrect))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.NodeAIncorrect ||
					e.Code == ErrorCodes.NodeBIncorrect ||
					e.Code == ErrorCodes.NodeABIncorrect ||
					e.Code == ErrorCodes.RootMaxConfusion ||
					e.Code == ErrorCodes.MinLevelConfusion ||
					e.Code == ErrorCodes.ValueAffectedByWrongPruning))
				{
					AddLink(links, source, target, "MAY_CAUSE", 0.6);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.PathNotMaximizingRootValue))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.PathStepIncorrect ||
					e.Code == ErrorCodes.ValueCorrectPathWrong))
				{
					AddLink(links, source, target, "EXPLAINS", 0.8);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.ValueCorrectPathWrong))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.PathStepIncorrect))
				{
					AddLink(links, source, target, "CAUSES", 0.8);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.ValuesAndPruningCorrectPathWrong))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.PathStepIncorrect ||
					e.Code == ErrorCodes.ValueCorrectPathWrong ||
					e.Code == ErrorCodes.PathNotMaximizingRootValue))
				{
					AddLink(links, source, target, "SUMMARIZES", 0.7);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.ValuesCorrectPruningWrong))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.PrunedRequiredNode ||
					e.Code == ErrorCodes.FailedToPruneNode ||
					e.Code == ErrorCodes.EarlyPruningError ||
					e.Code == ErrorCodes.MissedPruningError ||
					e.Code == ErrorCodes.ProcessedPrunedNode))
				{
					AddLink(links, source, target, "SUMMARIZES", 0.7);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.PruningCorrectResultWrongReason))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.ValuesCorrectPruningWrong ||
					e.Code == ErrorCodes.EarlyPruningError ||
					e.Code == ErrorCodes.MissedPruningError))
				{
					AddLink(links, source, target, "EXPLAINS", 0.7);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.PruningPathInconsistency))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.PathThroughPrunedBranch))
				{
					AddLink(links, source, target, "CAUSES", 1.0);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.ValuePruningInconsistency))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.ProcessedPrunedNode ||
					e.Code == ErrorCodes.PrunedRequiredNode ||
					e.Code == ErrorCodes.EarlyPruningError))
				{
					AddLink(links, source, target, "CAUSES", 0.9);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.ValuePathInconsistency))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.ValueCorrectPathWrong ||
					e.Code == ErrorCodes.PathNotMaximizingRootValue ||
					e.Code == ErrorCodes.PathStepIncorrect))
				{
					AddLink(links, source, target, "CAUSES", 0.8);
				}
			}

			foreach (var target in errors.Where(e =>
				e.Code == ErrorCodes.NodeMissing ||
				e.Code == ErrorCodes.NodeUnexpected))
			{
				foreach (var source in errors.Where(e =>
					e.Code == ErrorCodes.FailedToPruneNode ||
					e.Code == ErrorCodes.MissedPruningError ||
					e.Code == ErrorCodes.PrunedRequiredNode ||
					e.Code == ErrorCodes.EarlyPruningError))
				{
					AddLink(links, source, target, "MAY_CAUSE", 0.5);
				}
			}
		}

		private static void AddLink(
			List<CausalErrorLink> links,
			ErrorRecord source,
			ErrorRecord target,
			string relationType,
			double weight)
		{
			if (source.Id == target.Id)
				return;

			links.Add(new CausalErrorLink
			{
				SourceErrorId = source.Id,
				TargetErrorId = target.Id,
				RelationType = relationType,
				Weight = weight
			});
		}

		private static List<CausalErrorLink> RemoveDuplicates(List<CausalErrorLink> links)
		{
			return links
				.GroupBy(l => new
				{
					l.SourceErrorId,
					l.TargetErrorId,
					l.RelationType
				})
				.Select(g => g.First())
				.ToList();
		}
	}
}