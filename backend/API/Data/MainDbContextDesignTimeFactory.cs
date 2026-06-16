using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AICourseTester.Data
{
	public class MainDbContextDesignTimeFactory : IDesignTimeDbContextFactory<MainDbContext>
	{
		public MainDbContext CreateDbContext(string[] args)
		{
			var optionsBuilder = new DbContextOptionsBuilder<MainDbContext>();
			var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
				?? "Host=localhost;Port=5432;Database=main_db;Username=postgres;Password=postgres";

			optionsBuilder.UseNpgsql(connectionString);

			return new MainDbContext(optionsBuilder.Options);
		}
	}
}
