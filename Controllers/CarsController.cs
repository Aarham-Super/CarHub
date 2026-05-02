using CarHub.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarHub.Controllers;

public sealed class CarsController : Controller
{
    private readonly CarCatalogService _catalog;

    public CarsController(CarCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public IActionResult Details(string brand, string model)
    {
        var car = _catalog.Find(brand, model);

        if (car is null)
        {
            return RedirectToAction(nameof(Index), "Home");
        }

        ViewData["Title"] = car.DisplayName;
        return View($"~/Views/{car.Brand}/{car.Model}.cshtml", car);
    }
}
