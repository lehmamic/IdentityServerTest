using System;
using System.Linq;
using System.Threading.Tasks;
using Diskordia.IdentityServer.Controllers;
using Diskordia.IdentityServer.Models.Account;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using IdentityServer4.Test;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Diskordia.IdentityServer.Api
{
  [Route("api/[controller]")]
  public class AccountController : Controller
  {
    private readonly TestUserStore users;
    private readonly IIdentityServerInteractionService interaction;
    private readonly AccountService account;

    public AccountController(
      IIdentityServerInteractionService interaction,
      IClientStore clientStore,
      IHttpContextAccessor httpContextAccessor,
      TestUserStore users = null)
    {
      // if the TestUserStore is not in DI, then we'll just use the global users collection
      this.users = users ?? new TestUserStore(TestUsers.Users);
      this.interaction = interaction;
      this.account = new AccountService(interaction, httpContextAccessor, clientStore);
    }

    [HttpGet("login")]
    public async Task<IActionResult> Login([FromQuery]string returnUrl)
    {
      var vm = await this.account.BuildLoginViewModelAsync(returnUrl);

//      if (vm.IsExternalLoginOnly)
//      {
//        // only one option for logging in
//        return await this.ExternalLogin(vm.ExternalProviders.First().AuthenticationScheme, returnUrl);
//      }

      return this.Ok(vm);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginInputModel model)
    {
      if (this.ModelState.IsValid)
      {
        // validate username/password against in-memory store
        if (this.users.ValidateCredentials(model.Username, model.Password))
        {
          AuthenticationProperties props = null;

          // only set explicit expiration here if persistent.
          // otherwise we reply upon expiration configured in cookie middleware.
          if (AccountOptions.AllowRememberLogin && model.RememberLogin)
          {
            props = new AuthenticationProperties
            {
              IsPersistent = true,
              ExpiresUtc = DateTimeOffset.UtcNow.Add(AccountOptions.RememberMeLoginDuration)
            };
          }

          // issue authentication cookie with subject ID and username
          var user = this.users.FindByUsername(model.Username);
          await this.HttpContext.Authentication.SignInAsync(user.SubjectId, user.Username, props);

          // make sure the returnUrl is still valid, and if yes - redirect back to authorize endpoint
          if (this.interaction.IsValidReturnUrl(model.ReturnUrl))
          {
            return this.Redirect(model.ReturnUrl);
          }

          return this.Redirect("~/");
        }

        this.ModelState.AddModelError("", AccountOptions.InvalidCredentialsErrorMessage);
      }

      // something went wrong, show form with error
      var vm = await this.account.BuildLoginViewModelAsync(model);

      return this.View(vm);
    }
  }
}