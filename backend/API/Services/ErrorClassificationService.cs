using AICourseTester.Data;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AICourseTester.Services
{
	public class ErrorClassificationService : IErrorClassificationService
	{
		private readonly MainDbContext _context;

		public ErrorClassificationService(MainDbContext context)
		{
			_context = context;
		}

		private bool IsMinLevel(int level)
		{
			return level % 2 == 1;
		}

		private static bool IsAlphaBetaValueError(string code)
		{
			return code == ErrorCodes.NodeAIncorrect ||
				   code == ErrorCodes.NodeBIncorrect ||
				   code == ErrorCodes.NodeABIncorrect ||
				   code == ErrorCodes.MinLevelConfusion ||
				   code == ErrorCodes.RootMaxConfusion ||
				   code == ErrorCodes.ValueAffectedByWrongPruning;
		}

		private static bool IsPathError(string code)
		{
			return code == ErrorCodes.PathStepIncorrect ||
				   code == ErrorCodes.PathIncomplete ||
				   code == ErrorCodes.PathRedundant ||
				   code == ErrorCodes.PathMissing ||
				   code == ErrorCodes.ValueCorrectPathWrong ||
				   code == ErrorCodes.PathNotMaximizingRootValue ||
				   code == ErrorCodes.ValuesAndPruningCorrectPathWrong;
		}

		private static bool IsPruningError(string code)
		{
			return code == ErrorCodes.PrunedRequiredNode ||
				   code == ErrorCodes.FailedToPruneNode ||
				   code == ErrorCodes.ProcessedPrunedNode ||
				   code == ErrorCodes.PathThroughPrunedBranch ||
				   code == ErrorCodes.EarlyPruningError ||
				   code == ErrorCodes.MissedPruningError ||
				   code == ErrorCodes.ValueAffectedByWrongPruning ||
				   code == ErrorCodes.ValuesCorrectPruningWrong ||
				   code == ErrorCodes.PruningCorrectResultWrongReason ||
				   code == ErrorCodes.PruningPathInconsistency ||
				   code == ErrorCodes.ValuePruningInconsistency;
		}

		private static string GetErrorCategory(string code)
		{
			if (code == ErrorCodes.NodeAIncorrect ||
				code == ErrorCodes.NodeBIncorrect ||
				code == ErrorCodes.NodeABIncorrect ||
				code == ErrorCodes.NodeMissing ||
				code == ErrorCodes.NodeUnexpected ||
				code == ErrorCodes.MinLevelConfusion ||
				code == ErrorCodes.RootMaxConfusion ||
				code == ErrorCodes.HIncorrect ||
				code == ErrorCodes.GIncorrect ||
				code == ErrorCodes.FIncorrect ||
				code == ErrorCodes.FFormulaInconsistency ||
				code == ErrorCodes.FDerivedFromIncorrectComponents)
			{
				return "Calculation";
			}

			if (IsPathError(code) ||
				code == ErrorCodes.OpenOrderIncorrect)
			{
				return "Strategy";
			}

			if (IsPruningError(code))
			{
				return "Pruning";
			}

			if (code == ErrorCodes.ValuePathInconsistency ||
				code == ErrorCodes.ValuePruningInconsistency ||
				code == ErrorCodes.PruningPathInconsistency)
			{
				return "Consistency";
			}

			return "General";
		}

		private static string GetSeverityLevel(double severity)
		{
			if (severity >= 4.0)
				return "Critical";

			if (severity >= 3.0)
				return "High";

			if (severity >= 2.0)
				return "Medium";

			return "Low";
		}

		public async Task ClassifyErrorsAsync(int alphaBetaId)
		{
			await EnsureReferenceDataAsync();

			var errors = await _context.ErrorRecords
				.Where(e => e.AlphaBetaId == alphaBetaId)
				.ToListAsync();

			if (errors.Count == 0)
			{
				return;
			}

			var errorTypes = await _context.ErrorTypes.ToListAsync();

			foreach (var error in errors)
			{
				var matchedType = MatchErrorType(error, errors, errorTypes);
				error.ErrorTypeId = matchedType?.Id;
			}

			ApplyCompositeClassification(errors, errorTypes);

			await _context.SaveChangesAsync();
		}

		private void ApplyCompositeClassification(List<ErrorRecord> errors, List<ErrorType> errorTypes)
		{
			var hasValueErrors = errors.Any(e => IsAlphaBetaValueError(e.Code));
			var hasPathErrors = errors.Any(e => IsPathError(e.Code));

			if (hasPathErrors && !hasValueErrors)
			{
				var pathConsistencyType = errorTypes.FirstOrDefault(t => t.Code == "PATH_VALUE_CONSISTENCY_ERROR");

				foreach (var error in errors.Where(e =>
					e.Code == ErrorCodes.PathStepIncorrect ||
					e.Code == ErrorCodes.PathIncomplete ||
					e.Code == ErrorCodes.PathRedundant ||
					e.Code == ErrorCodes.PathMissing))
				{
					error.ErrorTypeId = pathConsistencyType?.Id;
				}
			}

			if (errors.Any(e =>
				e.Code == ErrorCodes.PathThroughPrunedBranch ||
				e.Code == ErrorCodes.PruningPathInconsistency))
			{
				var conflictType = errorTypes.FirstOrDefault(t => t.Code == "PRUNING_PATH_CONFLICT_ERROR");

				foreach (var error in errors.Where(e =>
					e.Code == ErrorCodes.PathThroughPrunedBranch ||
					e.Code == ErrorCodes.PruningPathInconsistency))
				{
					error.ErrorTypeId = conflictType?.Id;
				}
			}

			if (errors.Any(e =>
				e.Code == ErrorCodes.ValuePathInconsistency ||
				e.Code == ErrorCodes.ValuePruningInconsistency ||
				e.Code == ErrorCodes.PruningPathInconsistency))
			{
				var consistencyType = errorTypes.FirstOrDefault(t => t.Code == "SOLUTION_CONSISTENCY_ERROR");

				foreach (var error in errors.Where(e =>
					e.Code == ErrorCodes.ValuePathInconsistency ||
					e.Code == ErrorCodes.ValuePruningInconsistency ||
					e.Code == ErrorCodes.PruningPathInconsistency))
				{
					error.ErrorTypeId = consistencyType?.Id;
				}
			}

			var groupedByBranch = errors
				.Where(e => e.RootBranchId.HasValue &&
							e.Code == ErrorCodes.ProcessedPrunedNode)
				.GroupBy(e => e.RootBranchId!.Value);

			var boundaryType = errorTypes.FirstOrDefault(t => t.Code == "PRUNING_BOUNDARY_ERROR");

			foreach (var group in groupedByBranch)
			{
				if (group.Count() >= 2)
				{
					foreach (var error in group)
					{
						error.ErrorTypeId = boundaryType?.Id;
					}
				}
			}
		}

		private ErrorType? MatchFifteenPuzzleErrorType(
			ErrorRecord error,
			List<ErrorRecord> allErrors,
			List<ErrorType> errorTypes)
		{
			if (error.Code == ErrorCodes.HIncorrect)
			{
				if (error.GroupKey == "HAMMING_H_VALUE")
				{
					return errorTypes.FirstOrDefault(t => t.Code == "HAMMING_HEURISTIC_CALCULATION_ERROR");
				}

				if (error.GroupKey == "MANHATTAN_H_VALUE")
				{
					return errorTypes.FirstOrDefault(t => t.Code == "MANHATTAN_HEURISTIC_CALCULATION_ERROR");
				}

				return errorTypes.FirstOrDefault(t => t.Code == "HEURISTIC_VALUE_CALCULATION_ERROR");
			}

			if (error.Code == ErrorCodes.GIncorrect)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "PATH_COST_CALCULATION_ERROR");
			}

			if (error.Code == ErrorCodes.FFormulaInconsistency)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "F_FORMULA_CONSISTENCY_ERROR");
			}

			if (error.Code == ErrorCodes.FDerivedFromIncorrectComponents)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "DEPENDENT_VALUE_ERROR");
			}

			if (error.Code == ErrorCodes.FIncorrect)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "F_SCORE_CALCULATION_ERROR");
			}

			if (error.Code == ErrorCodes.OpenOrderIncorrect)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "NODE_EXPANSION_ORDER_ERROR");
			}

			if (error.Code == ErrorCodes.NodeMissing ||
				error.Code == ErrorCodes.NodeUnexpected)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "A_STAR_SEARCH_STRATEGY_ERROR");
			}

			return errorTypes.FirstOrDefault(t => t.Code == "UNCLASSIFIED_ERROR");
		}

		public async Task ClassifyFifteenPuzzleErrorsAsync(int fifteenPuzzleId)
		{
			await EnsureReferenceDataAsync();

			var errors = await _context.ErrorRecords
				.Where(e => e.TaskType == "FifteenPuzzle" && e.FifteenPuzzleId == fifteenPuzzleId)
				.ToListAsync();

			if (errors.Count == 0)
				return;

			var errorTypes = await _context.ErrorTypes.ToListAsync();

			foreach (var error in errors)
			{
				var matchedType = MatchFifteenPuzzleErrorType(error, errors, errorTypes);
				error.ErrorTypeId = matchedType?.Id;
			}

			await _context.SaveChangesAsync();
		}

		private ErrorType? MatchErrorType(
			ErrorRecord error,
			List<ErrorRecord> allErrors,
			List<ErrorType> errorTypes)
		{
			if (error.TaskType == "FifteenPuzzle")
			{
				return MatchFifteenPuzzleErrorType(error, allErrors, errorTypes);
			}

			if (error.Code == ErrorCodes.NodeAIncorrect ||
				error.Code == ErrorCodes.NodeBIncorrect ||
				error.Code == ErrorCodes.NodeABIncorrect)
			{
				if (error.TreeLevel.HasValue)
				{
					if (IsMinLevel(error.TreeLevel.Value))
					{
						return errorTypes.FirstOrDefault(t => t.Code == "MIN_LEVEL_AGGREGATION_ERROR");
					}

					if (error.TreeLevel.Value > 0)
					{
						return errorTypes.FirstOrDefault(t => t.Code == "MAX_LEVEL_AGGREGATION_ERROR");
					}
				}

				return errorTypes.FirstOrDefault(t => t.Code == "MINIMAX_VALUE_CALCULATION_ERROR");
			}

			if (error.Code == ErrorCodes.PathStepIncorrect ||
				error.Code == ErrorCodes.PathIncomplete ||
				error.Code == ErrorCodes.PathRedundant ||
				error.Code == ErrorCodes.PathMissing)
			{
				var hasValueErrors = allErrors.Any(e => IsAlphaBetaValueError(e.Code));

				if (!hasValueErrors)
				{
					return errorTypes.FirstOrDefault(t => t.Code == "PATH_VALUE_CONSISTENCY_ERROR");
				}

				return errorTypes.FirstOrDefault(t => t.Code == "OPTIMAL_PATH_SELECTION_ERROR");
			}

			if (error.Code == ErrorCodes.OverPruningSubtree ||
				error.Code == ErrorCodes.UnderPruningSubtree ||
				error.Code == ErrorCodes.ProcessedPrunedNode)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "PRUNING_BOUNDARY_ERROR");
			}

			if (error.Code == ErrorCodes.PathThroughPrunedBranch ||
				error.Code == ErrorCodes.PruningPathInconsistency)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "PRUNING_PATH_CONFLICT_ERROR");
			}

			if (error.Code == ErrorCodes.ValuePathInconsistency ||
				error.Code == ErrorCodes.ValuePruningInconsistency)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "SOLUTION_CONSISTENCY_ERROR");
			}

			if (error.Code == ErrorCodes.NodeMissing ||
				error.Code == ErrorCodes.NodeUnexpected)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "TREE_STRUCTURE_PROCESSING_ERROR");
			}

			if (error.Code == ErrorCodes.MinLevelConfusion ||
				error.Code == ErrorCodes.RootMaxConfusion)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "MIN_MAX_ROLE_CONFUSION_ERROR");
			}

			if (error.Code == ErrorCodes.ValueCorrectPathWrong ||
				error.Code == ErrorCodes.PathNotMaximizingRootValue ||
				error.Code == ErrorCodes.ValuesAndPruningCorrectPathWrong)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "OPTIMAL_PATH_SELECTION_ERROR");
			}

			if (error.Code == ErrorCodes.EarlyPruningError ||
				error.Code == ErrorCodes.PrunedRequiredNode)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "EARLY_PRUNING_ERROR_TYPE");
			}

			if (error.Code == ErrorCodes.MissedPruningError ||
				error.Code == ErrorCodes.FailedToPruneNode)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "MISSED_PRUNING_ERROR_TYPE");
			}

			if (error.Code == ErrorCodes.ValueAffectedByWrongPruning)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "VALUE_PRUNING_DEPENDENCY_ERROR");
			}

			if (error.Code == ErrorCodes.ValuesCorrectPruningWrong ||
				error.Code == ErrorCodes.PruningCorrectResultWrongReason)
			{
				return errorTypes.FirstOrDefault(t => t.Code == "PRUNING_LOGIC_ERROR");
			}

			return errorTypes.FirstOrDefault(t => t.Code == "UNCLASSIFIED_ERROR");
		}

		private async Task EnsureReferenceDataAsync()
		{
			var requiredErrorTypes = new List<ErrorType>
			{
				new ErrorType
				{
					Code = "MINIMAX_VALUE_CALCULATION_ERROR",
					Name = "Ошибка вычисления минимаксных значений",
					Description = "Неверное вычисление значений узлов дерева.",
					DefaultSeverity = 2.0
				},
				new ErrorType
				{
					Code = "MIN_LEVEL_AGGREGATION_ERROR",
					Name = "Ошибка выбора минимального значения",
					Description = "Ошибка на уровне MIN-узлов.",
					DefaultSeverity = 2.5
				},
				new ErrorType
				{
					Code = "MAX_LEVEL_AGGREGATION_ERROR",
					Name = "Ошибка выбора максимального значения",
					Description = "Ошибка на уровне MAX-узлов.",
					DefaultSeverity = 2.5
				},
				new ErrorType
				{
					Code = "OPTIMAL_PATH_SELECTION_ERROR",
					Name = "Ошибка выбора оптимального пути",
					Description = "Неверный выбор оптимального пути или неполное указание пути.",
					DefaultSeverity = 3.0
				},
				new ErrorType
				{
					Code = "PATH_VALUE_CONSISTENCY_ERROR",
					Name = "Ошибка согласованности пути и значений",
					Description = "Выбранный путь не соответствует рассчитанным значениям.",
					DefaultSeverity = 3.0
				},
				new ErrorType
				{
					Code = "PRUNING_BOUNDARY_ERROR",
					Name = "Ошибка границ отсечения",
					Description = "Неверно определены ветви, охватываемые отсечением.",
					DefaultSeverity = 3.5
				},
				new ErrorType
				{
					Code = "PRUNING_PATH_CONFLICT_ERROR",
					Name = "Конфликт пути и отсечения",
					Description = "Выбранный путь противоречит выполненным отсечениям.",
					DefaultSeverity = 4.0
				},
				new ErrorType
				{
					Code = "SOLUTION_CONSISTENCY_ERROR",
					Name = "Ошибка внутренней согласованности решения",
					Description = "Значения, путь и отсечения противоречат друг другу.",
					DefaultSeverity = 3.5
				},
				new ErrorType
				{
					Code = "TREE_STRUCTURE_PROCESSING_ERROR",
					Name = "Ошибка обработки структуры дерева",
					Description = "Пропущены обязательные узлы или обработаны лишние.",
					DefaultSeverity = 2.0
				},
				new ErrorType
				{
					Code = "UNCLASSIFIED_ERROR",
					Name = "Неклассифицированная ошибка",
					Description = "Ошибка не была сопоставлена с известным типом.",
					DefaultSeverity = 1.0
				},
				new ErrorType
				{
					Code = "HEURISTIC_VALUE_CALCULATION_ERROR",
					Name = "Ошибка расчёта эвристики",
					Description = "Неверно рассчитано значение эвристической функции h(n).",
					DefaultSeverity = 3.0
				},
				new ErrorType
				{
					Code = "PATH_COST_CALCULATION_ERROR",
					Name = "Ошибка расчёта стоимости пути",
					Description = "Неверно рассчитано значение g(n).",
					DefaultSeverity = 2.5
				},
				new ErrorType
				{
					Code = "F_SCORE_CALCULATION_ERROR",
					Name = "Ошибка расчёта оценочной функции",
					Description = "Неверно рассчитано значение f(n).",
					DefaultSeverity = 2.5
				},
				new ErrorType
				{
					Code = "F_FORMULA_CONSISTENCY_ERROR",
					Name = "Ошибка формулы f = g + h",
					Description = "Значение f(n) не соответствует сумме g(n) и h(n).",
					DefaultSeverity = 3.5
				},
				new ErrorType
				{
					Code = "NODE_EXPANSION_ORDER_ERROR",
					Name = "Ошибка порядка раскрытия узлов",
					Description = "Неверно выбран порядок раскрытия узлов в алгоритме A*.",
					DefaultSeverity = 3.5
				},
				new ErrorType
				{
					Code = "A_STAR_SEARCH_STRATEGY_ERROR",
					Name = "Ошибка стратегии поиска A*",
					Description = "Пользователь обработал лишние узлы или пропустил необходимые.",
					DefaultSeverity = 3.0
				},
				new ErrorType
				{
					Code = "HAMMING_HEURISTIC_CALCULATION_ERROR",
					Name = "Ошибка расчёта расстояния Хемминга",
					Description = "Неверно рассчитана эвристика Хемминга.",
					DefaultSeverity = 3.0
				},
				new ErrorType
				{
					Code = "MANHATTAN_HEURISTIC_CALCULATION_ERROR",
					Name = "Ошибка расчёта Манхэттенского расстояния",
					Description = "Неверно рассчитана манхэттенская эвристика.",
					DefaultSeverity = 3.0
				},
				new ErrorType
				{
					Code = "DEPENDENT_VALUE_ERROR",
					Name = "Производная ошибка значения",
					Description = "Значение отличается от эталона как следствие ошибки в другом параметре.",
					DefaultSeverity = 1.0
				},
				new ErrorType
				{
					Code = "MIN_MAX_ROLE_CONFUSION_ERROR",
					Name = "Ошибка различения MIN и MAX",
					Description = "Студент перепутал роли минимизирующего и максимизирующего уровней.",
					DefaultSeverity = 3.5
				},
				new ErrorType
				{
					Code = "EARLY_PRUNING_ERROR_TYPE",
					Name = "Слишком раннее отсечение",
					Description = "Отсечение выполнено до выполнения условия alpha-beta.",
					DefaultSeverity = 3.5
				},
				new ErrorType
				{
					Code = "MISSED_PRUNING_ERROR_TYPE",
					Name = "Пропущенное отсечение",
					Description = "Студент не выполнил отсечение после наступления условия alpha-beta.",
					DefaultSeverity = 3.0
				},
				new ErrorType
				{
					Code = "VALUE_PRUNING_DEPENDENCY_ERROR",
					Name = "Ошибка значения из-за неверного отсечения",
					Description = "Значение узла стало неверным из-за отсечения значимого дочернего узла.",
					DefaultSeverity = 4.0
				},
				new ErrorType
				{
					Code = "PRUNING_LOGIC_ERROR",
					Name = "Ошибка логики alpha-beta отсечения",
					Description = "Итог решения может быть частично верным, но логика отсечения применена неверно.",
					DefaultSeverity = 3.5
				}
			};

			var existingErrorTypes = await _context.ErrorTypes.ToListAsync();

			foreach (var requiredType in requiredErrorTypes)
			{
				var existing = existingErrorTypes.FirstOrDefault(t => t.Code == requiredType.Code);
				if (existing == null)
				{
					await _context.ErrorTypes.AddAsync(requiredType);
				}
				else
				{
					existing.Name = requiredType.Name;
					existing.Description = requiredType.Description;
					existing.DefaultSeverity = requiredType.DefaultSeverity;
				}
			}

			await _context.SaveChangesAsync();

			var requiredAspects = new List<KnowledgeAspect>
			{
				new KnowledgeAspect
				{
					Name = "Принцип вычисления минимаксных значений",
					Description = "Понимание вычисления значений узлов дерева на основе дочерних узлов.",
					TopicName = "Минимакс и альфа-бета отсечение",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Выбор оптимального хода",
					Description = "Понимание того, как выбирается оптимальный путь в дереве игры.",
					TopicName = "Минимакс и альфа-бета отсечение",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Понимание условий альфа-бета отсечения",
					Description = "Понимание того, когда отсечение допустимо.",
					TopicName = "Минимакс и альфа-бета отсечение",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Понимание границ действия отсечения",
					Description = "Понимание того, какие ветви охватывает отсечение.",
					TopicName = "Минимакс и альфа-бета отсечение",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Корректная обработка структуры дерева",
					Description = "Понимание того, какие узлы должны быть обработаны и заполнены.",
					TopicName = "Минимакс и альфа-бета отсечение",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Согласованность решения",
					Description = "Согласованность вычисленных значений, выбранного пути, раскрытых узлов и выполненных отсечений.",
					TopicName = "Алгоритмы поиска",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Различение ролей MIN и MAX",
					Description = "Понимание различий между минимизирующим и максимизирующим уровнями дерева.",
					TopicName = "Минимакс и альфа-бета отсечение",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Определение момента отсечения",
					Description = "Понимание момента, когда условие alpha-beta отсечения становится выполненным.",
					TopicName = "Минимакс и альфа-бета отсечение",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Последствия неверного отсечения",
					Description = "Понимание того, как неверное отсечение влияет на значения узлов и итоговое решение.",
					TopicName = "Минимакс и альфа-бета отсечение",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Обработка равных значений f(n)",
					Description = "Понимание допустимых вариантов раскрытия узлов при равных значениях оценочной функции.",
					TopicName = "Алгоритм A*",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Расчёт эвристической функции",
					Description = "Понимание назначения и расчёта эвристической функции h(n).",
					TopicName = "Алгоритм A*",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Расчёт расстояния Хемминга",
					Description = "Понимание расчёта эвристики по количеству неправильно расположенных фишек.",
					TopicName = "Алгоритм A*",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Расчёт Манхэттенского расстояния",
					Description = "Понимание расчёта суммы расстояний фишек до целевых позиций.",
					TopicName = "Алгоритм A*",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Расчёт стоимости пути g(n)",
					Description = "Понимание накопленной стоимости пути от начального состояния.",
					TopicName = "Алгоритм A*",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Расчёт оценочной функции f(n)",
					Description = "Понимание формулы f(n)=g(n)+h(n).",
					TopicName = "Алгоритм A*",
					IsActive = true
				},
				new KnowledgeAspect
				{
					Name = "Выбор узла для раскрытия в A*",
					Description = "Понимание принципа выбора следующего узла с минимальным f(n).",
					TopicName = "Алгоритм A*",
					IsActive = true
				}
			};

			var existingAspects = await _context.KnowledgeAspects.ToListAsync();

			foreach (var requiredAspect in requiredAspects)
			{
				var existing = existingAspects.FirstOrDefault(a => a.Name == requiredAspect.Name);
				if (existing == null)
				{
					await _context.KnowledgeAspects.AddAsync(requiredAspect);
				}
				else
				{
					existing.Description = requiredAspect.Description;
					existing.TopicName = requiredAspect.TopicName;
					existing.IsActive = requiredAspect.IsActive;
				}
			}

			await _context.SaveChangesAsync();

			var errorTypes = await _context.ErrorTypes.ToListAsync();
			var aspects = await _context.KnowledgeAspects.ToListAsync();
			var existingLinks = await _context.ErrorTypeAspects.ToListAsync();

			var requiredLinks = new List<(string errorTypeCode, string aspectName, double weight)>
			{
				("MINIMAX_VALUE_CALCULATION_ERROR", "Принцип вычисления минимаксных значений", 1.0),
				("MIN_LEVEL_AGGREGATION_ERROR", "Принцип вычисления минимаксных значений", 1.0),
				("MAX_LEVEL_AGGREGATION_ERROR", "Принцип вычисления минимаксных значений", 1.0),

				("OPTIMAL_PATH_SELECTION_ERROR", "Выбор оптимального хода", 1.0),
				("PATH_VALUE_CONSISTENCY_ERROR", "Выбор оптимального хода", 1.0),
				("PATH_VALUE_CONSISTENCY_ERROR", "Согласованность решения", 0.7),

				("PRUNING_BOUNDARY_ERROR", "Понимание границ действия отсечения", 1.0),
				("PRUNING_BOUNDARY_ERROR", "Понимание условий альфа-бета отсечения", 0.6),

				("MIN_MAX_ROLE_CONFUSION_ERROR", "Принцип вычисления минимаксных значений", 1.0),

				("EARLY_PRUNING_ERROR_TYPE", "Понимание условий альфа-бета отсечения", 1.0),

				("MISSED_PRUNING_ERROR_TYPE", "Понимание условий альфа-бета отсечения", 0.8),
				("MISSED_PRUNING_ERROR_TYPE", "Понимание границ действия отсечения", 0.6),

				("VALUE_PRUNING_DEPENDENCY_ERROR", "Понимание условий альфа-бета отсечения", 0.8),
				("VALUE_PRUNING_DEPENDENCY_ERROR", "Принцип вычисления минимаксных значений", 0.6),

				("PRUNING_LOGIC_ERROR", "Понимание условий альфа-бета отсечения", 1.0),
				("PRUNING_LOGIC_ERROR", "Согласованность решения", 0.5),

				("PRUNING_PATH_CONFLICT_ERROR", "Понимание границ действия отсечения", 0.8),
				("PRUNING_PATH_CONFLICT_ERROR", "Согласованность решения", 1.0),

				("SOLUTION_CONSISTENCY_ERROR", "Согласованность решения", 1.0),

				("MIN_MAX_ROLE_CONFUSION_ERROR", "Различение ролей MIN и MAX", 1.0),
				("MIN_LEVEL_AGGREGATION_ERROR", "Различение ролей MIN и MAX", 0.7),
				("MAX_LEVEL_AGGREGATION_ERROR", "Различение ролей MIN и MAX", 0.7),

				("EARLY_PRUNING_ERROR_TYPE", "Определение момента отсечения", 1.0),
				("MISSED_PRUNING_ERROR_TYPE", "Определение момента отсечения", 0.8),
				("PRUNING_LOGIC_ERROR", "Определение момента отсечения", 0.7),

				("VALUE_PRUNING_DEPENDENCY_ERROR", "Последствия неверного отсечения", 1.0),
				("PRUNING_PATH_CONFLICT_ERROR", "Последствия неверного отсечения", 0.7),
				("SOLUTION_CONSISTENCY_ERROR", "Последствия неверного отсечения", 0.5),

				("NODE_EXPANSION_ORDER_ERROR", "Обработка равных значений f(n)", 0.5),
				("A_STAR_SEARCH_STRATEGY_ERROR", "Обработка равных значений f(n)", 0.4),

				("TREE_STRUCTURE_PROCESSING_ERROR", "Корректная обработка структуры дерева", 1.0),

				("UNCLASSIFIED_ERROR", "Корректная обработка структуры дерева", 0.1),

				("HEURISTIC_VALUE_CALCULATION_ERROR", "Расчёт эвристической функции", 1.0),
				("HAMMING_HEURISTIC_CALCULATION_ERROR", "Расчёт расстояния Хемминга", 1.0),
				("MANHATTAN_HEURISTIC_CALCULATION_ERROR", "Расчёт Манхэттенского расстояния", 1.0),

				("PATH_COST_CALCULATION_ERROR", "Расчёт стоимости пути g(n)", 1.0),

				("F_SCORE_CALCULATION_ERROR", "Расчёт оценочной функции f(n)", 1.0),
				("F_FORMULA_CONSISTENCY_ERROR", "Расчёт оценочной функции f(n)", 1.0),

				("DEPENDENT_VALUE_ERROR", "Расчёт оценочной функции f(n)", 0.4),

				("NODE_EXPANSION_ORDER_ERROR", "Выбор узла для раскрытия в A*", 1.0),
				("A_STAR_SEARCH_STRATEGY_ERROR", "Выбор узла для раскрытия в A*", 0.8)
			};

			foreach (var (errorTypeCode, aspectName, weight) in requiredLinks)
			{
				var errorType = errorTypes.FirstOrDefault(t => t.Code == errorTypeCode);
				var aspect = aspects.FirstOrDefault(a => a.Name == aspectName);

				if (errorType == null || aspect == null)
				{
					continue;
				}

				var existingLink = existingLinks.FirstOrDefault(l =>
					l.ErrorTypeId == errorType.Id &&
					l.KnowledgeAspectId == aspect.Id);

				if (existingLink == null)
				{
					await _context.ErrorTypeAspects.AddAsync(new ErrorTypeAspect
					{
						ErrorTypeId = errorType.Id,
						KnowledgeAspectId = aspect.Id,
						Weight = weight
					});
				}
				else
				{
					existingLink.Weight = weight;
				}
			}

			var requiredLinkKeys = requiredLinks
				.Select(link =>
				{
					var errorType = errorTypes.FirstOrDefault(t => t.Code == link.errorTypeCode);
					var aspect = aspects.FirstOrDefault(a => a.Name == link.aspectName);

					if (errorType == null || aspect == null)
						return null;

					return new
					{
						ErrorTypeId = errorType.Id,
						KnowledgeAspectId = aspect.Id
					};
				})
				.Where(x => x != null)
				.ToList();

			var obsoleteLinks = existingLinks
				.Where(existing => !requiredLinkKeys.Any(required =>
					required!.ErrorTypeId == existing.ErrorTypeId &&
					required.KnowledgeAspectId == existing.KnowledgeAspectId))
				.ToList();

			_context.ErrorTypeAspects.RemoveRange(obsoleteLinks);

			await _context.SaveChangesAsync();
		}
	}
}