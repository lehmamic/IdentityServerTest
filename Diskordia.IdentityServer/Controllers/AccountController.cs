using IdentityModel;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using IdentityServer4.Test;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Diskordia.IdentityServer.Filters;
using Diskordia.IdentityServer.Models.Account;
using IdentityServer4;
using Microsoft.AspNetCore.Authentication;

namespace Diskordia.IdentityServer.Controllers
{
  [SecurityHeaders]
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

    /// <summary>
    /// Show login page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Login(string returnUrl)
    {
      var vm = await this.account.BuildLoginViewModelAsync(returnUrl);

      if (vm.IsExternalLoginOnly)
      {
        // only one option for logging in
        return await this.ExternalLogin(vm.ExternalProviders.First().AuthenticationScheme, returnUrl);
      }

      return this.View(vm);
    }

    /// <summary>
    /// Handle postback from username/password login
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
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

    /// <summary>
    /// Show logout page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Logout(string logoutId)
    {
      var vm = await this.account.BuildLogoutViewModelAsync(logoutId);

      if (vm.ShowLogoutPrompt == false)
      {
        // no need to show prompt
        return await this.Logout(vm);
      }

      return this.View(vm);
    }

    /// <summary>
    /// Handle logout page postback
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(LogoutInputModel model)
    {
      var vm = await this.account.BuildLoggedOutViewModelAsync(model.LogoutId);
      if (vm.TriggerExternalSignout)
      {
        string url = this.Url.Action("Logout", new { logoutId = vm.LogoutId });
        try
        {
          // hack: try/catch to handle social providers that throw
          await this.HttpContext.Authentication.SignOutAsync(vm.ExternalAuthenticationScheme,
            new AuthenticationProperties { RedirectUri = url });
        }
        catch(NotSupportedException) // this is for the external providers that don't have signout
        {
        }
        catch(InvalidOperationException) // this is for Windows/Negotiate
        {
        }
      }

      // delete local authentication cookie
      await this.HttpContext.Authentication.SignOutAsync();

      return this.View("LoggedOut", vm);
    }

    /// <summary>
    /// initiate roundtrip to external authentication provider
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExternalLogin(string provider, string returnUrl)
    {
      returnUrl = this.Url.Action("ExternalLoginCallback", new { returnUrl = returnUrl });

      // windows authentication is modeled as external in the asp.net core authentication manager, so we need special handling
      if (AccountOptions.WindowsAuthenticationSchemes.Contains(provider))
      {
        // but they don't support the redirect uri, so this URL is re-triggered when we call challenge
        if (this.HttpContext.User is WindowsPrincipal)
        {
          var props = new AuthenticationProperties();
          props.Items.Add("scheme", this.HttpContext.User.Identity.AuthenticationType);

          var id = new ClaimsIdentity(provider);
          id.AddClaim(new Claim(ClaimTypes.NameIdentifier, this.HttpContext.User.Identity.Name));
          id.AddClaim(new Claim(ClaimTypes.Name, this.HttpContext.User.Identity.Name));

          await this.HttpContext.Authentication.SignInAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme, new ClaimsPrincipal(id), props);
          return this.Redirect(returnUrl);
        }
        else
        {
          // this triggers all of the windows auth schemes we're supporting so the browser can use what it supports
          return new ChallengeResult(AccountOptions.WindowsAuthenticationSchemes);
        }
      }
      else
      {
        // start challenge and roundtrip the return URL
        var props = new AuthenticationProperties
        {
          RedirectUri = returnUrl,
          Items = { { "scheme", provider } }
        };
        return new ChallengeResult(provider, props);
      }
    }

    /// <summary>
    /// Post processing of external authentication
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string returnUrl)
    {
      // read external identity from the temporary cookie
      var info = await this.HttpContext.Authentication.GetAuthenticateInfoAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
      var tempUser = info?.Principal;
      if (tempUser == null)
      {
        throw new Exception("External authentication error");
      }

      // retrieve claims of the external user
      var claims = tempUser.Claims.ToList();

      // try to determine the unique id of the external user - the most common claim type for that are the sub claim and the NameIdentifier
      // depending on the external provider, some other claim type might be used
      var userIdClaim = claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Subject);
      if (userIdClaim == null)
      {
        userIdClaim = claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
      }
      if (userIdClaim == null)
      {
        throw new Exception("Unknown userid");
      }

      // remove the user id claim from the claims collection and move to the userId property
      // also set the name of the external authentication provider
      claims.Remove(userIdClaim);
      var provider = info.Properties.Items["scheme"];
      var userId = userIdClaim.Value;

      // check if the external user is already provisioned
      var user = this.users.FindByExternalProvider(provider, userId);
      if (user == null)
      {
        // this sample simply auto-provisions new external user
        // another common approach is to start a registrations workflow first
        user = this.users.AutoProvisionUser(provider, userId, claims);
      }

      var additionalClaims = new List<Claim>();

      // if the external system sent a session id claim, copy it over
      var sid = claims.FirstOrDefault(x => x.Type == JwtClaimTypes.SessionId);
      if (sid != null)
      {
        additionalClaims.Add(new Claim(JwtClaimTypes.SessionId, sid.Value));
      }

      // if the external provider issued an id_token, we'll keep it for signout
      AuthenticationProperties props = null;
      var idToken = info.Properties.GetTokenValue("id_token");
      if (idToken != null)
      {
        props = new AuthenticationProperties();
        props.StoreTokens(new[] { new AuthenticationToken { Name = "id_token", Value = idToken } });
      }

      // issue authentication cookie for user
      await this.HttpContext.Authentication.SignInAsync(user.SubjectId, user.Username,  provider, props, additionalClaims.ToArray());

      // delete temporary cookie used during external authentication
      await this.HttpContext.Authentication.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

      // validate return URL and redirect back to authorization endpoint
      if (this.interaction.IsValidReturnUrl(returnUrl))
      {
        return this.Redirect(returnUrl);
      }

      return this.Redirect("~/");
    }
  }
}