using AICourseTester.Data;
using AICourseTester.Models;
using AICourseTester.Services;
using AICourseTester.Services.Analysis;
using AICourseTester.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SixLabors.ImageSharp.Web.DependencyInjection;
using System.Resources;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          if (builder.Environment.IsDevelopment())
                          {
                              policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                          }
                          else
                          {
                              var front_url = Environment.GetEnvironmentVariable("FRONTEND_URL");
                              if (front_url == null)
                              {
                                  policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                              }
                              else
                              {
                                  policy
                                  //.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback)
                                  .WithOrigins(front_url)
                                  .AllowAnyHeader().AllowAnyMethod();
                              }
                          }
                      });
});

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "AICourseTester API",
		Version = "v1"
	});

	options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
	{
		Name = "Authorization",
		Type = SecuritySchemeType.Http,
		Scheme = "bearer",
		BearerFormat = "JWT",
		In = ParameterLocation.Header,
		Description = "¬ведите токен в формате: Bearer {token}"
	});

	options.AddSecurityRequirement(new OpenApiSecurityRequirement
	{
		{
			new OpenApiSecurityScheme
			{
				Reference = new OpenApiReference
				{
					Type = ReferenceType.SecurityScheme,
					Id = "Bearer"
				}
			},
			new string[] {}
		}
	});
});


string connString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? builder.Configuration.GetConnectionString("main_db");
builder.Services.AddDbContext<MainDbContext>(options =>
{
    options
    // TODO: FIGURE OUT THE WARNING
    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
    .UseNpgsql(connString);
});
builder.Services.AddTransient<UsersService>();

builder.Services
    .AddIdentityApiEndpoints<ApplicationUser>(identityOptions =>
    {
        identityOptions.Lockout.MaxFailedAccessAttempts = 10;
        identityOptions.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
        identityOptions.Lockout.AllowedForNewUsers = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MainDbContext>();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddImageSharp();

builder.Services.Configure<IdentityOptions>(options =>
{
    // Password settings.
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    //// Lockout settings.
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings.
    options.User.AllowedUserNameCharacters =
    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._+!@#$%^&*";
});


var tokenPolicy = "token";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(tokenPolicy, httpContext =>
    RateLimitPartition.GetTokenBucketLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        //partitionKey: httpContext.User.Identity?.Name ?? "anonymous",
        factory: _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 150,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            ReplenishmentPeriod = TimeSpan.FromSeconds(60),
            TokensPerPeriod = 50,
            AutoReplenishment = true
        })
    );
});

builder.Services.AddScoped<ITaskAnalysisPipelineService, TaskAnalysisPipelineService>();
builder.Services.AddScoped<IErrorCausalityBuilder, ErrorCausalityBuilder>();
builder.Services.AddScoped<IAlphaBetaErrorAnalysisService, AlphaBetaErrorAnalysisService>();
builder.Services.AddScoped<IFifteenPuzzleErrorAnalysisService, FifteenPuzzleErrorAnalysisService>();
builder.Services.AddScoped<IErrorClassificationService, ErrorClassificationService>();
builder.Services.AddScoped<IKnowledgeGapDetectionService, KnowledgeGapDetectionService>();

var app = builder.Build();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.MapIdentityApi<ApplicationUser>();

app.UseHttpsRedirection();

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthentication();
app.UseAuthorization();

app.UseImageSharp();

app.UseStaticFiles();

app.MapControllers();

app.UseRateLimiter();

using (var scope = app.Services.CreateScope())
{
    if (app.Environment.IsDevelopment())
    {
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            await context.Database.MigrateAsync();
        }
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new string[] { "Administrator", "Student" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }



    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var userStore = scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
    var ctx = scope.ServiceProvider.GetRequiredService<MainDbContext>();

    string? userName = Environment.GetEnvironmentVariable("ADMIN_USERNAME") ?? builder.Configuration["Admin:UserName"] ?? "admin";
    var password = builder.Configuration["Admin:Password"] ?? builder.Configuration["Admin:Pw"] ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
    if (password == null)
    {
        throw new Exception("Provide password for admin");
    }

    ApplicationUser? user = await userManager.FindByNameAsync(userName);
	if (user != null)
	{
		if (!await userManager.CheckPasswordAsync(user, password))
		{
			string resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
			var result = await userManager.ResetPasswordAsync(user, resetToken, password);
			if (!result.Succeeded)
			{
				throw new Exception(string.Join("\n", result.Errors));
			}
		}

		if (!await userManager.IsInRoleAsync(user, "Administrator"))
		{
			await userManager.AddToRoleAsync(user, "Administrator");
		}
	}
	else
	{
		user = new ApplicationUser();
		await userStore.SetUserNameAsync(user, userName, CancellationToken.None);

		var createResult = await userManager.CreateAsync(user, password);
		if (!createResult.Succeeded)
		{
			throw new Exception(string.Join("\n", createResult.Errors.Select(e => e.Description)));
		}

		var roleResult = await userManager.AddToRoleAsync(user, "Administrator");
		if (!roleResult.Succeeded)
		{
			throw new Exception(string.Join("\n", roleResult.Errors.Select(e => e.Description)));
		}
	}

}

var url = Environment.GetEnvironmentVariable("LISTEN_ON");
var (http, https) = (Environment.GetEnvironmentVariable("HTTP_PORT"), Environment.GetEnvironmentVariable("HTTPS_PORT"));
if (url != null)
{
    if (url.Contains("http:") || url.Contains("https:"))
    {
        app.Urls.Add(url);
    }
    else
    {
        if (http != null)
        {
            app.Urls.Add($"http://{url}:{http}");
        }
        if (https != null)
        {
            app.Urls.Add($"https://{url}:{https}");
        }
    }
}
app.MapFallbackToFile("index.html");
app.Run();
