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

		private readonly IAlphaBetaErrorAnalysisService _errorAnalysisService;
		private readonly IErrorClassificationService _errorClassificationService;

		public ABController(
			MainDbContext context,
			UserManager<ApplicationUser> userManager,
			UsersService usersService,
			IAlphaBetaErrorAnalysisService errorAnalysisService,
			IErrorClassificationService errorClassificationService)
		{
			_userManager = userManager;
			_context = context;
			_usersService = usersService;
			_errorAnalysisService = errorAnalysisService;
			_errorClassificationService = errorClassificationService;
		}

		private async Task SaveAnalysisErrorsAsync(int alphaBetaId, ErrorAnalysisResult analysisResult)
		{
			var oldErrors = await _context.ErrorRecords
				.Where(e => e.AlphaBetaId == alphaBetaId)
				.ToListAsync();

			if (oldErrors.Count > 0)
			{
				_context.ErrorRecords.RemoveRange(oldErrors);
			}

			if (analysisResult.Errors.Count == 0)
			{
				return;
			}

			var errorEntities = analysisResult.Errors.Select(error => new ErrorRecord
			{
				AlphaBetaId = alphaBetaId,
				Code = error.Code,
				Message = error.Message,
				NodeId = error.NodeId,
				TreeLevel = error.TreeLevel,
				ElementType = error.ElementType,
				ExpectedA = error.ExpectedA,
				ActualA = error.ActualA,
				ExpectedB = error.ExpectedB,
				ActualB = error.ActualB,
				PathStepIndex = error.PathStepIndex,
				ExpectedPathNodeId = error.ExpectedPathNodeId,
				ActualPathNodeId = error.ActualPathNodeId,
				IsPrimary = error.IsPrimary,
				SeverityScore = error.SeverityScore,
				GroupKey = error.GroupKey,
				RootBranchId = error.RootBranchId,
				IsOnCorrectPath = error.IsOnCorrectPath,
				IsUserPruned = error.IsUserPruned,
				IsExpectedPruned = error.IsExpectedPruned,
				CreatedAt = DateTime.UtcNow
			}).ToList();

			await _context.ErrorRecords.AddRangeAsync(errorEntities);
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

        [Authorize, HttpGet("Test")]
        public async Task<ActionResult<AlphaBetaDTO>> GetABTest()
        {
            var ab = await _context.AlphaBeta.FirstOrDefaultAsync(f => f.UserId == _userManager.GetUserId(User));
            if (ab == null)
            {
                return NotFound();
            }
            if (ab.IsSolved)
            {
                return new AlphaBetaDTO() {
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
                var problemInner = AlphaBetaService.GenerateTree3((int)ab.MaxValue, (int)ab.Template);
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

		[Authorize, HttpPost("Test")]
		public async Task<ActionResult<AlphaBetaSolutionDTO>> PostABTestVerify(AlphaBetaSolutionDTO userSolution)
		{
			var ab = await _context.AlphaBeta.FirstOrDefaultAsync(f => f.UserId == _userManager.GetUserId(User));
			if (ab == null)
			{
				return NotFound();
			}

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

			var problem = ab.Problem.FromJson<ProblemTree<ABNode>>();
			var solution = AlphaBetaService.Search(problem);

			ab.Solution = solution.Nodes.ToJson();
			ab.Path = solution.Path.ToJson();
			ab.IsSolved = true;

			var analysisResult = _errorAnalysisService.Analyze(problem, userSolution, solution);

			await SaveAnalysisErrorsAsync(ab.Id, analysisResult);

			_context.AlphaBeta.Update(ab);
			await _context.SaveChangesAsync();

			await _errorClassificationService.ClassifyErrorsAsync(ab.Id);

			return solution;
		}

		[Authorize, HttpPost("Test/Analyze")]
		public async Task<ActionResult<ErrorAnalysisResult>> AnalyzeABTest(AlphaBetaSolutionDTO userSolution)
		{
			var ab = await _context.AlphaBeta.FirstOrDefaultAsync(f => f.UserId == _userManager.GetUserId(User));
			if (ab == null)
			{
				return NotFound();
			}

			if (ab.Problem == null)
			{
				return BadRequest("Задача не была сгенерирована.");
			}

			var problem = ab.Problem.FromJson<ProblemTree<ABNode>>();
			var solution = AlphaBetaService.Search(problem);

			var analysisResult = _errorAnalysisService.Analyze(problem, userSolution, solution);

			return Ok(analysisResult);
		}

		[Authorize, HttpGet("Test/Errors")]
		public async Task<ActionResult<List<ErrorRecord>>> GetABTestErrors()
		{
			var ab = await _context.AlphaBeta.FirstOrDefaultAsync(f => f.UserId == _userManager.GetUserId(User));
			if (ab == null)
			{
				return NotFound();
			}

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

		[Authorize, HttpGet("Test/Errors/Classified")]
		public async Task<ActionResult<List<object>>> GetABTestClassifiedErrors()
		{
			var ab = await _context.AlphaBeta.FirstOrDefaultAsync(f => f.UserId == _userManager.GetUserId(User));
			if (ab == null)
			{
				return NotFound();
			}

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

		[Authorize, HttpGet("Test/Errors/Aspects")]
		public async Task<ActionResult<List<object>>> GetABTestErrorAspects()
		{
			var ab = await _context.AlphaBeta.FirstOrDefaultAsync(f => f.UserId == _userManager.GetUserId(User));
			if (ab == null)
			{
				return NotFound();
			}

			var result = await _context.ErrorRecords
				.Where(e => e.AlphaBetaId == ab.Id && e.ErrorTypeId != null)
				.Include(e => e.ErrorType)
					.ThenInclude(et => et!.ErrorTypeAspects)
						.ThenInclude(eta => eta.KnowledgeAspect)
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
							eta.KnowledgeAspect.TopicName,
							eta.Weight
						}).Cast<object>().ToList()
				})
				.ToListAsync<object>();

			return Ok(result);
		}

		private async Task<bool> _assignTask(string userId, int treeHeight, int maxValue, int template)
		{
			if ((await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)) == null)
			{
				return false;
			}

			var ab = await _context.AlphaBeta.FirstOrDefaultAsync(f => f.UserId == userId);
			if (ab == null)
			{
				_context.AlphaBeta.Add(new AlphaBeta() { UserId = userId });
				await _context.SaveChangesAsync();
				ab = await _context.AlphaBeta.FirstOrDefaultAsync(f => f.UserId == userId);
			}

			var oldErrors = await _context.ErrorRecords
				.Where(e => e.AlphaBetaId == ab.Id)
				.ToListAsync();

			if (oldErrors.Count > 0)
			{
				_context.ErrorRecords.RemoveRange(oldErrors);
			}

			ab.TreeHeight = treeHeight;
			ab.UserSolution = null;
			ab.UserPath = null;
			ab.Solution = null;
			ab.Path = null;
			ab.Problem = null;
			ab.IsSolved = false;
			ab.Date = DateTime.Now;
			ab.Template = template;
			ab.MaxValue = maxValue;

			_context.AlphaBeta.Update(ab);
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
                        Problem = ab.Problem == null ? null : ab.Problem.FromJson<ProblemTree<ABNode>>(),
                        TreeHeight = ab.TreeHeight,
                        Date = ab.Date,
                        IsSolved = ab.IsSolved
                    },
                    User = u
                });
            return await ab.ToArrayAsync();
        }

        [DisableRateLimiting]
        [Authorize(Roles = "Administrator"), HttpGet("Users/{userId}/")]
        public async Task<ActionResult<AlphaBetaTaskDTO>> GetUser(string userId)
        {
            var ab = _usersService.UserLeftJoinGroup(userId).Join(_context.AlphaBeta,
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
            var task = await ab.FirstOrDefaultAsync();
            return task == null ? NotFound() : task;
        }

        [DisableRateLimiting]
        [Authorize(Roles = "Administrator"), HttpPut("Users/{userId}/")]
        public async Task<ActionResult> UpdateABTest(string userId, int? height = null, int? maxValue = null, int? template = null, bool generate = false)
        {
            var ab = await _context.AlphaBeta.FirstOrDefaultAsync(f => f.UserId == userId);
            if (ab == null)
            {
                if (_context.Users.FirstOrDefault(f => f.Id == userId) != null)
                {
                    _context.AlphaBeta.Add(new AlphaBeta() { UserId = userId });
                    _context.SaveChanges();
                    ab = await _context.AlphaBeta.FirstOrDefaultAsync(f => f.UserId == userId);
                }
                else
                {
                    return NotFound();
                }
            }
			if (height != null)
			{
				ab.TreeHeight = (int)height;
			}

			ab.Problem = null;
			ab.Solution = null;
			ab.UserSolution = null;
			ab.UserPath = null;
			ab.Path = null;
			ab.IsSolved = false;
			ab.Date = DateTime.Now;
			ab.MaxValue = maxValue;
			ab.Template = template;

			var oldErrors = await _context.ErrorRecords
				.Where(e => e.AlphaBetaId == ab.Id)
				.ToListAsync();

			if (oldErrors.Count > 0)
			{
				_context.ErrorRecords.RemoveRange(oldErrors);
			}

			if (generate == true)
			{
				var tree = AlphaBetaService.GenerateTree3((int)ab.MaxValue, (int)ab.Template);
				ab.Problem = tree.ToJson();
			}

			_context.AlphaBeta.Update(ab);
			await _context.SaveChangesAsync();
			return Ok();
		}

        [DisableRateLimiting]
        [Authorize(Roles = "Administrator"), HttpDelete("Users/{userId}")]
        public async Task<ActionResult> DeleteABTest(string userId)
        {
            await _context.AlphaBeta.Where(f => f.UserId == userId).ExecuteDeleteAsync();
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
