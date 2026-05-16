using IfsaKlasik.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IfsaKlasik.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IfsaRoles.Admin)]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
