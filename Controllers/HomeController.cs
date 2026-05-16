using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using IfsaKlasik.Web.Models;

namespace IfsaKlasik.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult NasilOynanir()
    {
        var tail = Url.Action(nameof(Index));
        if (string.IsNullOrEmpty(tail))
            return RedirectToAction(nameof(Index));
        var path = $"{Request.PathBase}{tail}#nasil";
        return Redirect(path);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
