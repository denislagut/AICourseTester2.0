using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services;
using AICourseTester.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using NuGet.Protocol;

namespace AICourseTester.Controllers
{
    [EnableRateLimiting("token")]
    [Route("api/[controller]")]
    [ApiController]
    public class AController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MainDbContext _context;
        private readonly UsersService _usersService;
        private readonly Random _random = new Random();

		private readonly ITaskAnalysisPipelineService _taskAnalysisPipelineService;

		public AController(
	    MainDbContext context,
	    UserManager<ApplicationUser> userManager,
	    UsersService usersService,
		ITaskAnalysisPipelineService taskAnalysisPipelineService,
		IFifteenPuzzleErrorAnalysisService fifteenPuzzleErrorAnalysisService,
	    IErrorClassificationService errorClassificationService,
	    IKnowledgeGapDetectionService knowledgeGapDetectionService)
		{
			_userManager = userManager;
			_context = context;
			_usersService = usersService;
			_taskAnalysisPipelineService = taskAnalysisPipelineService;
		}

		[Authorize]
		[HttpPost("FifteenPuzzle/Debug/ResetSolved/{id}")]
		public async Task<ActionResult> DebugResetSolved(int id)
		{
			var userId = _userManager.GetUserId(User);

			var fp = await _context.Fifteens
				.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

			if (fp == null)
				return NotFound();

			fp.IsSolved = false;
			fp.UserSolution = null;
			fp.Solution = null;

			await RemoveFifteenPuzzleAnalysisDataAsync(fp.Id);
			await _context.SaveChangesAsync();

			return Ok();
		}

		[HttpGet("FifteenPuzzle/Train")]
        public ActionResult<List<ANode>> GetFPTrain(int heuristic = 1, int iters = 3, int dimensions = 3)
        {
            if (heuristic < 1 || heuristic > 2 || iters < 1 || dimensions < 1)
            {
                return BadRequest();
            }

            //ANode aNode = new ANode(dimensions);
            //FifteenPuzzleService.ShuffleState(aNode);
            var aNode = FifteenPuzzleService.GenerateState(_random.Next(1, 5), heuristic, dimensions);
            var (_, list) = FifteenPuzzleService.GenerateTree(aNode, iters);
            return list;
        }

        [HttpPost("FifteenPuzzle/Train")]
        public ActionResult<List<ANodeDTO>> PostFPTrainVerify(List<ANode> list, [FromQuery] int heuristic = 1)
        {
            if (heuristic != 1 && heuristic != 2)
            {
                return BadRequest();
            }
            var tree = FifteenPuzzleService.ListToTree(list);
            var solution = FifteenPuzzleService.Search(tree, FifteenPuzzleService.Heuristics[heuristic - 1]);
            return solution;
        }

		[Authorize, HttpGet("FifteenPuzzle/Test/{id}")]
		public async Task<ActionResult<FifteenPuzzleDTO>> GetFPTest(int id)
		{
			var userId = _userManager.GetUserId(User);

			var fp = await _context.Fifteens
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (fp == null)
				return NotFound();

			if (fp.IsSolved)
			{
				var (_, problem) = FifteenPuzzleService.GenerateTree(
					new ANode() { State = fp.Problem.FromJson<int[][]>() },
					fp.TreeHeight);

				var solution = fp.Solution.FromJson<List<ANodeDTO>>();
				var userSolution = fp.UserSolution.FromJson<List<ANodeDTO>>();

				return new FifteenPuzzleDTO()
				{
					Id = fp.Id,
					Problem = problem,
					Solution = solution,
					UserSolution = userSolution,
					Date = fp.Date,
					IsSolved = fp.IsSolved,
					Heuristic = fp.Heuristic,
					Dimensions = fp.Dimensions,
					TreeHeight = fp.TreeHeight,
				};
			}

			if (fp.Problem == null)
			{
				if (fp.Heuristic == null)
					return BadRequest("Недостаточно параметров для генерации задания FifteenPuzzle.");

				var aNode = FifteenPuzzleService.GenerateState(
					_random.Next(1, 5),
					fp.Heuristic.Value,
					fp.Dimensions);

				fp.Problem = aNode.State.ToJson();
				_context.Fifteens.Update(fp);
				await _context.SaveChangesAsync();
			}

			var (_, list) = FifteenPuzzleService.GenerateTree(
				new ANode() { State = fp.Problem.FromJson<int[][]>() },
				fp.TreeHeight);

			return new FifteenPuzzleDTO()
			{
				Id = fp.Id,
				Problem = list,
				Date = fp.Date,
				IsSolved = fp.IsSolved,
				Heuristic = fp.Heuristic,
				Dimensions = fp.Dimensions,
				TreeHeight = fp.TreeHeight,
			};
		}
		[Authorize, HttpPost("FifteenPuzzle/Test/{id}")]
		public async Task<ActionResult<List<ANodeDTO>>> PostFPTestVerify(
			int id,
			List<ANodeDTO> userSolution)
		{
			var userId = _userManager.GetUserId(User);

			var fp = await _context.Fifteens
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (fp == null)
				return NotFound();

			if (fp.IsSolved)
				return fp.Solution.FromJson<List<ANodeDTO>>();

			if (fp.Problem == null || fp.Heuristic == null)
				return BadRequest("Недостаточно данных для проверки задания FifteenPuzzle.");

			var (problemTree, problem) = FifteenPuzzleService.GenerateTree(
				new ANode() { State = fp.Problem.FromJson<int[][]>() },
				fp.TreeHeight);

			fp.UserSolution = userSolution.ToJson();

			var solution = FifteenPuzzleService.Search(
				problemTree,
				FifteenPuzzleService.Heuristics[fp.Heuristic.Value - 1]);

			fp.Solution = solution.ToJson();
			fp.IsSolved = true;

			_context.Fifteens.Update(fp);
			await _context.SaveChangesAsync();

			await _taskAnalysisPipelineService.AnalyzeAsync(
				"FifteenPuzzle",
				fp.Id,
				fp.UserId);

			return solution;
		}

		[Authorize, HttpGet("FifteenPuzzle/Test/{id}/Errors")]
		public async Task<ActionResult<List<ErrorRecord>>> GetFPTestErrors(int id)
		{
			var userId = _userManager.GetUserId(User);

			var fp = await _context.Fifteens
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (fp == null)
				return NotFound();

			var errors = await _context.ErrorRecords
				.Where(e => e.TaskTypeId == LookupIds.TaskTypeId("FifteenPuzzle") && e.FifteenPuzzleId == fp.Id)
				.Include(e => e.ErrorType)
				.OrderBy(e => e.Id)
				.Select(e => new
				{
					e.Id,
					Code = e.ErrorType == null ? null : e.ErrorType.Code,
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

		[Authorize, HttpGet("FifteenPuzzle/Test/{id}/KnowledgeGaps")]
		public async Task<ActionResult<List<object>>> GetFPTestKnowledgeGaps(int id)
		{
			var userId = _userManager.GetUserId(User);

			var fp = await _context.Fifteens
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (fp == null)
				return NotFound();

			var gaps = await _context.KnowledgeGaps
				.Where(g => g.TaskTypeId == LookupIds.TaskTypeId("FifteenPuzzle") && g.FifteenPuzzleId == fp.Id)
				.Include(g => g.KnowledgeAspect)
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

		[Authorize, HttpGet("FifteenPuzzle/MyTasks")]
		public async Task<ActionResult<List<FifteenPuzzleDTO>>> GetMyFifteenPuzzleTasks()
		{
			var userId = _userManager.GetUserId(User);

			var tasks = await _context.Fifteens
				.Where(x => x.UserId == userId)
				.OrderByDescending(x => x.Date)
				.Select(fp => new FifteenPuzzleDTO
				{
					Id = fp.Id,
					Date = fp.Date,
					IsSolved = fp.IsSolved,
					Heuristic = fp.Heuristic,
					Dimensions = fp.Dimensions,
					TreeHeight = fp.TreeHeight,
					Problem = null,
					Solution = null,
					UserSolution = null
				})
				.ToListAsync();

			return Ok(tasks);
		}

		private async Task RemoveFifteenPuzzleAnalysisDataAsync(int fifteenPuzzleId)
		{
			var oldErrors = await _context.ErrorRecords
				.Where(e => e.TaskTypeId == LookupIds.TaskTypeId("FifteenPuzzle") && e.FifteenPuzzleId == fifteenPuzzleId)
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

		[Authorize, HttpGet("FifteenPuzzle/Test/{id}/CausalLinks")]
		public async Task<ActionResult<List<object>>> GetFifteenPuzzleTestCausalLinks(int id)
		{
			var userId = _userManager.GetUserId(User);

			var puzzle = await _context.Fifteens
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (puzzle == null)
				return NotFound();

			var links = await _context.CausalErrorLinks
				.Where(l =>
					l.SourceError.FifteenPuzzleId == puzzle.Id ||
					l.TargetError.FifteenPuzzleId == puzzle.Id)
				.OrderByDescending(l => l.Weight)
				.Select(l => new
				{
					l.Id,
					RelationType = l.RelationTypeRef.Code,
					l.Weight,
					SourceError = new
					{
						l.SourceError.Id,
						Code = l.SourceError.ErrorType == null ? null : l.SourceError.ErrorType.Code,
						l.SourceError.Message,
						l.SourceError.NodeId
					},
					TargetError = new
					{
						l.TargetError.Id,
						Code = l.TargetError.ErrorType == null ? null : l.TargetError.ErrorType.Code,
						l.TargetError.Message,
						l.TargetError.NodeId
					}
				})
				.ToListAsync<object>();

			return Ok(links);
		}

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator")]
		[HttpGet("FifteenPuzzle/Users/{userId}/Tasks/{id}/CausalLinks")]
		public async Task<ActionResult<List<object>>> GetUserFifteenPuzzleCausalLinks(string userId, int id)
		{
			var puzzle = await _context.Fifteens
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (puzzle == null)
				return NotFound();

			var links = await _context.CausalErrorLinks
				.Where(l =>
					l.SourceError.FifteenPuzzleId == puzzle.Id ||
					l.TargetError.FifteenPuzzleId == puzzle.Id)
				.OrderByDescending(l => l.Weight)
				.Select(l => new
				{
					l.Id,
					RelationType = l.RelationTypeRef.Code,
					l.Weight,
					SourceError = new
					{
						l.SourceError.Id,
						Code = l.SourceError.ErrorType == null ? null : l.SourceError.ErrorType.Code,
						l.SourceError.Message,
						l.SourceError.NodeId
					},
					TargetError = new
					{
						l.TargetError.Id,
						Code = l.TargetError.ErrorType == null ? null : l.TargetError.ErrorType.Code,
						l.TargetError.Message,
						l.TargetError.NodeId
					}
				})
				.ToListAsync<object>();

			return Ok(links);
		}

		private async Task<bool> _assignTask(string userId, int heuristic, int dimensions, int iters)
		{
			var userExists = await _context.Users.AnyAsync(u => u.Id == userId);

			if (!userExists)
				return false;

			var fp = new FifteenPuzzle
			{
				UserId = userId,
				Heuristic = heuristic,
				Dimensions = dimensions,
				TreeHeight = iters,
				Problem = null,
				UserSolution = null,
				Solution = null,
				IsSolved = false,
				Date = DateTime.Now
			};

			await _context.Fifteens.AddAsync(fp);

			return true;
		}

		[DisableRateLimiting]
        [Authorize(Roles = "Administrator"), HttpPost("FifteenPuzzle/Users/Assign")]
        public async Task<ActionResult> PostFPTestAssign(string[] userIds, int dimensions = 3, int iters = 3, int heuristic = 1)
        {
            foreach (var userId in userIds)
            {
                await _assignTask(userId, heuristic, dimensions, iters);
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [DisableRateLimiting]
        [Authorize(Roles = "Administrator"), HttpPost("FifteenPuzzle/Users/{userId}/Assign")]
        public async Task<ActionResult> PostFPTestAssign(string userId, int dimensions = 3, int iters = 3, int heuristic = 1)
        {
            if (await _assignTask(userId, heuristic, dimensions, iters))
            {
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        [DisableRateLimiting]
        [Authorize(Roles = "Administrator"), HttpPost("FifteenPuzzle/Groups/{groupId}/Assign")]
        public async Task<ActionResult> PostFPTestAssign(int groupId, int dimensions = 3, int iters = 3, int heuristic = 1)
        {
            var userIds = await _context.UserGroups.Include(ug => ug.User).Where(ug => ug.GroupId == groupId).Select(ug => ug.UserId).ToArrayAsync();
            foreach (var userId in userIds)
            {
                await _assignTask(userId, heuristic, dimensions, iters);
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [DisableRateLimiting]
        [Authorize(Roles = "Administrator"), HttpGet("FifteenPuzzle/Users/")]
        public async Task<ActionResult<FifteenPuzzleTaskDTO[]?>> GetUsers()
        {
            var fp = _usersService.UserLeftJoinGroup().Join(_context.Fifteens,
                u => u.Id,
                fp => fp.UserId,
                (u, fp) => new FifteenPuzzleTaskDTO
                    {
                        Task = new FifteenPuzzleDTO
                        {
                            Id = fp.Id,
                            Problem = fp.Problem == null ? null : new List<ANode>() { new ANode() { State = fp.Problem.FromJson<int[][]>() } },
                            Solution = null,
                            UserSolution = null,
                            Heuristic = fp.Heuristic,
                            Dimensions = fp.Dimensions,
                            TreeHeight = fp.TreeHeight,
                            IsSolved = fp.IsSolved,
                            Date = fp.Date,
                        },
                        User = u
                });
            return await fp
			.OrderByDescending(x => x.Task!.Date)
			.ToArrayAsync();
		}

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator"), HttpGet("FifteenPuzzle/Users/{userId}/")]
		public async Task<ActionResult<FifteenPuzzleTaskDTO[]>> GetUserTasks(string userId)
		{
			var tasks = _usersService.UserLeftJoinGroup(userId).Join(_context.Fifteens,
				u => u.Id,
				fp => fp.UserId,
				(u, fp) => new FifteenPuzzleTaskDTO
				{
					Task = new FifteenPuzzleDTO
					{
						Id = fp.Id,
						Problem = fp.Problem == null ? null : FifteenPuzzleService.GenerateTree(
							new ANode() { State = fp.Problem.FromJson<int[][]>() },
							fp.TreeHeight).Item2,
						Solution = fp.Solution == null ? null : fp.Solution.FromJson<List<ANodeDTO>>(),
						UserSolution = fp.UserSolution == null ? null : fp.UserSolution.FromJson<List<ANodeDTO>>(),
						Heuristic = fp.Heuristic,
						Dimensions = fp.Dimensions,
						TreeHeight = fp.TreeHeight,
						IsSolved = fp.IsSolved,
						Date = fp.Date,
					},
					User = u
				});

			var result = await tasks
				.OrderByDescending(x => x.Task!.Date)
				.ToArrayAsync();

			return result.Length == 0 ? NotFound() : result;
		}

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator"), HttpGet("FifteenPuzzle/Users/{userId}/Tasks/{id}")]
		public async Task<ActionResult<FifteenPuzzleTaskDTO>> GetUserTask(string userId, int id)
		{
			var task = await _usersService.UserLeftJoinGroup(userId).Join(_context.Fifteens,
				u => u.Id,
				fp => fp.UserId,
				(u, fp) => new FifteenPuzzleTaskDTO
				{
					Task = new FifteenPuzzleDTO
					{
						Id = fp.Id,
						Problem = fp.Problem == null ? null : FifteenPuzzleService.GenerateTree(
							new ANode() { State = fp.Problem.FromJson<int[][]>() },
							fp.TreeHeight).Item2,
						Solution = fp.Solution == null ? null : fp.Solution.FromJson<List<ANodeDTO>>(),
						UserSolution = fp.UserSolution == null ? null : fp.UserSolution.FromJson<List<ANodeDTO>>(),
						Heuristic = fp.Heuristic,
						Dimensions = fp.Dimensions,
						TreeHeight = fp.TreeHeight,
						IsSolved = fp.IsSolved,
						Date = fp.Date,
					},
					User = u
				})
				.FirstOrDefaultAsync(x => x.Task!.Id == id);

			return task == null ? NotFound() : task;
		}

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator"), HttpPut("FifteenPuzzle/Users/{userId}/Tasks/{id}")]
		public async Task<ActionResult> UpdateFPTest(
	int[][]? State,
	string userId,
	int id,
	[FromQuery] int? iters = null,
	[FromQuery] int? dimensions = null,
	[FromQuery] bool generate = false)
		{
			var fp = await _context.Fifteens
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (fp == null)
				return NotFound();

			if (iters != null)
				fp.TreeHeight = (int)iters;

			if (dimensions != null)
				fp.Dimensions = (int)dimensions;

			await RemoveFifteenPuzzleAnalysisDataAsync(fp.Id);

			fp.UserSolution = null;
			fp.Problem = null;
			fp.Solution = null;
			fp.IsSolved = false;
			fp.Date = DateTime.Now;

			if (State != null)
			{
				fp.Problem = State.ToJson();
				fp.Dimensions = State.Length;
			}
			else if (generate == true)
			{
				ANode aNode = new ANode(fp.Dimensions);
				FifteenPuzzleService.ShuffleState(aNode);
				fp.Problem = aNode.State.ToJson();
			}

			_context.Fifteens.Update(fp);
			await _context.SaveChangesAsync();

			return Ok();
		}

		[DisableRateLimiting]
		[Authorize(Roles = "Administrator"), HttpDelete("FifteenPuzzle/Users/{userId}/Tasks/{id}")]
		public async Task<ActionResult> DeleteFPTest(string userId, int id)
		{
			var fp = await _context.Fifteens
				.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

			if (fp == null)
				return NotFound();

			await RemoveFifteenPuzzleAnalysisDataAsync(fp.Id);

			_context.Fifteens.Remove(fp);
			await _context.SaveChangesAsync();

			return Ok();
		}
	}
}
