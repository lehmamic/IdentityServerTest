using System.Threading.Tasks;
using Diskordia.IdentityServer.Filters;
using Diskordia.IdentityServer.Models;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Mvc;

namespace Diskordia.IdentityServer.Controllers
{
  [SecurityHeaders]
  public class HomeController : Controller
  {
    private readonly IIdentityServerInteractionService interaction;

    public HomeController(IIdentityServerInteractionService interaction)
    {
      this.interaction = interaction;
    }

    public IActionResult Index()
    {
      return View();
    }

    /// <summary>
    /// Shows the error page
    /// </summary>
    public async Task<IActionResult> Error(string errorId)
    {
      var vm = new ErrorViewModel();

      // retrieve error details from identityserver
      var message = await this.interaction.GetErrorContextAsync(errorId);
      if (message != null)
      {
        vm.Error = message;
      }

      return this.View("Error", vm);
    }
  }
}