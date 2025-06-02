using System.Diagnostics;
using LLMTextToSql.Models;
using Microsoft.AspNetCore.Mvc;

namespace LLMTextToSql.Controllers
{
    using Microsoft.AspNetCore.Mvc;

    namespace ConvertApp.Controllers
    {
        public class HomeController : Controller
        {
            [HttpGet]
            public IActionResult Index()
            {
                return View();
            }

            [HttpPost]
            public IActionResult Index(string inputValue)
            {
                ViewBag.Result = $"You entered: {inputValue}";
                return View();
            }
        }
    }

}
