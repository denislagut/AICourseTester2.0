using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Models;
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
				.OrderBy(aspect => aspect.TopicName)
				.ThenBy(aspect => aspect.Name)
				.Select(aspect => new KnowledgeAspectViewDTO
				{
					Id = aspect.Id,
					Name = aspect.Name,
					Description = aspect.Description,
					TopicName = aspect.TopicName,
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
				TopicName = string.IsNullOrWhiteSpace(dto.TopicName) ? null : dto.TopicName.Trim(),
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
				.FirstOrDefaultAsync(item => item.Id == aspectId);

			if (aspect == null)
			{
				return NotFound("Аспект знаний не найден");
			}

			aspect.Name = dto.Name.Trim();
			aspect.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
			aspect.TopicName = string.IsNullOrWhiteSpace(dto.TopicName) ? null : dto.TopicName.Trim();
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

		private IQueryable<ErrorTypeAspectViewDTO> BuildLinkQuery()
		{
			return _context.ErrorTypeAspects
				.AsNoTracking()
				.Include(link => link.ErrorType)
				.Include(link => link.KnowledgeAspect)
				.Select(link => new ErrorTypeAspectViewDTO
				{
					Id = link.Id,
					ErrorTypeId = link.ErrorTypeId,
					ErrorTypeName = link.ErrorType.Name,
					ErrorTypeCode = link.ErrorType.Code,
					KnowledgeAspectId = link.KnowledgeAspectId,
					KnowledgeAspectName = link.KnowledgeAspect.Name,
					TopicName = link.KnowledgeAspect.TopicName,
					Weight = link.Weight
				});
		}

		private async Task<ErrorTypeAspectViewDTO> GetLinkByIdAsync(int linkId)
		{
			return await BuildLinkQuery()
				.FirstAsync(link => link.Id == linkId);
		}

		private static KnowledgeAspectViewDTO MapAspect(KnowledgeAspect aspect)
		{
			return new KnowledgeAspectViewDTO
			{
				Id = aspect.Id,
				Name = aspect.Name,
				Description = aspect.Description,
				TopicName = aspect.TopicName,
				IsActive = aspect.IsActive
			};
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
	}
}
