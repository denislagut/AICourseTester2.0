using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AICourseTester.Controllers
{
	[Authorize(Roles = "Administrator")]
	[Route("api/[controller]")]
	[ApiController]
	public class KnowledgeManagementController : ControllerBase
	{
		private const double MaxLinkWeight = 1.0;
		private readonly MainDbContext _context;

		public KnowledgeManagementController(MainDbContext context)
		{
			_context = context;
		}

		[HttpGet("ErrorTypes")]
		public async Task<ActionResult<List<ErrorTypeViewDTO>>> GetErrorTypes()
		{
			return await _context.ErrorTypes
				.AsNoTracking()
				.OrderBy(errorType => errorType.Name)
				.Select(errorType => new ErrorTypeViewDTO
				{
					Id = errorType.Id,
					Code = errorType.Code,
					Name = errorType.Name,
					Description = errorType.Description,
					DefaultSeverity = errorType.DefaultSeverity
				})
				.ToListAsync();
		}

		[HttpGet("Aspects")]
		public async Task<ActionResult<List<KnowledgeAspectViewDTO>>> GetAspects()
		{
			return await _context.KnowledgeAspects
				.AsNoTracking()
				.Include(aspect => aspect.Topic)
				.OrderBy(aspect => aspect.Topic == null ? null : aspect.Topic.Name)
				.ThenBy(aspect => aspect.Name)
				.Select(aspect => new KnowledgeAspectViewDTO
				{
					Id = aspect.Id,
					Name = aspect.Name,
					Description = aspect.Description,
					TopicName = aspect.Topic == null ? null : aspect.Topic.Name,
					IsActive = aspect.IsActive
				})
				.ToListAsync();
		}

		[HttpGet("ErrorTypeAspects")]
		public async Task<ActionResult<List<ErrorTypeAspectViewDTO>>> GetErrorTypeAspects()
		{
			return await BuildLinkQuery()
				.OrderBy(link => link.ErrorTypeName)
				.ThenBy(link => link.KnowledgeAspectName)
				.ToListAsync();
		}

		[HttpPost("Aspects")]
		public async Task<ActionResult<KnowledgeAspectViewDTO>> CreateAspect(KnowledgeAspectEditDTO dto)
		{
			var validationError = ValidateAspect(dto);
			if (validationError != null)
			{
				return BadRequest(validationError);
			}

			var aspect = new KnowledgeAspect
			{
				Name = dto.Name.Trim(),
				Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
				Topic = await GetOrCreateTopicAsync(dto.TopicName),
				IsActive = dto.IsActive
			};

			_context.KnowledgeAspects.Add(aspect);
			await _context.SaveChangesAsync();

			return Ok(MapAspect(aspect));
		}

		[HttpPut("Aspects/{aspectId:int}")]
		public async Task<ActionResult<KnowledgeAspectViewDTO>> UpdateAspect(int aspectId, KnowledgeAspectEditDTO dto)
		{
			var validationError = ValidateAspect(dto);
			if (validationError != null)
			{
				return BadRequest(validationError);
			}

			var aspect = await _context.KnowledgeAspects
				.Include(item => item.Topic)
				.FirstOrDefaultAsync(item => item.Id == aspectId);

			if (aspect == null)
			{
				return NotFound("Аспект знаний не найден");
			}

			aspect.Name = dto.Name.Trim();
			aspect.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
			aspect.Topic = await GetOrCreateTopicAsync(dto.TopicName);
			aspect.IsActive = dto.IsActive;

			await _context.SaveChangesAsync();

			return Ok(MapAspect(aspect));
		}

		[HttpPost("ErrorTypeAspects")]
		public async Task<ActionResult<ErrorTypeAspectViewDTO>> CreateOrUpdateErrorTypeAspect(ErrorTypeAspectEditDTO dto)
		{
			var validationError = await ValidateLinkAsync(dto.ErrorTypeId, dto.KnowledgeAspectId, dto.Weight);
			if (validationError != null)
			{
				return BadRequest(validationError);
			}

			var existingLink = await _context.ErrorTypeAspects
				.FirstOrDefaultAsync(link =>
					link.ErrorTypeId == dto.ErrorTypeId &&
					link.KnowledgeAspectId == dto.KnowledgeAspectId);

			if (existingLink != null)
			{
				existingLink.Weight = dto.Weight;
				await _context.SaveChangesAsync();

				return Ok(await GetLinkByIdAsync(existingLink.Id));
			}

			var link = new ErrorTypeAspect
			{
				ErrorTypeId = dto.ErrorTypeId,
				KnowledgeAspectId = dto.KnowledgeAspectId,
				Weight = dto.Weight
			};

			_context.ErrorTypeAspects.Add(link);
			await _context.SaveChangesAsync();

			return Ok(await GetLinkByIdAsync(link.Id));
		}

		[HttpPut("ErrorTypeAspects/{linkId:int}")]
		public async Task<ActionResult<ErrorTypeAspectViewDTO>> UpdateErrorTypeAspectWeight(int linkId, ErrorTypeAspectUpdateDTO dto)
		{
			if (dto.Weight <= 0 || dto.Weight > MaxLinkWeight)
			{
				return BadRequest("Вес связи должен быть больше 0 и не больше 1");
			}

			var link = await _context.ErrorTypeAspects
				.FirstOrDefaultAsync(item => item.Id == linkId);

			if (link == null)
			{
				return NotFound("Связь типа ошибки и аспекта знаний не найдена");
			}

			link.Weight = dto.Weight;
			await _context.SaveChangesAsync();

			return Ok(await GetLinkByIdAsync(link.Id));
		}

		[HttpDelete("ErrorTypeAspects/{linkId:int}")]
		public async Task<ActionResult> DeleteErrorTypeAspect(int linkId)
		{
			var link = await _context.ErrorTypeAspects
				.FirstOrDefaultAsync(item => item.Id == linkId);

			if (link == null)
			{
				return NotFound("Связь типа ошибки и аспекта знаний не найдена");
			}

			_context.ErrorTypeAspects.Remove(link);
			await _context.SaveChangesAsync();

			return Ok();
		}

		[HttpGet("CausalRules")]
		public async Task<ActionResult<List<CausalErrorRuleViewDTO>>> GetRules()
		{
			return await BuildCausalRuleQuery()
				.ToListAsync();
		}

		[HttpPost("CausalRules")]
		public async Task<ActionResult<CausalErrorRuleViewDTO>> CreateOrUpdateRule(CausalErrorRuleEditDTO dto)
		{
			var validationError = await ValidateCausalRuleAsync(dto);
			if (validationError != null)
			{
				return BadRequest(validationError);
			}

			var existingRule = await _context.CausalErrorRules
				.FirstOrDefaultAsync(rule =>
					rule.TaskTypeId == dto.TaskTypeId &&
					rule.SourceErrorTypeId == dto.SourceErrorTypeId &&
					rule.TargetErrorTypeId == dto.TargetErrorTypeId &&
					rule.RelationTypeId == dto.RelationTypeId &&
					rule.SameNodeRequired == dto.SameNodeRequired &&
					rule.SameRootBranchRequired == dto.SameRootBranchRequired);

			if (existingRule != null)
			{
				existingRule.Weight = dto.Weight;
				existingRule.IsActive = dto.IsActive;
				await _context.SaveChangesAsync();

				return Ok(await GetCausalRuleByIdAsync(existingRule.Id));
			}

			var rule = new CausalErrorRule
			{
				TaskTypeId = dto.TaskTypeId,
				SourceErrorTypeId = dto.SourceErrorTypeId,
				TargetErrorTypeId = dto.TargetErrorTypeId,
				RelationTypeId = dto.RelationTypeId,
				Weight = dto.Weight,
				SameNodeRequired = dto.SameNodeRequired,
				SameRootBranchRequired = dto.SameRootBranchRequired,
				IsActive = dto.IsActive
			};

			_context.CausalErrorRules.Add(rule);
			await _context.SaveChangesAsync();

			return Ok(await GetCausalRuleByIdAsync(rule.Id));
		}

		[HttpPut("CausalRules/{id}")]
		public async Task<ActionResult<CausalErrorRuleViewDTO>> UpdateRule(int id, CausalErrorRuleEditDTO dto)
		{
			var validationError = await ValidateCausalRuleAsync(dto);
			if (validationError != null)
			{
				return BadRequest(validationError);
			}

			var rule = await _context.CausalErrorRules.FindAsync(id);

			if (rule == null)
			{
				return NotFound("Правило причинно-следственной связи не найдено");
			}

			var duplicateExists = await _context.CausalErrorRules
				.AsNoTracking()
				.AnyAsync(item =>
					item.Id != id &&
					item.TaskTypeId == dto.TaskTypeId &&
					item.SourceErrorTypeId == dto.SourceErrorTypeId &&
					item.TargetErrorTypeId == dto.TargetErrorTypeId &&
					item.RelationTypeId == dto.RelationTypeId &&
					item.SameNodeRequired == dto.SameNodeRequired &&
					item.SameRootBranchRequired == dto.SameRootBranchRequired);

			if (duplicateExists)
			{
				return BadRequest("Такое правило уже существует. Измените существующее правило или выберите другие параметры.");
			}

			rule.TaskTypeId = dto.TaskTypeId;
			rule.SourceErrorTypeId = dto.SourceErrorTypeId;
			rule.TargetErrorTypeId = dto.TargetErrorTypeId;
			rule.RelationTypeId = dto.RelationTypeId;
			rule.Weight = dto.Weight;
			rule.SameNodeRequired = dto.SameNodeRequired;
			rule.SameRootBranchRequired = dto.SameRootBranchRequired;
			rule.IsActive = dto.IsActive;

			await _context.SaveChangesAsync();

			return Ok(await GetCausalRuleByIdAsync(rule.Id));
		}

		[HttpDelete("CausalRules/{id}")]
		public async Task<ActionResult> DeleteRule(int id)
		{
			var rule = await _context.CausalErrorRules
				.FirstOrDefaultAsync(item => item.Id == id);

			if (rule == null)
			{
				return NotFound("Правило причинно-следственной связи не найдено");
			}

			_context.CausalErrorRules.Remove(rule);
			await _context.SaveChangesAsync();

			return Ok();
		}

		private IQueryable<ErrorTypeAspectViewDTO> BuildLinkQuery()
		{
			return _context.ErrorTypeAspects
				.AsNoTracking()
				.Include(link => link.ErrorType)
				.Include(link => link.KnowledgeAspect)
					.ThenInclude(aspect => aspect.Topic)
				.Select(link => new ErrorTypeAspectViewDTO
				{
					Id = link.Id,
					ErrorTypeId = link.ErrorTypeId,
					ErrorTypeName = link.ErrorType.Name,
					ErrorTypeCode = link.ErrorType.Code,
					KnowledgeAspectId = link.KnowledgeAspectId,
					KnowledgeAspectName = link.KnowledgeAspect.Name,
					TopicName = link.KnowledgeAspect.Topic == null ? null : link.KnowledgeAspect.Topic.Name,
					Weight = link.Weight
				});
		}

		private async Task<ErrorTypeAspectViewDTO> GetLinkByIdAsync(int linkId)
		{
			return await BuildLinkQuery()
				.FirstAsync(link => link.Id == linkId);
		}

		private IQueryable<CausalErrorRuleViewDTO> BuildCausalRuleQuery()
		{
			return _context.CausalErrorRules
				.AsNoTracking()
				.Include(rule => rule.TaskTypeRef)
				.Include(rule => rule.SourceErrorType)
				.Include(rule => rule.TargetErrorType)
				.Include(rule => rule.RelationTypeRef)
				.OrderBy(rule => rule.TaskTypeRef.Code)
				.ThenBy(rule => rule.SourceErrorType.Code)
				.ThenBy(rule => rule.TargetErrorType.Code)
				.Select(rule => new CausalErrorRuleViewDTO
				{
					Id = rule.Id,
					TaskTypeId = rule.TaskTypeId,
					TaskTypeCode = rule.TaskTypeRef.Code,
					TaskTypeName = rule.TaskTypeRef.Name,
					SourceErrorTypeId = rule.SourceErrorTypeId,
					SourceErrorTypeCode = rule.SourceErrorType.Code,
					SourceErrorTypeName = rule.SourceErrorType.Name,
					TargetErrorTypeId = rule.TargetErrorTypeId,
					TargetErrorTypeCode = rule.TargetErrorType.Code,
					TargetErrorTypeName = rule.TargetErrorType.Name,
					RelationTypeId = rule.RelationTypeId,
					RelationTypeCode = rule.RelationTypeRef.Code,
					RelationTypeName = rule.RelationTypeRef.Name,
					Weight = rule.Weight,
					SameNodeRequired = rule.SameNodeRequired,
					SameRootBranchRequired = rule.SameRootBranchRequired,
					IsActive = rule.IsActive
				});
		}

		private async Task<CausalErrorRuleViewDTO> GetCausalRuleByIdAsync(int id)
		{
			return await BuildCausalRuleQuery()
				.FirstAsync(rule => rule.Id == id);
		}

		private static KnowledgeAspectViewDTO MapAspect(KnowledgeAspect aspect)
		{
			return new KnowledgeAspectViewDTO
			{
				Id = aspect.Id,
				Name = aspect.Name,
				Description = aspect.Description,
				TopicName = aspect.Topic == null ? null : aspect.Topic.Name,
				IsActive = aspect.IsActive
			};
		}

		private async Task<KnowledgeTopic?> GetOrCreateTopicAsync(string? topicName)
		{
			if (string.IsNullOrWhiteSpace(topicName))
			{
				return null;
			}

			var normalizedName = topicName.Trim();
			var topic = await _context.KnowledgeTopics
				.FirstOrDefaultAsync(item => item.Name == normalizedName);

			if (topic != null)
			{
				return topic;
			}

			topic = new KnowledgeTopic { Name = normalizedName };
			_context.KnowledgeTopics.Add(topic);
			return topic;
		}

		private async Task ResolveRuleReferencesAsync(CausalErrorRule rule)
		{
			var sourceCode = rule.SourceErrorCode;
			var targetCode = rule.TargetErrorCode;

			rule.TaskTypeId = LookupIds.TaskTypeId(rule.TaskType);
			rule.RelationTypeId = LookupIds.CausalRelationTypeId(rule.RelationType);
			rule.SourceErrorType = await GetOrCreateErrorTypeAsync(sourceCode);
			rule.TargetErrorType = await GetOrCreateErrorTypeAsync(targetCode);
			rule.SourceErrorTypeId = rule.SourceErrorType.Id;
			rule.TargetErrorTypeId = rule.TargetErrorType.Id;
		}

		private async Task<ErrorType> GetOrCreateErrorTypeAsync(string code)
		{
			if (string.IsNullOrWhiteSpace(code))
			{
				code = "UNCLASSIFIED_ERROR";
			}

			var normalizedCode = code.Trim();
			var errorType = await _context.ErrorTypes
				.FirstOrDefaultAsync(type => type.Code == normalizedCode);

			if (errorType != null)
			{
				return errorType;
			}

			errorType = new ErrorType
			{
				Code = normalizedCode,
				Name = normalizedCode,
				DefaultSeverity = 1.0
			};
			_context.ErrorTypes.Add(errorType);
			await _context.SaveChangesAsync();

			return errorType;
		}

		private static string? ValidateAspect(KnowledgeAspectEditDTO dto)
		{
			if (dto == null)
			{
				return "Данные аспекта знаний не переданы";
			}

			return string.IsNullOrWhiteSpace(dto.Name)
				? "Название аспекта знаний не должно быть пустым"
				: null;
		}

		private async Task<string?> ValidateLinkAsync(int errorTypeId, int knowledgeAspectId, double weight)
		{
			if (weight <= 0 || weight > MaxLinkWeight)
			{
				return "Вес связи должен быть больше 0 и не больше 1";
			}

			var errorTypeExists = await _context.ErrorTypes
				.AsNoTracking()
				.AnyAsync(errorType => errorType.Id == errorTypeId);

			if (!errorTypeExists)
			{
				return "Тип ошибки не найден";
			}

			var aspectExists = await _context.KnowledgeAspects
				.AsNoTracking()
				.AnyAsync(aspect => aspect.Id == knowledgeAspectId);

			return aspectExists ? null : "Аспект знаний не найден";
		}

		private async Task<string?> ValidateCausalRuleAsync(CausalErrorRuleEditDTO dto)
		{
			if (dto == null)
			{
				return "Данные правила не переданы";
			}

			if (dto.Weight <= 0 || dto.Weight > MaxLinkWeight)
			{
				return "Вес правила должен быть больше 0 и не больше 1";
			}

			if (dto.SourceErrorTypeId == dto.TargetErrorTypeId)
			{
				return "Исходный и целевой тип ошибки должны отличаться";
			}

			var taskTypeExists = await _context.TaskTypes
				.AsNoTracking()
				.AnyAsync(taskType => taskType.Id == dto.TaskTypeId);

			if (!taskTypeExists)
			{
				return "Тип задания не найден";
			}

			var relationTypeExists = await _context.CausalRelationTypes
				.AsNoTracking()
				.AnyAsync(relationType => relationType.Id == dto.RelationTypeId);

			if (!relationTypeExists)
			{
				return "Тип причинной связи не найден";
			}

			var requestedErrorTypeIds = new[] { dto.SourceErrorTypeId, dto.TargetErrorTypeId }
				.Distinct()
				.ToList();
			var existingErrorTypeIds = await _context.ErrorTypes
				.AsNoTracking()
				.Where(errorType => requestedErrorTypeIds.Contains(errorType.Id))
				.Select(errorType => errorType.Id)
				.ToListAsync();

			return existingErrorTypeIds.Count == requestedErrorTypeIds.Count
				? null
				: "Один из типов ошибок не найден";
		}
	}
}
