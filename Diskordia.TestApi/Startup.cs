using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diskordia.TestApi
{
  public class Startup
  {
    public Startup(IHostingEnvironment env)
    {
      var builder = new ConfigurationBuilder()
        .SetBasePath(env.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

      builder.AddEnvironmentVariables();
      this.Configuration = builder.Build();
    }

    public IConfigurationRoot Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
      services.AddCors(options=>
      {
        // this defines a CORS policy called "default"
        options.AddPolicy("default", policy =>
        {
          policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
      });

      services.AddMvcCore()
        .AddAuthorization()
        .AddJsonFormatters();
    }

    public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
    {
      loggerFactory.AddConsole(this.Configuration.GetSection("Logging"));
      loggerFactory.AddDebug();

      // this uses the policy called "default"
      app.UseCors("default");

      app.UseIdentityServerAuthentication(new IdentityServerAuthenticationOptions
      {
        Authority = "http://localhost:5000",
        RequireHttpsMetadata = false,

        ApiName = "api1"
      });

      app.UseMvc();
    }
  }
}
