using System.Diagnostics;
using LLMTextToSql.Models;
using Microsoft.AspNetCore.Mvc;


using LLMTextToSql.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LLMTextToSql.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILlmService _llmService;

        public HomeController(ILlmService llmService)
        {
            _llmService = llmService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string inputValue)
        {
            var sqlResult = await _llmService.GetSqlFromPrompt(inputValue);
            ViewBag.Result = sqlResult;
            return View();
        }
    }
}

