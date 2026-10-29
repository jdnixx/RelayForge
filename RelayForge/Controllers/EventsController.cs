using Microsoft.AspNetCore.Mvc;

namespace RelayForge.Controllers;

public class EventsController : Controller
{
    // GET
    public IActionResult Index()
    {
        return View();
    }
}