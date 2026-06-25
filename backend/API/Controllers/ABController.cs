using Microsoft.AspNetCore.Mvc;
using NuGet.Protocol;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AICourseTester.Data;
using AICourseTester.Models;
using AICourseTester.Services;
using Microsoft.AspNetCore.RateLimiting;
using AICourseTester.DTO;
using AICourseTester.Services.Interfaces;
using AICourseTester.Models.Analysis;

namespace AICourseTester.Controllers
{
    [EnableRateLimiting("token")]
    [Route("api/[controller]")]
    [ApiController]
    public class ABController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MainDbContext _context;
        private readonly UsersService _usersService;

		private readonly ITaskAnalysisPipelineService _taskAnalysisPipelineService;
		private readonly IAlphaBetaErrorAnalysisService _errorAnalysisService;

		public ABController(
		MainDbContext context,
		UserManager<ApplicationUser> userManager,
		UsersService usersService,
		IAlphaBetaErrorAnalysisService errorAnalysisService,
		ITaskAnalysisPipelineService taskAnalysisPipelineService)
		{
			_userManager = userManager;
			_context = context;
			_usersService = usersService;
			_errorAnalysisService = errorAnalysisService;
			_taskAnalysisPipelineService = taskAnalysisPipelineService;
		}

		[Authorize, HttpGet("MyTasks")]
		public async Task<ActionResult<List<AlphaBetaDTO>>> GetMyAlphaBetaTasks()
		{
			var userId = _userManager.GetUserId(User);

			var tasks = await _context.AlphaBeta
				.Where(x => x.UserId == userId)
				.OrderByDescending(x => x.Date)
				.Select(ab => new AlphaBetaDTO
				{
					Id = ab.Id,
					Problem = ab.Problem == null ? null : ab.Problem.FromJson<ProblemTree<ABNode>>(),
					Solution = ab.Solution == null ? null : ab.Solution.FromJson<List<ABNodeDTO>>(),
					UserSolution = ab.UserSolution == null ? null : ab.UserSolution.FromJson<List<ABNodeDTO>>(),
					Path = ab.Path == null ? null : ab.Path.FromJson<int[]>(),
					UserPath = ab.UserPath == null ? null : ab.UserPath.FromJson<int[]>(),
					Date = ab.Date,
					IsSolved = ab.IsSolved,
					TreeHeight = ab.TreeHeight
				})
				.ToListAsync();

			return Ok(tasks);
		}

		[Authorize]
		[HttpPost("Debug/ResetSolved/{id}")]
		public async Task<ActionResult> DebugResetSolved(int id)
		{
			var userId = _userManager.GetUserId(User);

			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

			if (ab == null)
				return NotFound();

			ab.IsSolved = false;
			ab.UserSolution = null;
			ab.UserPath = null;
			ab.UserPrunedNodeIds = null;

			await RemoveAlphaBetaAnalysisDataAsync(ab.Id);
			await _context.SaveChangesAsync();

			return Ok();
		}

		[Authorize]
		[HttpPost("Debug/AssignMe")]
		public async Task<ActionResult<object>> DebugAssignMe(int treeHeight = 3, int max = 15, int template = 1)
		{
			var userId = _userManager.GetUserId(User);

			if (userId == null)
				return Unauthorized();

			try
			{
				var ab = await CreateAlphaBetaTaskAsync(userId, treeHeight, max, template);
				await _context.SaveChangesAsync();

				return Ok(new { ab.Id });
			}
			catch (InvalidOperationException)
			{
				return NotFound();
			}
		}

		private async Task<AlphaBeta> CreateAlphaBetaTaskAsync(
			string userId,
			int treeHeight,
			int maxValue,
			int template)
		{
			var userExists = await _context.Users.AnyAsync(u => u.Id == userId);

			if (!userExists)
			{
				throw new InvalidOperationException("Пользователь не найден.");
			}

			var ab = new AlphaBeta
			{
				UserId = userId,
				TreeHeight = treeHeight,
				MaxValue = maxValue,
				Template = template,
				Problem = null,
				Solution = null,
				UserSolution = null,
				UserPath = null,
				UserPrunedNodeIds = null,
				Path = null,
				IsSolved = false,
				Date = DateTime.Now
			};

			await _context.AlphaBeta.AddAsync(ab);

			return ab;
		}

		[HttpGet("Train")]
        public ActionResult<ProblemTree<ABNode>> GetABTrain(int depth = 3, int max = 15, int template = 1)
        {
            if (max < 4 || template < 1 || template > 4)
            {
                return BadRequest();
            }
            var tree = AlphaBetaService.GenerateTree3(max, template);
            return tree;
        }

        [HttpPost("Train")]
        public ActionResult<AlphaBetaSolutionDTO> PostABTrainVerify(ProblemTree<ABNode> tree)
        {
            var solution = AlphaBetaService.Search(tree);
            return solution;
        }

		[Authorize, HttpGet("Test/{id}")]
		public async Task<ActionResult<AlphaBetaDTO>> GetABTest(int id)
		{
			var userId = _userManager.GetUserId(User);

			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			if (ab.IsSolved)
			{
				return new AlphaBetaDTO()
				{
					Id = ab.Id,
					Problem = ab.Problem == null ? null : ab.Problem.FromJson<ProblemTree<ABNode>>(),
					Solution = ab.Solution == null ? null : ab.Solution.FromJson<List<ABNodeDTO>>(),
					UserSolution = ab.UserSolution == null ? null : ab.UserSolution.FromJson<List<ABNodeDTO>>(),
					Path = ab.Path == null ? null : ab.Path.FromJson<int[]>(),
					UserPath = ab.UserPath == null ? null : ab.UserPath.FromJson<int[]>(),
					Date = ab.Date,
					IsSolved = ab.IsSolved
				};
			}

			if (ab.Problem == null)
			{
				if (ab.MaxValue == null || ab.Template == null)
					return BadRequest("Недостаточно параметров для генерации задания AlphaBeta.");

				var problemInner = AlphaBetaService.GenerateTree3(ab.MaxValue.Value, ab.Template.Value);
				ab.Problem = problemInner.ToJson();
				_context.Update(ab);
				await _context.SaveChangesAsync();
			}

			return new AlphaBetaDTO()
			{
				Id = ab.Id,
				Problem = ab.Problem.FromJson<ProblemTree<ABNode>>(),
				Date = ab.Date,
				IsSolved = ab.IsSolved
			};
		}

		[Authorize, HttpPost("Test/{id}")]
		public async Task<ActionResult<AlphaBetaSolutionDTO>> PostABTestVerify(
			int id,
			AlphaBetaSolutionDTO userSolution)
		{
			var userId = _userManager.GetUserId(User);

			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			if (ab.IsSolved)
			{
				return new AlphaBetaSolutionDTO()
				{
					Nodes = ab.Solution == null ? null : ab.Solution.FromJson<List<ABNodeDTO>>(),
					Path = ab.Path == null ? null : ab.Path.FromJson<int[]>()
				};
			}

			ab.UserSolution = userSolution.Nodes.ToJson();
			ab.UserPath = userSolution.Path.ToJson();
			ab.UserPrunedNodeIds = userSolution.PrunedNodeIds.ToJson();

			var problem = ab.Problem.FromJson<ProblemTree<ABNode>>();
			var solution = AlphaBetaService.Search(problem);

			ab.Solution = solution.Nodes.ToJson();
			ab.Path = solution.Path.ToJson();
			ab.IsSolved = true;

			_context.AlphaBeta.Update(ab);
			await _context.SaveChangesAsync();

			await _taskAnalysisPipelineService.AnalyzeAsync(
				"AlphaBeta",
				ab.Id,
				ab.UserId);

			return solution;
		}

		[Authorize, HttpPost("Test/{id}/Analyze")]
		public async Task<ActionResult<ErrorAnalysisResult>> AnalyzeABTest(
			int id,
			AlphaBetaSolutionDTO userSolution)
		{
			var userId = _userManager.GetUserId(User);

			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			if (ab.Problem == null)
				return BadRequest("Задача не была сгенерирована.");

			var problem = ab.Problem.FromJson<ProblemTree<ABNode>>();
			var solution = AlphaBetaService.Search(problem);

			var analysisResult = _errorAnalysisService.Analyze(problem, userSolution, solution);

			return Ok(analysisResult);
		}

		[Authorize, HttpGet("Test/{id}/Errors")]
		public async Task<ActionResult<List<ErrorRecord>>> GetABTestErrors(int id)
		{
			var userId = _userManager.GetUserId(User);

			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			var errors = await _context.ErrorRecords
				.Where(e => e.AlphaBetaId == ab.Id)
				.OrderBy(e => e.Id)
				.ToListAsync();

			return Ok(errors);
		}

		[AllowAnonymous]
		[HttpGet("Debug/AuthHeader")]
		public ActionResult<object> DebugAuthHeader()
		{
			var authHeader = Request.Headers.Authorization.ToString();

			return Ok(new
			{
				AuthorizationHeader = authHeader,
				IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
				Name = User.Identity?.Name
			});
		}

		[Authorize]
		[HttpGet("Debug/Me")]
		public ActionResult<object> DebugMe()
		{
			return Ok(new
			{
				IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
				Name = User.Identity?.Name,
				Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
			});
		}

		[Authorize, HttpGet("Test/{id}/Errors/Classified")]
		public async Task<ActionResult<List<object>>> GetABTestClassifiedErrors(int id)
		{
			var userId = _userManager.GetUserId(User);

			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			var errors = await _context.ErrorRecords
				.Where(e => e.AlphaBetaId == ab.Id)
				.Include(e => e.ErrorType)
				.OrderBy(e => e.Id)
				.Select(e => new
				{
					e.Id,
					e.Code,
					e.Message,
					e.NodeId,
					e.TreeLevel,
					e.ElementType,
					e.ExpectedA,
					e.ActualA,
					e.ExpectedB,
					e.ActualB,
					e.PathStepIndex,
					e.ExpectedPathNodeId,
					e.ActualPathNodeId,
					e.SeverityScore,
					e.GroupKey,
					ErrorType = e.ErrorType == null ? null : new
					{
						e.ErrorType.Id,
						e.ErrorType.Code,
						e.ErrorType.Name,
						e.ErrorType.Description,
						e.ErrorType.DefaultSeverity
					}
				})
				.ToListAsync<object>();

			return Ok(errors);
		}

		[Authorize, HttpGet("Test/{id}/Errors/Aspects")]
		public async Task<ActionResult<List<object>>> GetABTestErrorAspects(int id)
		{
			var userId = _userManager.GetUserId(User);

			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			var result = await _context.ErrorRecords
				.Where(e => e.AlphaBetaId == ab.Id && e.ErrorTypeId != null)
				.Include(e => e.ErrorType)
					.ThenInclude(et => et!.ErrorTypeAspects)
						.ThenInclude(eta => eta.KnowledgeAspect)
						.ThenInclude(ka => ka.Topic)
				.Select(e => new
				{
					e.Id,
					e.Code,
					e.Message,
					ErrorType = e.ErrorType == null ? null : new
					{
						e.ErrorType.Id,
						e.ErrorType.Code,
						e.ErrorType.Name
					},
					Aspects = e.ErrorType == null
						? new List<object>()
						: e.ErrorType.ErrorTypeAspects.Select(eta => new
						{
							eta.KnowledgeAspectId,
							eta.KnowledgeAspect.Name,
							eta.KnowledgeAspect.Description,
							TopicName = eta.KnowledgeAspect.Topic == null ? null : eta.KnowledgeAspect.Topic.Name,
							eta.Weight
						}).Cast<object>().ToList()
				})
				.ToListAsync<object>();

			return Ok(result);
		}

		[Authorize, HttpGet("Test/{id}/KnowledgeGaps")]
		public async Task<ActionResult<List<object>>> GetABTestKnowledgeGaps(int id)
		{
			var userId = _userManager.GetUserId(User);

			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			var gaps = await _context.KnowledgeGaps
				.Where(g => g.AlphaBetaId == ab.Id)
				.Include(g => g.KnowledgeAspect)
					.ThenInclude(ka => ka.Topic)
				.Include(g => g.LevelRef)
				.OrderByDescending(g => g.GapScore)
				.Select(g => new
				{
					g.Id,
					g.KnowledgeAspectId,
					AspectName = g.KnowledgeAspect.Name,
					AspectDescription = g.KnowledgeAspect.Description,
					g.ErrorCount,
					g.TotalWeight,
					g.AverageSeverity,
					g.GapScore,
					Level = g.LevelRef.Code,
					g.CreatedAt
				})
				.ToListAsync<object>();

			return Ok(gaps);
		}

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator"), HttpGet("Users/{userId}/Tasks/{id}/KnowledgeGaps")]
		public async Task<ActionResult<List<object>>> GetUserKnowledgeGaps(string userId, int id)
		{
			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			var gaps = await _context.KnowledgeGaps
				.Where(g => g.AlphaBetaId == ab.Id)
				.Include(g => g.KnowledgeAspect)
					.ThenInclude(ka => ka.Topic)
				.Include(g => g.LevelRef)
				.OrderByDescending(g => g.GapScore)
				.Select(g => new
				{
					g.Id,
					g.KnowledgeAspectId,
					AspectName = g.KnowledgeAspect.Name,
					AspectDescription = g.KnowledgeAspect.Description,
					g.ErrorCount,
					g.TotalWeight,
					g.AverageSeverity,
					g.GapScore,
					Level = g.LevelRef.Code,
					g.CreatedAt
				})
				.ToListAsync<object>();

			return Ok(gaps);
		}

		private async Task RemoveAlphaBetaAnalysisDataAsync(int alphaBetaId)
		{
			var oldErrors = await _context.ErrorRecords
				.Where(e => e.AlphaBetaId == alphaBetaId)
				.ToListAsync();

			var oldErrorIds = oldErrors.Select(e => e.Id).ToList();

			if (oldErrorIds.Count > 0)
			{
				var oldLinks = await _context.CausalErrorLinks
					.Where(l =>
						oldErrorIds.Contains(l.SourceErrorId) ||
						oldErrorIds.Contains(l.TargetErrorId))
					.ToListAsync();

				_context.CausalErrorLinks.RemoveRange(oldLinks);
			}

			_context.ErrorRecords.RemoveRange(oldErrors);
		}

		[Authorize, HttpGet("Test/{id}/CausalLinks")]
		public async Task<ActionResult<List<object>>> GetABTestCausalLinks(int id)
		{
			var userId = _userManager.GetUserId(User);

			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			var links = await _context.CausalErrorLinks
				.Where(l =>
					l.SourceError.AlphaBetaId == ab.Id ||
					l.TargetError.AlphaBetaId == ab.Id)
				.Include(l => l.SourceError)
				.Include(l => l.TargetError)
				.OrderByDescending(l => l.Weight)
				.Select(l => new
				{
					l.Id,
					RelationType = l.RelationTypeRef.Code,
					l.Weight,
					SourceError = new
					{
						l.SourceError.Id,
						Code = l.SourceError.ErrorType == null ? string.Empty : l.SourceError.ErrorType.Code,
						l.SourceError.Message,
						l.SourceError.NodeId
					},
					TargetError = new
					{
						l.TargetError.Id,
						Code = l.TargetError.ErrorType == null ? string.Empty : l.TargetError.ErrorType.Code,
						l.TargetError.Message,
						l.TargetError.NodeId
					}
				})
				.ToListAsync<object>();

			return Ok(links);
		}

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator")]
		[HttpGet("Users/{userId}/Tasks/{id}/CausalLinks")]
		public async Task<ActionResult<List<object>>> GetUserCausalLinks(string userId, int id)
		{
			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			var links = await _context.CausalErrorLinks
				.Where(l =>
					l.SourceError.AlphaBetaId == ab.Id ||
					l.TargetError.AlphaBetaId == ab.Id)
				.Include(l => l.SourceError)
				.Include(l => l.TargetError)
				.OrderByDescending(l => l.Weight)
				.Select(l => new
				{
					l.Id,
					RelationType = l.RelationTypeRef.Code,
					l.Weight,
					SourceError = new
					{
						l.SourceError.Id,
						Code = l.SourceError.ErrorType == null ? string.Empty : l.SourceError.ErrorType.Code,
						l.SourceError.Message,
						l.SourceError.NodeId
					},
					TargetError = new
					{
						l.TargetError.Id,
						Code = l.TargetError.ErrorType == null ? string.Empty : l.TargetError.ErrorType.Code,
						l.TargetError.Message,
						l.TargetError.NodeId
					}
				})
				.ToListAsync<object>();

			return Ok(links);
		}

		private async Task<bool> _assignTask(string userId, int treeHeight, int maxValue, int template)
		{
			var userExists = await _context.Users.AnyAsync(u => u.Id == userId);

			if (!userExists)
				return false;

			var ab = new AlphaBeta
			{
				UserId = userId,
				TreeHeight = treeHeight,
				UserSolution = null,
				UserPath = null,
				UserPrunedNodeIds = null,
				Solution = null,
				Path = null,
				Problem = null,
				IsSolved = false,
				Date = DateTime.Now,
				Template = template,
				MaxValue = maxValue
			};

			await _context.AlphaBeta.AddAsync(ab);

			return true;
		}

		[DisableRateLimiting]
        [Authorize(Roles = "Administrator"), HttpPost("Users/Assign")]
        public async Task<ActionResult> PostFPTestAssign(string[] userIds, int treeHeight, int max = 15, int template = 1)
        {
            foreach (var userId in userIds)
            {
                await _assignTask(userId, treeHeight, max, template);
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [DisableRateLimiting]
        [Authorize(Roles = "Administrator"), HttpPost("Users/{userId}/Assign")]
        public async Task<ActionResult> PostFPTestAssign(string userId, int treeHeight, int max = 15, int template = 1)
        {
            if (await _assignTask(userId, treeHeight, max, template))
            {
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        [DisableRateLimiting]
        [Authorize(Roles = "Administrator"), HttpPost("Groups/{groupId}/Assign")]
        public async Task<ActionResult> PostFPTestAssign(int groupId, int treeHeight, int max = 15, int template = 1)
        {
            var userIds = await _context.UserGroups.Include(ug => ug.User).Where(ug => ug.GroupId == groupId).Select(ug => ug.UserId).ToArrayAsync();
            foreach (var userId in userIds)
            {
                await _assignTask(userId, treeHeight, max, template);
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator"), HttpGet("Users/")]
		public async Task<ActionResult<AlphaBetaTaskDTO[]?>> GetUsers()
		{
			var ab = _usersService.UserLeftJoinGroup().Join(_context.AlphaBeta,
				u => u.Id,
				ab => ab.UserId,
				(u, ab) => new AlphaBetaTaskDTO
				{
					Task = new AlphaBetaDTO
					{
						Id = ab.Id,
						Problem = ab.Problem == null ? null : ab.Problem.FromJson<ProblemTree<ABNode>>(),
						TreeHeight = ab.TreeHeight,
						Date = ab.Date,
						IsSolved = ab.IsSolved
					},
					User = u
				});

			return await ab
				.OrderByDescending(x => x.Task!.Date)
				.ToArrayAsync();
		}

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator"), HttpGet("Users/{userId}/")]
		public async Task<ActionResult<AlphaBetaTaskDTO[]>> GetUserTasks(string userId)
		{
			var tasks = _usersService.UserLeftJoinGroup(userId).Join(_context.AlphaBeta,
				u => u.Id,
				ab => ab.UserId,
				(u, ab) => new AlphaBetaTaskDTO
				{
					Task = new AlphaBetaDTO
					{
						Id = ab.Id,
						Problem = ab.Problem == null ? null : ab.Problem.FromJson<ProblemTree<ABNode>>(),
						Solution = ab.Solution == null ? null : ab.Solution.FromJson<List<ABNodeDTO>>(),
						UserSolution = ab.UserSolution == null ? null : ab.UserSolution.FromJson<List<ABNodeDTO>>(),
						Path = ab.Path == null ? null : ab.Path.FromJson<int[]>(),
						UserPath = ab.UserPath == null ? null : ab.UserPath.FromJson<int[]>(),
						Date = ab.Date,
						IsSolved = ab.IsSolved,
						TreeHeight = ab.TreeHeight,
					},
					User = u
				});

			var result = await tasks
				.OrderByDescending(x => x.Task!.Date)
				.ToArrayAsync();

			return result.Length == 0 ? NotFound() : result;
		}

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator"), HttpGet("Users/{userId}/Tasks/{id}")]
		public async Task<ActionResult<AlphaBetaTaskDTO>> GetUserTask(string userId, int id)
		{
			var task = await _usersService.UserLeftJoinGroup(userId).Join(_context.AlphaBeta,
				u => u.Id,
				ab => ab.UserId,
				(u, ab) => new AlphaBetaTaskDTO
				{
					Task = new AlphaBetaDTO
					{
						Id = ab.Id,
						Problem = ab.Problem == null ? null : ab.Problem.FromJson<ProblemTree<ABNode>>(),
						Solution = ab.Solution == null ? null : ab.Solution.FromJson<List<ABNodeDTO>>(),
						UserSolution = ab.UserSolution == null ? null : ab.UserSolution.FromJson<List<ABNodeDTO>>(),
						Path = ab.Path == null ? null : ab.Path.FromJson<int[]>(),
						UserPath = ab.UserPath == null ? null : ab.UserPath.FromJson<int[]>(),
						Date = ab.Date,
						IsSolved = ab.IsSolved,
						TreeHeight = ab.TreeHeight,
					},
					User = u
				})
				.FirstOrDefaultAsync(x => x.Task!.Id == id);

			return task == null ? NotFound() : task;
		}

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator"), HttpPut("Users/{userId}/Tasks/{id}")]
		public async Task<ActionResult> UpdateABTest(
	string userId,
	int id,
	int? height = null,
	int? maxValue = null,
	int? template = null,
	bool generate = false)
		{
			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			if (height != null)
				ab.TreeHeight = height.Value;

			if (maxValue != null)
				ab.MaxValue = maxValue;

			if (template != null)
				ab.Template = template;

			ab.Problem = null;
			ab.Solution = null;
			ab.UserSolution = null;
			ab.UserPrunedNodeIds = null;
			ab.UserPath = null;
			ab.Path = null;
			ab.IsSolved = false;
			ab.Date = DateTime.Now;

			await RemoveAlphaBetaAnalysisDataAsync(ab.Id);

			if (generate)
			{
				if (ab.MaxValue == null || ab.Template == null)
					return BadRequest("Недостаточно параметров для генерации задания AlphaBeta.");

				var tree = AlphaBetaService.GenerateTree3(ab.MaxValue.Value, ab.Template.Value);
				ab.Problem = tree.ToJson();
			}

			_context.AlphaBeta.Update(ab);
			await _context.SaveChangesAsync();

			return Ok();
		}

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator"), HttpDelete("Users/{userId}/Tasks/{id}")]
		public async Task<ActionResult> DeleteABTest(string userId, int id)
		{
			var ab = await _context.AlphaBeta
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (ab == null)
				return NotFound();

			await RemoveAlphaBetaAnalysisDataAsync(ab.Id);

			_context.AlphaBeta.Remove(ab);
			await _context.SaveChangesAsync();

			return Ok();
		}
	}
}
