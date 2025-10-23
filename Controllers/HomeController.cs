//Controla a página principal do site. Exibe o menu inicial e chama os modais de login, cadastro e agendamento.

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AgendaTatiNails.Models;

namespace AgendaTatiNails.Controllers;

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
