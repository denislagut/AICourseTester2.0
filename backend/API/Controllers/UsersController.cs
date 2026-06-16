using AICourseTester.Data;
using AICourseTester.DTO;
using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using AICourseTester.Services;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;

namespace AICourseTester.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly MainDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IOptionsMonitor<BearerTokenOptions> _bearerTokenOptions;
        private readonly TimeProvider _timeProvider;
        private readonly UsersService _usersService;

        public UsersController(MainDbContext context, UserManager<ApplicationUser> userManager, IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager, IOptionsMonitor<BearerTokenOptions> bearerTokenOptions, TimeProvider timeProvider,
            UsersService usersService)
        {
            _userManager = userManager;
            _userStore = userStore;
            _context = context;
            _signInManager = signInManager;
            _bearerTokenOptions = bearerTokenOptions;
            _timeProvider = timeProvider;
            _usersService = usersService;
        }

        [EnableRateLimiting("token")]
        [Authorize, HttpGet]
        public async Task<ActionResult<UserDTO[]>> GetUsers(bool getSelf = false)
        {
            var reqUser = await _userManager.GetUserAsync(User);
            if (reqUser == null)
            {
                return Unauthorized();
            }

            var roles = await _userManager.GetRolesAsync(reqUser);
            var isAdmin = roles.FirstOrDefault(r => r == "Administrator") != null;
            if (getSelf || !isAdmin)
            {
                var user = _usersService.UserLeftJoinGroup(reqUser.Id, true, true);
                return await user.ToArrayAsync();
            }
            var users = _usersService.UserLeftJoinGroup();
            if (users.IsNullOrEmpty())
            {
                return NotFound();
            }
            return await users.ToArrayAsync();
        }

        [Authorize, HttpGet("TaskHistory")]
        public async Task<ActionResult<StudentTaskHistoryDTO[]>> GetTaskHistory()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var analysisRuns = await _context.AnalysisRuns
                .AsNoTracking()
                .Where(run =>
                    run.UserId == userId &&
                    run.StatusId == LookupIds.AnalysisStatusId("Completed") &&
                    run.CompletedAt.HasValue)
                .OrderByDescending(run => run.CompletedAt)
                .ThenByDescending(run => run.Id)
                .ToListAsync();

            var history = analysisRuns
                .Select(run => new StudentTaskHistoryDTO
                {
                    Id = run.Id,
                    TaskId = run.TaskTypeId == LookupIds.TaskTypeId("AlphaBeta") ? run.AlphaBetaId : run.FifteenPuzzleId,
                    TaskType = run.TaskTypeId == LookupIds.TaskTypeId("FifteenPuzzle") ? "a-star" : "min-max",
                    TaskName = run.TaskTypeId == LookupIds.TaskTypeId("FifteenPuzzle") ? "Пятнашки A*" : "min-max алгоритм",
                    Date = run.CompletedAt!.Value,
                    Status = "Проверено",
                    IsSolved = true,
                    CanOpen = false
                })
                .ToArray();

            return Ok(history);
        }

        [Authorize(Roles = "Administrator"), HttpGet("TaskHistory/All")]
        public async Task<ActionResult<TeacherTaskHistoryDTO[]>> GetAllTaskHistory()
        {
            var analysisRuns = await _context.AnalysisRuns
                .AsNoTracking()
                .Where(run =>
                    run.StatusId == LookupIds.AnalysisStatusId("Completed") &&
                    run.CompletedAt.HasValue)
                .OrderByDescending(run => run.CompletedAt)
                .ThenByDescending(run => run.Id)
                .ToListAsync();

            var userIds = analysisRuns
                .Select(run => run.UserId)
                .Distinct()
                .ToList();

            var users = await _context.Users
                .AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id);

            var userGroups = await _context.UserGroups
                .AsNoTracking()
                .Include(userGroup => userGroup.Group)
                .Where(userGroup => userIds.Contains(userGroup.UserId))
                .ToListAsync();

            var groupByUserId = userGroups
                .GroupBy(userGroup => userGroup.UserId)
                .ToDictionary(group => group.Key, group => group.First());

            var history = analysisRuns
                .Select(run =>
                {
                    users.TryGetValue(run.UserId, out var user);
                    groupByUserId.TryGetValue(run.UserId, out var userGroup);

                    var fullName = user == null
                        ? run.UserId
                        : string.Join(" ", new[] { user.SecondName, user.Name, user.Patronymic }
                            .Where(part => !string.IsNullOrWhiteSpace(part)));

                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        fullName = user?.UserName ?? run.UserId;
                    }

                    return new TeacherTaskHistoryDTO
                    {
                        Id = run.Id,
                        TaskId = run.TaskTypeId == LookupIds.TaskTypeId("AlphaBeta") ? run.AlphaBetaId : run.FifteenPuzzleId,
                        TaskType = run.TaskTypeId == LookupIds.TaskTypeId("FifteenPuzzle") ? "a-star" : "min-max",
                        TaskName = run.TaskTypeId == LookupIds.TaskTypeId("FifteenPuzzle") ? "Пятнашки A*" : "min-max алгоритм",
                        UserId = run.UserId,
                        UserName = fullName,
                        GroupId = userGroup?.GroupId,
                        GroupName = userGroup?.Group?.Name,
                        Date = run.CompletedAt!.Value,
                        Status = "Проверено",
                        IsSolved = true,
                        CanOpen = false
                    };
                })
                .ToArray();

            return Ok(history);
        }

        [Authorize(Roles = "Administrator"), HttpGet("{userId}")]
        public async Task<ActionResult<UserDTO?>> GetUser(string userId)
        {
            var user = await _usersService.UserLeftJoinGroup(userId, getPfp: true, getUserNames: true).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound();
            }
            return user;
        }

        [EnableRateLimiting("token")]
        [Authorize, HttpPut("")]
        public async Task<ActionResult<IdentityResult>> UpdateUser([FromForm] UserModifyDTO userNewData, string? userId)
        {
            var reqUser = await _userManager.GetUserAsync(User);
            if (reqUser == null)
            {
                return Unauthorized();
            }

            var roles = await _userManager.GetRolesAsync(reqUser);
            if (roles.FirstOrDefault(r => r == "Administrator") == null)
            {
                userId = reqUser.Id;
            }
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest();
            }

            var user = await _context.Users.FirstOrDefaultAsync(f => f.Id == userId);
            if (user == null)
            {
                return NotFound();
            }
            if (userNewData.Pfp != null)
            {
                user.PfpPath = await _usersService.UploadPfp(userId, userNewData.Pfp);
            }
            if (userNewData.RemoveGroup == true)
            {
                await _context.UserGroups.Where(ug => ug.UserId == userId).ExecuteDeleteAsync();
            }
            else if (userNewData.GroupId != null && await _context.Groups.FirstOrDefaultAsync(g => g.Id == userNewData.GroupId) != null)
            {
                await _context.UserGroups.Where(ug => ug.UserId == userId).ExecuteDeleteAsync();
                _context.UserGroups.Add(new UserGroups() { UserId = userId, GroupId = (int)userNewData.GroupId });
            }
            if (userNewData.Name != null)
            {
                user.Name = userNewData.Name;
            }
            if (userNewData.SecondName != null)
            {
                user.SecondName = userNewData.SecondName;
            }
            if (userNewData.Patronymic != null)
            {
                user.Patronymic = userNewData.Patronymic == "-" ? null : userNewData.Patronymic;   
            }
            await _context.SaveChangesAsync();
            if (userNewData.UserName != null)
            {
                var result = await _userManager.SetUserNameAsync(user, userNewData.UserName);
                if (!result.Succeeded)
                {
                    return result;
                }
            }
            if (userNewData.Password != null)
            {
                string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                IdentityResult result = await _userManager.ResetPasswordAsync(user, resetToken, userNewData.Password);
                if (!result.Succeeded)
                {
                    return result;
                }
            }
            return Ok();
        }

        [Authorize(Roles = "Administrator"), HttpDelete()]
        public async Task<ActionResult> DeleteUser([FromQuery] string? userId, [FromQuery] int? groupId, [FromQuery] string[]? userIds)
        {
            if (groupId != null)
            {
                if (await _context.Groups.FirstOrDefaultAsync(g => g.Id == groupId) != null)
                {
                    var ids = await _context.UserGroups.Where(g => g.GroupId == groupId).Select(ug => ug.UserId).ToArrayAsync();
                    foreach (var id in ids)
                    {
                        await _context.Users.Where(u => u.Id == id).ExecuteDeleteAsync();
                    }
                }
            }
            if (userId != null)
            {
                var user = await _context.Users.FirstOrDefaultAsync(f => f.Id == userId);
                if (user != null)
                {
                    _context.Users.Remove(user);
                }
            }
            if (userIds != null)
            {
                foreach (var id in userIds)
                {
                    await _context.Users.Where(u => u.Id == id).ExecuteDeleteAsync();
                }
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = "Administrator"), HttpGet("Groups")]
        public async Task<ActionResult<Group[]>> GetGroups()
        {
            return await _context.Groups.ToArrayAsync();
        }

        [Authorize(Roles = "Administrator"), HttpGet("Groups/{id}/")]
        public async Task<ActionResult<UserDTO[]>> GetGroup(int id)
        {
            var group = await _context.UserGroups.Where(g => g.GroupId == id).Select(g => new UserDTO
            {
                Id = g.User.Id,
                Name = g.User.Name,
                SecondName = g.User.SecondName,
                Patronymic = g.User.Patronymic,
                GroupId = g.GroupId,
                Group = g.Group.Name
            }).ToArrayAsync();
            if (group.IsNullOrEmpty())
            {
                return NotFound();
            }
            return group;
        }

        [Authorize(Roles = "Administrator"), HttpPost("Groups")]
        public async Task<ActionResult> AddGroup(string groupName)
        {
            _context.Groups.Add(new Group { Name = groupName });
            await _context.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = "Administrator"), HttpPut("Groups/{id}")]
        public async Task<ActionResult> ChangeGroup(int id, string[] userIds)
        {
            if (await _context.Groups.FirstOrDefaultAsync(g => g.Id == id) == null)
            {
                return NotFound();
            }
            foreach (var userId in userIds)
            {
                if (await _context.Users.FirstOrDefaultAsync(u => u.Id == userId) == null)
                {
                    continue;
                }
                await _context.UserGroups.Where(ug => ug.UserId == userId).ExecuteDeleteAsync();
                _context.UserGroups.Add(new UserGroups { UserId = userId, GroupId = id });
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = "Administrator"), HttpDelete("Groups")]
        public async Task<ActionResult> DeleteGroup([FromQuery] int? id, [FromQuery] int[]? ids)
        {
            if (ids != null)
            {
                foreach (var groupId in ids)
                {
                    await _context.Groups.Where(g => g.Id == groupId).ExecuteDeleteAsync();
                }
            }
            await _context.Groups.Where(g => g.Id == id).ExecuteDeleteAsync();
            await _context.SaveChangesAsync();
            return Ok();
        }
        
        private static ValidationProblem CreateValidationProblem(IdentityResult result)
        {
            // We expect a single error code and description in the normal case.
            // This could be golfed with GroupBy and ToDictionary, but perf! :P
            Debug.Assert(!result.Succeeded);
            var errorDictionary = new Dictionary<string, string[]>(1);

            foreach (var error in result.Errors)
            {
                string[] newDescriptions;

                if (errorDictionary.TryGetValue(error.Code, out var descriptions))
                {
                    newDescriptions = new string[descriptions.Length + 1];
                    Array.Copy(descriptions, newDescriptions, descriptions.Length);
                    newDescriptions[descriptions.Length] = error.Description;
                }
                else
                {
                    newDescriptions = [error.Description];
                }

                errorDictionary[error.Code] = newDescriptions;
            }

            return TypedResults.ValidationProblem(errorDictionary);
        }

        [Authorize(Roles = "Administrator"), HttpPost("Register")]
        public async Task<Results<Ok, ValidationProblem>> RegisterUser(RegReq registration)
        {
            var userName = registration.UserName;

            if (string.IsNullOrEmpty(userName))
            {
                return CreateValidationProblem(IdentityResult.Failed(_userManager.ErrorDescriber.InvalidUserName(userName)));
            }

            var user = new ApplicationUser();
            await _userStore.SetUserNameAsync(user, userName, CancellationToken.None);
            user.Name = registration.Name;
            user.SecondName = registration.SecondName;
            user.Patronymic = registration.Patronymic;
            var result = await _userManager.CreateAsync(user, registration.Password);

            if (!result.Succeeded)
            {
                return CreateValidationProblem(result);
            }

            if (registration.GroupId != null)
            {
                var userId = await _context.Users.FirstAsync(u => u.UserName == userName);
                _context.UserGroups.Add(new UserGroups { UserId = userId.Id, GroupId = (int)registration.GroupId });
                await _context.SaveChangesAsync();
            }

            return TypedResults.Ok();
        }

        [EnableRateLimiting("token")]
        [HttpPost("Login")]
        public async Task<Results<Ok<AccessTokenResponse>, EmptyHttpResult, ProblemHttpResult>> LoginUser(LogReq login, [FromQuery] bool? useCookies, [FromQuery] bool? useSessionCookies)
        {
            var useCookieScheme = useCookies == true || useSessionCookies == true;
            var isPersistent = useCookies == true && useSessionCookies != true;
            _signInManager.AuthenticationScheme = useCookieScheme ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;

            var result = await _signInManager.PasswordSignInAsync(login.UserName, login.Password, isPersistent, lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                return TypedResults.Problem(result.ToString(), statusCode: StatusCodes.Status401Unauthorized);
            }

            return TypedResults.Empty;
        }

        [EnableRateLimiting("token")]
        [HttpPost("Refresh")]
        public async Task<Results<Ok<AccessTokenResponse>, UnauthorizedHttpResult, SignInHttpResult, ChallengeHttpResult>> RefreshToken([FromBody] RefreshRequest refreshRequest)
        {

            var refreshTokenProtector = _bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
            var refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);
            // Reject the /refresh attempt with a 401 if the token expired or the security stamp validation fails
            if (refreshTicket?.Properties?.ExpiresUtc is not { } expiresUtc ||
                _timeProvider.GetUtcNow() >= expiresUtc ||
                await _signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not ApplicationUser user)
            {
                return TypedResults.Challenge();
            }

            var newPrincipal = await _signInManager.CreateUserPrincipalAsync(user);
            return TypedResults.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
        }

        [EnableRateLimiting("token")]
        [Authorize, HttpPost("Logout")]
        public async Task<ActionResult> LogoutUser()
        {
            await _signInManager.SignOutAsync();
            return Ok();
        }
    }
}
