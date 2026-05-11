using AICourseTester.Data;
using AICourseTester.Models;
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

			// Проход 1: базовая классификация
			foreach (var error in errors)
			{
				var matchedType = MatchErrorType(error, errors, errorTypes);
				error.ErrorTypeId = matchedType?.Id;
			}

			// Проход 2: составные правила
			ApplyCompositeClassification(errors, errorTypes);

			await _context.SaveChangesAsync();
		}

		private void ApplyCompositeClassification(List<ErrorRecord> errors, List<ErrorType> errorTypes)
		{
			var hasValueErrors = errors.Any(e =>
				e.Code == "NODE_A_INCORRECT" ||
				e.Code == "NODE_B_INCORRECT" ||
				e.Code == "NODE_AB_INCORRECT");

			var hasPathErrors = errors.Any(e => e.Code.StartsWith("PATH"));
			var hasPruningErrors = errors.Any(e =>
				e.Code.StartsWith("PRUN") || e.Code.Contains("PRUNE"));

			// 1. Путь неверен, но значения в целом верны
			if (hasPathErrors && !hasValueErrors)
			{
				var pathConsistencyType = errorTypes.FirstOrDefault(t => t.Code == "PATH_VALUE_CONSISTENCY_ERROR");

				foreach (var error in errors.Where(e => e.Code.StartsWith("PATH")))
				{
					error.ErrorTypeId = pathConsistencyType?.Id;
				}
			}

			// 2. Есть конфликт пути и pruning
			if (errors.Any(e =>
				e.Code == "PATH_THROUGH_PRUNED_BRANCH" ||
				e.Code == "PRUNING_PATH_INCONSISTENCY"))
			{
				var conflictType = errorTypes.FirstOrDefault(t => t.Code == "PRUNING_PATH_CONFLICT_ERROR");

				foreach (var error in errors.Where(e =>
					e.Code == "PATH_THROUGH_PRUNED_BRANCH" ||
					e.Code == "PRUNING_PATH_INCONSISTENCY"))
				{
					error.ErrorTypeId = conflictType?.Id;
				}
			}

			// 3. Есть общая несогласованность решения
			if (errors.Any(e =>
				e.Code == "VALUE_PATH_INCONSISTENCY" ||
				e.Code == "VALUE_PRUNING_INCONSISTENCY" ||
				e.Code == "PRUNING_PATH_INCONSISTENCY"))
			{
				var consistencyType = errorTypes.FirstOrDefault(t => t.Code == "SOLUTION_CONSISTENCY_ERROR");

				foreach (var error in errors.Where(e =>
					e.Code == "VALUE_PATH_INCONSISTENCY" ||
					e.Code == "VALUE_PRUNING_INCONSISTENCY" ||
					e.Code == "PRUNING_PATH_INCONSISTENCY"))
				{
					error.ErrorTypeId = consistencyType?.Id;
				}
			}

			// 4. Если pruning-ошибки сконцентрированы в одном поддереве,
			// усиливаем их как boundary error
			var groupedByBranch = errors
				.Where(e => e.RootBranchId.HasValue &&
							(e.Code == "PROCESSED_PRUNED_NODE" ||
							 e.Code == "FAILED_TO_PRUNE_NODE" ||
							 e.Code == "PRUNED_REQUIRED_NODE"))
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
			if (error.Code == "H_INCORRECT")
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

			if (error.Code == "G_INCORRECT")
			{
				return errorTypes.FirstOrDefault(t => t.Code == "PATH_COST_CALCULATION_ERROR");
			}

			if (error.Code == "F_FORMULA_INCONSISTENCY")
			{
				return errorTypes.FirstOrDefault(t => t.Code == "F_FORMULA_CONSISTENCY_ERROR");
			}

			if (error.Code == "F_DERIVED_FROM_INCORRECT_COMPONENTS")
			{
				return errorTypes.FirstOrDefault(t => t.Code == "DEPENDENT_VALUE_ERROR");
			}

			if (error.Code == "F_INCORRECT")
			{
				return errorTypes.FirstOrDefault(t => t.Code == "F_SCORE_CALCULATION_ERROR");
			}

			if (error.Code == "OPEN_ORDER_INCORRECT")
			{
				return errorTypes.FirstOrDefault(t => t.Code == "NODE_EXPANSION_ORDER_ERROR");
			}

			if (error.Code == "NODE_MISSING" ||
				error.Code == "NODE_UNEXPECTED")
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
			// 1. Ошибки вычисления значений узлов
			if (error.Code == "NODE_A_INCORRECT" ||
				error.Code == "NODE_B_INCORRECT" ||
				error.Code == "NODE_AB_INCORRECT")
			{
				if (error.TreeLevel.HasValue)
				{
					// В вашей задаче можно договориться:
					// четный/нечетный уровень интерпретировать как MAX/MIN
					// в зависимости от дерева и корня
					if (error.TreeLevel.Value % 2 == 1)
					{
						return errorTypes.FirstOrDefault(t => t.Code == "MIN_LEVEL_AGGREGATION_ERROR");
					}
					else if (error.TreeLevel.Value > 0)
					{
						return errorTypes.FirstOrDefault(t => t.Code == "MAX_LEVEL_AGGREGATION_ERROR");
					}
				}

				return errorTypes.FirstOrDefault(t => t.Code == "MINIMAX_VALUE_CALCULATION_ERROR");
			}

			// 2. Ошибки выбора пути
			if (error.Code == "PATH_STEP_INCORRECT" ||
				error.Code == "PATH_INCOMPLETE" ||
				error.Code == "PATH_REDUNDANT" ||
				error.Code == "PATH_MISSING")
			{
				var hasValueErrors = allErrors.Any(e =>
					e.Code == "NODE_A_INCORRECT" ||
					e.Code == "NODE_B_INCORRECT" ||
					e.Code == "NODE_AB_INCORRECT");

				if (!hasValueErrors)
				{
					return errorTypes.FirstOrDefault(t => t.Code == "PATH_VALUE_CONSISTENCY_ERROR");
				}

				return errorTypes.FirstOrDefault(t => t.Code == "OPTIMAL_PATH_SELECTION_ERROR");
			}

			// 3. Ошибки pruning — решение об отсечении
			if (error.Code == "PRUNED_REQUIRED_NODE" ||
				error.Code == "FAILED_TO_PRUNE_NODE")
			{
				return errorTypes.FirstOrDefault(t => t.Code == "PRUNING_DECISION_ERROR");
			}

			// 4. Ошибки pruning — границы отсечения
			if (error.Code == "OVER_PRUNING_SUBTREE" ||
				error.Code == "UNDER_PRUNING_SUBTREE" ||
				error.Code == "PROCESSED_PRUNED_NODE")
			{
				return errorTypes.FirstOrDefault(t => t.Code == "PRUNING_BOUNDARY_ERROR");
			}

			// 5. Конфликт пути и отсечения
			if (error.Code == "PATH_THROUGH_PRUNED_BRANCH" ||
				error.Code == "PRUNING_PATH_INCONSISTENCY")
			{
				return errorTypes.FirstOrDefault(t => t.Code == "PRUNING_PATH_CONFLICT_ERROR");
			}

			// 6. Согласованность решения
			if (error.Code == "VALUE_PATH_INCONSISTENCY" ||
				error.Code == "VALUE_PRUNING_INCONSISTENCY")
			{
				return errorTypes.FirstOrDefault(t => t.Code == "SOLUTION_CONSISTENCY_ERROR");
			}

			// 7. Ошибки структуры
			if (error.Code == "NODE_MISSING" ||
				error.Code == "NODE_UNEXPECTED")
			{
				return errorTypes.FirstOrDefault(t => t.Code == "TREE_STRUCTURE_PROCESSING_ERROR");
			}

			// 8. Резерв
			return errorTypes.FirstOrDefault(t => t.Code == "UNCLASSIFIED_ERROR");
		}

		private async Task EnsureReferenceDataAsync()
		{
			// ---------- ErrorTypes ----------
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
			Code = "PRUNING_DECISION_ERROR",
			Name = "Ошибка принятия решения об отсечении",
			Description = "Отсечение выполнено или пропущено в неверном месте.",
			DefaultSeverity = 3.5
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

			// ---------- KnowledgeAspects ----------
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

			// ---------- ErrorTypeAspect ----------
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

				("PRUNING_DECISION_ERROR", "Понимание условий альфа-бета отсечения", 1.0),

				("PRUNING_BOUNDARY_ERROR", "Понимание границ действия отсечения", 1.0),
				("PRUNING_BOUNDARY_ERROR", "Понимание условий альфа-бета отсечения", 0.6),

				("PRUNING_PATH_CONFLICT_ERROR", "Понимание границ действия отсечения", 0.8),
				("PRUNING_PATH_CONFLICT_ERROR", "Согласованность решения", 1.0),

				("SOLUTION_CONSISTENCY_ERROR", "Согласованность решения", 1.0),

				("TREE_STRUCTURE_PROCESSING_ERROR", "Корректная обработка структуры дерева", 1.0),

				("UNCLASSIFIED_ERROR", "Корректная обработка структуры дерева", 0.1),

				("HEURISTIC_VALUE_CALCULATION_ERROR", "Расчёт эвристической функции", 1.0),

				("HAMMING_HEURISTIC_CALCULATION_ERROR", "Расчёт расстояния Хемминга", 1.0),

				("MANHATTAN_HEURISTIC_CALCULATION_ERROR", "Расчёт Манхэттенского расстояния", 1.0),

				("PATH_COST_CALCULATION_ERROR", "Расчёт стоимости пути g(n)", 1.0),

				("F_SCORE_CALCULATION_ERROR", "Расчёт оценочной функции f(n)", 1.0),
				("F_FORMULA_CONSISTENCY_ERROR", "Расчёт оценочной функции f(n)", 1.0),

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