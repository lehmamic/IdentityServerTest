using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diskordia.TestApi.Controllers
{
  [Route("[controller]")]
  [Authorize]
  public class IdentityController : ControllerBase
  {
    [HttpGet]
    public IActionResult Get()
    {
      return new JsonResult(from c in this.User.Claims select new { c.Type, c.Value });
    }
  }
}