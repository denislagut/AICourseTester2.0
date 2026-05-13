using AICourseTester.Models;
using AICourseTester.Models.Analysis;

namespace AICourseTester.Services.Analysis
{
	public static class ErrorCausalityBuilder
	{
		public static List<CausalErrorLink> Build(List<ErrorRecord> errors)
		{
			var links = new List<CausalErrorLink>();

			AddFifteenPuzzleLinks(errors, links);
			AddAlphaBetaLinks(errors, links);

			return RemoveDuplicates(links);
		}

		private static void AddFifteenPuzzleLinks(
			List<ErrorRecord> errors,
			List<CausalErrorLink> links)
		{
			foreach (var target in errors.Where(e => e.Code == "F_DERIVED_FROM_INCORRECT_COMPONENTS"))
			{
				var source = errors.FirstOrDefault(e =>
					e.NodeId == target.NodeId &&
					(e.Code == "H_INCORRECT" || e.Code == "G_INCORRECT"));

				if (source != null)
				{
					AddLink(links, source, target, "CAUSES", 1.0);
				}
			}
		}

		private static void AddAlphaBetaLinks(
			List<ErrorRecord> errors,
			List<CausalErrorLink> links)
		{
			foreach (var target in errors.Where(e => e.Code == "NODE_B_INCORRECT"))
			{
				var source = errors.FirstOrDefault(e =>
					e.NodeId == target.NodeId &&
					(e.Code == "MIN_LEVEL_CONFUSION" ||
					 e.Code == "VALUE_AFFECTED_BY_WRONG_PRUNING"));

				if (source != null)
				{
					AddLink(links, source, target, "EXPLAINS", 1.0);
				}
			}

			foreach (var target in errors.Where(e => e.Code == "NODE_A_INCORRECT"))
			{
				var source = errors.FirstOrDefault(e =>
					e.NodeId == target.NodeId &&
					e.Code == "ROOT_MAX_CONFUSION");

				if (source != null)
				{
					AddLink(links, source, target, "EXPLAINS", 1.0);
				}
			}

			foreach (var target in errors.Where(e => e.Code == "VALUE_AFFECTED_BY_WRONG_PRUNING"))
			{
				var source = errors.FirstOrDefault(e =>
					e.RootBranchId == target.RootBranchId &&
					(e.Code == "PRUNED_REQUIRED_NODE" ||
					 e.Code == "EARLY_PRUNING_ERROR"));

				if (source != null)
				{
					AddLink(links, source, target, "CAUSES", 0.9);
				}
			}

			foreach (var target in errors.Where(e => e.Code == "PATH_STEP_INCORRECT"))
			{
				var source = errors.FirstOrDefault(e =>
					e.Code == "NODE_A_INCORRECT" ||
					e.Code == "NODE_B_INCORRECT" ||
					e.Code == "ROOT_MAX_CONFUSION" ||
					e.Code == "MIN_LEVEL_CONFUSION" ||
					e.Code == "VALUE_AFFECTED_BY_WRONG_PRUNING");

				if (source != null)
				{
					AddLink(links, source, target, "MAY_CAUSE", 0.6);
				}
			}

			foreach (var target in errors.Where(e => e.Code == "PRUNING_PATH_INCONSISTENCY"))
			{
				var source = errors.FirstOrDefault(e =>
					e.Code == "PATH_THROUGH_PRUNED_BRANCH");

				if (source != null)
				{
					AddLink(links, source, target, "CAUSES", 1.0);
				}
			}

			foreach (var target in errors.Where(e => e.Code == "VALUE_PRUNING_INCONSISTENCY"))
			{
				var source = errors.FirstOrDefault(e =>
					e.Code == "PROCESSED_PRUNED_NODE");

				if (source != null)
				{
					AddLink(links, source, target, "CAUSES", 1.0);
				}
			}

			foreach (var target in errors.Where(e => e.Code == "VALUE_PATH_INCONSISTENCY"))
			{
				var source = errors.FirstOrDefault(e =>
					e.Code == "VALUE_CORRECT_PATH_WRONG" ||
					e.Code == "PATH_NOT_MAXIMIZING_ROOT_VALUE" ||
					e.Code == "PATH_STEP_INCORRECT");

				if (source != null)
				{
					AddLink(links, source, target, "CAUSES", 0.8);
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