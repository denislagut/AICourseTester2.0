using AICourseTester.Data;
using AICourseTester.Models;
using AICourseTester.Services.Interfaces;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace AICourseTester.Services
{
	public class ReportExportService : IReportExportService
	{
		private readonly MainDbContext _context;

		public ReportExportService(MainDbContext context)
		{
			_context = context;
			QuestPDF.Settings.License = LicenseType.Community;
		}

		public async Task<byte[]?> ExportExcelAsync(int reportId)
		{
			var report = await LoadReportAsync(reportId);

			if (report == null)
			{
				return null;
			}

			var data = ParseReport(report);

			using var workbook = new XLWorkbook();
			AddSummarySheet(workbook, data);
			AddStatisticsSheet(workbook, data);
			AddKnowledgeDistributionSheet(workbook, data);
			AddProblemsSheet(workbook, data);
			AddErrorTypesSheet(workbook, data);
			AddRecommendationsSheet(workbook, data);
			AddTeacherActionsSheet(workbook, data);

			using var stream = new MemoryStream();
			workbook.SaveAs(stream);
			return stream.ToArray();
		}

		public async Task<byte[]?> ExportPdfAsync(int reportId)
		{
			var report = await LoadReportAsync(reportId);

			if (report == null)
			{
				return null;
			}

			var data = ParseReport(report);

			return Document.Create(container =>
			{
				container.Page(page =>
				{
					page.Margin(32);
					page.Size(PageSizes.A4);
					page.DefaultTextStyle(text => text.FontSize(10).FontFamily("Arial"));

					page.Header()
						.BorderBottom(1)
						.BorderColor(Colors.Grey.Lighten2)
						.PaddingBottom(10)
						.Column(column =>
						{
							column.Item().Text(data.Title).FontSize(20).Bold();
							column.Item().Text($"Дата формирования: {FormatDate(data.CreatedAt)}").FontColor(Colors.Grey.Darken2);
						});

					page.Content()
						.PaddingVertical(14)
						.Column(column =>
						{
							column.Spacing(12);

							AddPdfCard(column, "Краткий вывод", data.Conclusion);
							AddPdfRiskBlock(column, data.RiskLevel);
							AddPdfStatisticsBlock(column, data);
							AddPdfKnowledgeDistributionBlock(column, data);
							AddPdfProblemsBlock(column, data);
							AddPdfErrorTypesBlock(column, data);
							AddPdfRecommendationsBlock(column, data);
							AddPdfTeacherActionsBlock(column, data);
						});

					page.Footer()
						.AlignCenter()
						.Text(text =>
						{
							text.Span("Страница ");
							text.CurrentPageNumber();
							text.Span(" из ");
							text.TotalPages();
						});
				});
			}).GeneratePdf();
		}

		private async Task<GeneratedReport?> LoadReportAsync(int reportId)
		{
			return await _context.GeneratedReports
				.AsNoTracking()
				.FirstOrDefaultAsync(r => r.Id == reportId);
		}

		private static void AddSummarySheet(XLWorkbook workbook, ReportExportData data)
		{
			var sheet = workbook.Worksheets.Add("Сводка");
			AddKeyValueRows(sheet, new[]
			{
				("Тип отчета", TranslateReportType(data.ReportType)),
				("Дата", FormatDate(data.CreatedAt)),
				("Уровень риска", TranslateLevel(data.RiskLevel)),
				("Краткий вывод", data.Conclusion),
				("Количество рекомендаций", data.RecommendationsCount.ToString())
			});
		}

		private static void AddStatisticsSheet(XLWorkbook workbook, ReportExportData data)
		{
			var sheet = workbook.Worksheets.Add("Статистика");
			AddKeyValueRows(sheet, new[]
			{
				("Количество ошибок", data.TotalErrors.ToString()),
				("Количество пробелов знаний", data.TotalKnowledgeGaps.ToString()),
				("Средний уровень пробелов", data.AverageGapScore.ToString("0.##")),
				("Количество критических ошибок", data.HighSeverityErrorsCount.ToString())
			});
		}

		private static void AddKnowledgeDistributionSheet(XLWorkbook workbook, ReportExportData data)
		{
			var sheet = workbook.Worksheets.Add("Проблемность знаний");
			AddHeader(sheet, "Уровень", "Количество", "Процент", "Визуализация");

			var rows = data.KnowledgeDistribution;
			for (var i = 0; i < rows.Count; i++)
			{
				var row = i + 2;
				var item = rows[i];
				sheet.Cell(row, 1).Value = item.Label;
				sheet.Cell(row, 2).Value = item.Count;
				sheet.Cell(row, 3).Value = item.Percent / 100;
				sheet.Cell(row, 3).Style.NumberFormat.Format = "0%";
				sheet.Cell(row, 4).Value = item.Percent / 100;
				sheet.Cell(row, 4).Style.NumberFormat.Format = "0%";
				sheet.Cell(row, 4).Style.Fill.BackgroundColor = item.Color;
				sheet.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
			}

			if (rows.Count > 0)
			{
				var range = sheet.Range(2, 4, rows.Count + 1, 4);
				range.AddConditionalFormat().DataBar(XLColor.FromHtml("#CED4DA"), false);
			}

			FormatTable(sheet);
		}

		private static void AddProblemsSheet(XLWorkbook workbook, ReportExportData data)
		{
			var sheet = workbook.Worksheets.Add("Проблемные темы");
			AddHeader(sheet, "Аспект", "Тема", "Показатель пробела", "Уровень");

			var row = 2;
			foreach (var problem in data.KnowledgeGaps)
			{
				sheet.Cell(row, 1).Value = problem.AspectName;
				sheet.Cell(row, 2).Value = problem.TopicName;
				sheet.Cell(row, 3).Value = problem.Score;
				sheet.Cell(row, 4).Value = TranslateLevel(problem.Level);
				row++;
			}

			FormatTable(sheet);
		}

		private static void AddErrorTypesSheet(XLWorkbook workbook, ReportExportData data)
		{
			var sheet = workbook.Worksheets.Add("Типы ошибок");
			AddHeader(sheet, "Название ошибки", "Количество", "Средняя серьезность");

			var row = 2;
			foreach (var error in data.ErrorTypes)
			{
				sheet.Cell(row, 1).Value = error.Name;
				sheet.Cell(row, 2).Value = error.Count;
				sheet.Cell(row, 3).Value = error.AverageSeverity;
				row++;
			}

			FormatTable(sheet);
		}

		private static void AddRecommendationsSheet(XLWorkbook workbook, ReportExportData data)
		{
			var sheet = workbook.Worksheets.Add("Рекомендации");
			AddHeader(sheet, "Приоритет", "Название", "Описание", "Тема", "GapScore", "Количество связанных ошибок");

			var row = 2;
			foreach (var recommendation in data.Recommendations)
			{
				sheet.Cell(row, 1).Value = TranslateLevel(recommendation.Priority);
				sheet.Cell(row, 2).Value = recommendation.Title;
				sheet.Cell(row, 3).Value = recommendation.Description;
				sheet.Cell(row, 4).Value = recommendation.TopicName;
				sheet.Cell(row, 5).Value = recommendation.GapScore;
				sheet.Cell(row, 6).Value = recommendation.RelatedErrorCount;
				row++;
			}

			FormatTable(sheet);
		}

		private static void AddTeacherActionsSheet(XLWorkbook workbook, ReportExportData data)
		{
			var sheet = workbook.Worksheets.Add("Действия преподавателя");
			AddHeader(sheet, "Действие");

			var actions = data.TeacherActions.Count > 0
				? data.TeacherActions
				: new List<string> { "Дополнительные действия не требуются." };

			for (var i = 0; i < actions.Count; i++)
			{
				sheet.Cell(i + 2, 1).Value = actions[i];
			}

			FormatTable(sheet);
		}

		private static void AddKeyValueRows(IXLWorksheet sheet, IEnumerable<(string Key, string Value)> rows)
		{
			var row = 1;
			foreach (var (key, value) in rows)
			{
				sheet.Cell(row, 1).Value = key;
				sheet.Cell(row, 2).Value = value;
				row++;
			}

			sheet.Column(1).Style.Font.Bold = true;
			FormatTable(sheet);
		}

		private static void AddHeader(IXLWorksheet sheet, params string[] headers)
		{
			for (var i = 0; i < headers.Length; i++)
			{
				sheet.Cell(1, i + 1).Value = headers[i];
			}

			sheet.Row(1).Style.Font.Bold = true;
			sheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightGray;
		}

		private static void FormatTable(IXLWorksheet sheet)
		{
			sheet.Columns().AdjustToContents();
			sheet.Style.Alignment.WrapText = true;
		}

		private static void AddPdfCard(ColumnDescriptor column, string title, string text)
		{
			column.Item()
				.Border(1)
				.BorderColor(Colors.Grey.Lighten2)
				.Background(Colors.Grey.Lighten5)
				.Padding(10)
				.Column(inner =>
				{
					inner.Spacing(4);
					inner.Item().Text(title).FontSize(13).Bold();
					inner.Item().Text(string.IsNullOrWhiteSpace(text) ? "Нет данных." : text);
				});
		}

		private static void AddPdfRiskBlock(ColumnDescriptor column, string riskLevel)
		{
			column.Item()
				.Border(1)
				.BorderColor(Colors.Grey.Lighten2)
				.Padding(10)
				.Column(inner =>
				{
					inner.Spacing(6);
					inner.Item().Text("Уровень риска").FontSize(13).Bold();
					inner.Item().Text(TranslateLevel(riskLevel)).Bold().FontColor(GetPdfColor(riskLevel));
					AddPdfProgressBar(inner, GetRiskPercent(riskLevel), GetPdfColor(riskLevel));
				});
		}

		private static void AddPdfStatisticsBlock(ColumnDescriptor column, ReportExportData data)
		{
			AddPdfSection(column, "Общая статистика", c =>
			{
				c.Item().Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					AddMetricCell(table, "Количество ошибок", data.TotalErrors.ToString());
					AddMetricCell(table, "Количество пробелов знаний", data.TotalKnowledgeGaps.ToString());
					AddMetricCell(table, "Средний уровень пробелов", $"{data.AverageGapScore:0.##}%");
					AddMetricCell(table, "Критические ошибки", data.HighSeverityErrorsCount.ToString());
				});
			});
		}

		private static void AddPdfKnowledgeDistributionBlock(ColumnDescriptor column, ReportExportData data)
		{
			AddPdfSection(column, "Распределение уровня проблемности знаний", c =>
			{
				if (data.KnowledgeDistribution.All(item => item.Count == 0))
				{
					c.Item().Text("Недостаточно данных для построения диаграммы.");
					return;
				}

				foreach (var item in data.KnowledgeDistribution)
				{
					c.Item().Text($"{item.Label}: {item.Percent:0.#}% ({item.Count})").Bold();
					AddPdfProgressBar(c, item.Percent, item.PdfColor);
				}
			});
		}

		private static void AddPdfProblemsBlock(ColumnDescriptor column, ReportExportData data)
		{
			AddPdfSection(column, "Проблемные темы", c =>
			{
				if (data.KnowledgeGaps.Count == 0)
				{
					c.Item().Text("Проблемные темы не найдены.");
					return;
				}

				foreach (var problem in data.KnowledgeGaps)
				{
					c.Item().Text($"{problem.AspectName} | тема: {problem.TopicName}").Bold();
					c.Item().Text($"Показатель пробела: {problem.Score:0.##}% | уровень: {TranslateLevel(problem.Level)}").FontColor(Colors.Grey.Darken2);
					AddPdfProgressBar(c, problem.Score, GetPdfColor(problem.Level));
				}
			});
		}

		private static void AddPdfErrorTypesBlock(ColumnDescriptor column, ReportExportData data)
		{
			AddPdfSection(column, "Основные типы ошибок", c =>
			{
				if (data.ErrorTypes.Count == 0)
				{
					c.Item().Text("Основные типы ошибок не найдены.");
					return;
				}

				var maxCount = Math.Max(data.ErrorTypes.Max(item => item.Count), 1);
				foreach (var error in data.ErrorTypes)
				{
					var percent = error.Count * 100d / maxCount;
					c.Item().Text($"{error.Name}: {error.Count} ошибок, средняя серьезность {error.AverageSeverity:0.##}").Bold();
					AddPdfProgressBar(c, percent, Colors.Blue.Medium);
				}
			});
		}

		private static void AddPdfRecommendationsBlock(ColumnDescriptor column, ReportExportData data)
		{
			AddPdfSection(column, "Рекомендации преподавателю", c =>
			{
				if (data.Recommendations.Count == 0)
				{
					c.Item().Text("Рекомендации не найдены.");
					return;
				}

				foreach (var recommendation in data.Recommendations)
				{
					c.Item()
						.Border(1)
						.BorderColor(Colors.Grey.Lighten2)
						.Padding(8)
						.Column(card =>
						{
							card.Spacing(3);
							card.Item().Text($"{TranslateLevel(recommendation.Priority)}: {recommendation.Title}").Bold().FontColor(GetPdfColor(recommendation.Priority));
							card.Item().Text(recommendation.Description);
							card.Item().Text($"Тема: {recommendation.TopicName}; gapScore: {recommendation.GapScore:0.##}; связанные ошибки: {recommendation.RelatedErrorCount}")
								.FontColor(Colors.Grey.Darken2);
						});
				}
			});
		}

		private static void AddPdfTeacherActionsBlock(ColumnDescriptor column, ReportExportData data)
		{
			AddPdfSection(column, "Рекомендуемые действия преподавателя", c =>
			{
				var actions = data.TeacherActions.Count > 0
					? data.TeacherActions
					: new List<string> { "Дополнительные действия не требуются." };

				foreach (var action in actions)
				{
					c.Item().Text($"- {action}");
				}
			});
		}

		private static void AddPdfSection(ColumnDescriptor column, string title, Action<ColumnDescriptor> content)
		{
			column.Item()
				.PaddingTop(4)
				.Text(title)
				.FontSize(14)
				.Bold();

			column.Item().Column(inner =>
			{
				inner.Spacing(6);
				content(inner);
			});
		}

		private static void AddMetricCell(TableDescriptor table, string label, string value)
		{
			table.Cell()
				.Border(1)
				.BorderColor(Colors.Grey.Lighten2)
				.Background(Colors.Grey.Lighten5)
				.Padding(8)
				.Column(column =>
				{
					column.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken2);
					column.Item().Text(value).FontSize(14).Bold();
				});
		}

		private static void AddPdfProgressBar(ColumnDescriptor column, double percent, string color)
		{
			var safePercent = Math.Clamp(percent, 0, 100);
			column.Item()
				.Height(9)
				.Row(row =>
				{
					row.RelativeItem((float)Math.Max(safePercent, 1)).Background(color);
					row.RelativeItem((float)Math.Max(100 - safePercent, 1)).Background(Colors.Grey.Lighten3);
				});
		}

		private static ReportExportData ParseReport(GeneratedReport report)
		{
			using var summary = ParseDocument(report.SummaryJson);
			using var analytics = ParseDocument(report.AnalyticsJson);
			using var recommendations = ParseDocument(report.RecommendationsJson);

			var summaryRoot = summary.RootElement;
			var analyticsRoot = analytics.RootElement;
			var knowledgeGaps = GetKnowledgeGaps(analyticsRoot);
			var mainProblems = GetProblems(summaryRoot);
			if (knowledgeGaps.Count == 0 && mainProblems.Count > 0)
			{
				knowledgeGaps = mainProblems;
			}

			return new ReportExportData
			{
				ReportType = GetString(summaryRoot, "reportType", "ReportType", report.ReportType),
				Title = GetString(summaryRoot, "title", "Title", report.Title),
				CreatedAt = GetDate(summaryRoot, "createdAt", "CreatedAt", report.CreatedAt),
				RiskLevel = GetString(summaryRoot, "riskLevel", "RiskLevel", "Low"),
				Conclusion = GetString(summaryRoot, "conclusion", "Conclusion", ""),
				RecommendationsCount = GetInt(summaryRoot, "recommendationsCount", "RecommendationsCount", 0),
				TeacherActions = GetStringArray(summaryRoot, "teacherActions", "TeacherActions"),
				KnowledgeGaps = knowledgeGaps,
				TotalErrors = GetInt(analyticsRoot, "TotalErrors", "totalErrors", 0),
				TotalKnowledgeGaps = GetInt(analyticsRoot, "TotalKnowledgeGaps", "totalKnowledgeGaps", 0),
				AverageGapScore = GetDouble(analyticsRoot, "AverageGapScore", "averageGapScore", 0),
				HighSeverityErrorsCount = GetInt(analyticsRoot, "HighSeverityErrorsCount", "highSeverityErrorsCount", 0),
				ErrorTypes = GetErrorTypes(analyticsRoot),
				Recommendations = GetRecommendations(recommendations.RootElement)
			};
		}

		private static JsonDocument ParseDocument(string json)
		{
			return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
		}

		private static List<ProblemExportItem> GetProblems(JsonElement summary)
		{
			if (!TryGetProperty(summary, out var problems, "mainProblems", "MainProblems") || problems.ValueKind != JsonValueKind.Array)
			{
				return new List<ProblemExportItem>();
			}

			return problems.EnumerateArray()
				.Select(item => new ProblemExportItem
				{
					AspectName = GetString(item, "aspectName", "AspectName", "Аспект не указан"),
					TopicName = GetString(item, "topicName", "TopicName", "не указана"),
					Score = GetDouble(item, "score", "Score", 0),
					Level = GetString(item, "level", "Level", GetLevel(GetDouble(item, "score", "Score", 0)))
				})
				.ToList();
		}

		private static List<ProblemExportItem> GetKnowledgeGaps(JsonElement analytics)
		{
			var json = GetString(analytics, "TopKnowledgeGapsJson", "topKnowledgeGapsJson", "[]");

			using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
			if (document.RootElement.ValueKind != JsonValueKind.Array)
			{
				return new List<ProblemExportItem>();
			}

			return document.RootElement.EnumerateArray()
				.Select(item =>
				{
					var score = GetDouble(item, "AverageGapScore", "averageGapScore", GetDouble(item, "MaxGapScore", "maxGapScore", 0));

					return new ProblemExportItem
					{
						AspectName = GetString(item, "AspectName", "aspectName", "Аспект не указан"),
						TopicName = GetString(item, "TopicName", "topicName", "не указана"),
						Score = score,
						Level = GetLevel(score)
					};
				})
				.ToList();
		}

		private static List<ErrorTypeExportItem> GetErrorTypes(JsonElement analytics)
		{
			var json = GetString(analytics, "TopErrorTypesJson", "topErrorTypesJson", "[]");

			using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
			if (document.RootElement.ValueKind != JsonValueKind.Array)
			{
				return new List<ErrorTypeExportItem>();
			}

			return document.RootElement.EnumerateArray()
				.Select(item => new ErrorTypeExportItem
				{
					Name = GetString(item, "Name", "name", GetString(item, "Code", "code", "Ошибка")),
					Count = GetInt(item, "Count", "count", 0),
					AverageSeverity = GetDouble(item, "AverageSeverity", "averageSeverity", 0)
				})
				.ToList();
		}

		private static List<RecommendationExportItem> GetRecommendations(JsonElement root)
		{
			if (root.ValueKind != JsonValueKind.Array)
			{
				return new List<RecommendationExportItem>();
			}

			return root.EnumerateArray()
				.Select(item => new RecommendationExportItem
				{
					Priority = GetString(item, "Priority", "priority", "Low"),
					Title = GetString(item, "Title", "title", "Рекомендация"),
					Description = GetString(item, "Description", "description", ""),
					TopicName = GetString(item, "TopicName", "topicName", "не указана"),
					GapScore = GetDouble(item, "GapScore", "gapScore", 0),
					RelatedErrorCount = GetInt(item, "RelatedErrorCount", "relatedErrorCount", 0)
				})
				.ToList();
		}

		private static List<string> GetStringArray(JsonElement element, params string[] names)
		{
			if (!TryGetProperty(element, out var array, names) || array.ValueKind != JsonValueKind.Array)
			{
				return new List<string>();
			}

			return array.EnumerateArray()
				.Where(item => item.ValueKind == JsonValueKind.String)
				.Select(item => item.GetString() ?? "")
				.Where(value => !string.IsNullOrWhiteSpace(value))
				.ToList();
		}

		private static string GetString(JsonElement element, string name1, string name2, string defaultValue)
		{
			if (!TryGetProperty(element, out var property, name1, name2))
			{
				return defaultValue;
			}

			return property.ValueKind switch
			{
				JsonValueKind.String => property.GetString() ?? defaultValue,
				JsonValueKind.Number => property.ToString(),
				_ => defaultValue
			};
		}

		private static int GetInt(JsonElement element, string name1, string name2, int defaultValue)
		{
			return TryGetProperty(element, out var property, name1, name2) && property.TryGetInt32(out var value)
				? value
				: defaultValue;
		}

		private static double GetDouble(JsonElement element, string name1, string name2, double defaultValue)
		{
			return TryGetProperty(element, out var property, name1, name2) && property.TryGetDouble(out var value)
				? value
				: defaultValue;
		}

		private static DateTime GetDate(JsonElement element, string name1, string name2, DateTime defaultValue)
		{
			return TryGetProperty(element, out var property, name1, name2) && property.TryGetDateTime(out var value)
				? value
				: defaultValue;
		}

		private static bool TryGetProperty(JsonElement element, out JsonElement property, params string[] names)
		{
			foreach (var name in names)
			{
				if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out property))
				{
					return true;
				}
			}

			property = default;
			return false;
		}

		private static string TranslateReportType(string reportType)
		{
			return reportType == "Group" ? "Группа" : "Студент";
		}

		private static string TranslateLevel(string? level)
		{
			return level switch
			{
				"High" => "Высокий",
				"Medium" => "Средний",
				"Low" => "Низкий",
				_ => level ?? "-"
			};
		}

		private static string GetLevel(double score)
		{
			if (score > 75)
			{
				return "High";
			}

			if (score >= 50)
			{
				return "Medium";
			}

			return "Low";
		}

		private static double GetRiskPercent(string? level)
		{
			return level switch
			{
				"High" => 100,
				"Medium" => 65,
				"Low" => 35,
				_ => 0
			};
		}

		private static string GetPdfColor(string? level)
		{
			return level switch
			{
				"High" => Colors.Red.Medium,
				"Medium" => Colors.Orange.Medium,
				"Low" => Colors.Green.Medium,
				_ => Colors.Grey.Medium
			};
		}

		private static string FormatDate(DateTime date)
		{
			return date.ToString("dd.MM.yyyy HH:mm");
		}

		private class ReportExportData
		{
			public string ReportType { get; set; } = "";
			public string Title { get; set; } = "";
			public DateTime CreatedAt { get; set; }
			public string RiskLevel { get; set; } = "";
			public string Conclusion { get; set; } = "";
			public int RecommendationsCount { get; set; }
			public int TotalErrors { get; set; }
			public int TotalKnowledgeGaps { get; set; }
			public double AverageGapScore { get; set; }
			public int HighSeverityErrorsCount { get; set; }
			public List<string> TeacherActions { get; set; } = new();
			public List<ProblemExportItem> KnowledgeGaps { get; set; } = new();
			public List<ErrorTypeExportItem> ErrorTypes { get; set; } = new();
			public List<RecommendationExportItem> Recommendations { get; set; } = new();
			public List<KnowledgeDistributionItem> KnowledgeDistribution
			{
				get
				{
					var total = KnowledgeGaps.Count;
					var critical = KnowledgeGaps.Count(item => item.Score > 75);
					var medium = KnowledgeGaps.Count(item => item.Score >= 50 && item.Score <= 75);
					var low = KnowledgeGaps.Count(item => item.Score < 50);

					return new List<KnowledgeDistributionItem>
					{
						new("Критические", critical, total, XLColor.FromHtml("#D92D20"), Colors.Red.Medium),
						new("Средние", medium, total, XLColor.FromHtml("#F79009"), Colors.Orange.Medium),
						new("Низкие", low, total, XLColor.FromHtml("#12B76A"), Colors.Green.Medium)
					};
				}
			}
		}

		private class ProblemExportItem
		{
			public string AspectName { get; set; } = "";
			public string TopicName { get; set; } = "";
			public double Score { get; set; }
			public string Level { get; set; } = "";
		}

		private class KnowledgeDistributionItem
		{
			public KnowledgeDistributionItem(string label, int count, int total, XLColor color, string pdfColor)
			{
				Label = label;
				Count = count;
				Percent = total == 0 ? 0 : count * 100d / total;
				Color = color;
				PdfColor = pdfColor;
			}

			public string Label { get; }
			public int Count { get; }
			public double Percent { get; }
			public XLColor Color { get; }
			public string PdfColor { get; }
		}

		private class ErrorTypeExportItem
		{
			public string Name { get; set; } = "";
			public int Count { get; set; }
			public double AverageSeverity { get; set; }
		}

		private class RecommendationExportItem
		{
			public string Priority { get; set; } = "";
			public string Title { get; set; } = "";
			public string Description { get; set; } = "";
			public string TopicName { get; set; } = "";
			public double GapScore { get; set; }
			public int RelatedErrorCount { get; set; }
		}
	}
}
