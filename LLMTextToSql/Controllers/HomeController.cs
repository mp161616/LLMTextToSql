// Controllers/HomeController.cs
using Microsoft.AspNetCore.Mvc;
using LLMTextToSql.Interfaces;
using LLMTextToSql.Interfaces.Agents;
using LLMTextToSql.Interfaces.Services;
using System.Threading.Tasks;

namespace LLMTextToSql.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILlmService _llmService;
        private readonly IPythonDecomposerAgent _decomposer;
        private readonly IPythonRefinerAgent _refinerAgent;

        public HomeController(
            ILlmService llmService,
            IPythonDecomposerAgent decomposer,
            IPythonRefinerAgent refinerAgent)
        {
            _llmService = llmService;
            _decomposer = decomposer;
            _refinerAgent = refinerAgent;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string inputValue, bool useDecomposer = false)
        {
            if (string.IsNullOrWhiteSpace(inputValue))
            {
                ViewBag.Error = "Please enter a question.";
                return View();
            }
            string initialSql = useDecomposer
                ? await _decomposer.DecomposeAndGenerateFinalSqlAsync(inputValue)
                : await _llmService.GenerateSqlAsync(inputValue);

            string finalSql = await _refinerAgent
                .RefineAndGenerateSqlAsync(initialSql, errorMessage: "", question: inputValue);

            ViewBag.Prompt = inputValue;
            ViewBag.UseDecomposer = useDecomposer;
            ViewBag.FinalSql = finalSql;
            return View();
        }
    }
}

