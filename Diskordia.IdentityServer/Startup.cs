using IdentityServer4;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diskordia.IdentityServer
{
  public class Startup
  {
    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
      services.AddMvc();

      services.AddCors(options=>
      {
        // this defines a CORS policy called "default"
        options.AddPolicy("default", policy =>
        {
          // client & identity server ports => require for the redicrect statement in a rest call (e.g. post login).
          policy.WithOrigins("http://localhost:4201", "http://localhost:5000")
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
      });

      // configure identity server with in-memory stores, keys, clients and scopes
      services.AddIdentityServer(options => options.UserInteraction.LoginUrl = "http://localhost:4201/login")
      //  services.AddIdentityServer()
        .AddTemporarySigningCredential()
        .AddInMemoryIdentityResources(Config.GetIdentityResources())
        .AddInMemoryApiResources(Config.GetApiResources())
        .AddInMemoryClients(Config.GetClients())
        .AddTestUsers(Config.GetUsers());
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
    {
      loggerFactory.AddConsole(LogLevel.Debug);
      app.UseDeveloperExceptionPage();

      app.UseCors("default");

      app.UseIdentityServer();

      app.UseGoogleAuthentication(new GoogleOptions
      {
        AuthenticationScheme = "Google",
        DisplayName = "Google",
        SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme,

        ClientId = "434483408261-55tc8n0cs4ff1fe21ea8df2o443v2iuc.apps.googleusercontent.com",
        ClientSecret = "3gcoTrEDPPJ0ukn_aYYT6PWo"
      });

      app.UseStaticFiles();
      app.UseMvcWithDefaultRoute();
    }
  }
}
