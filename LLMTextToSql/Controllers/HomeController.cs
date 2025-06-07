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
        private readonly IIterativeRefinerService _iterativeRefiner;

        public HomeController(
            ILlmService llmService,
            IPythonDecomposerAgent decomposer,
            IIterativeRefinerService iterativeRefiner)
        {
            _llmService = llmService;
            _decomposer = decomposer;
            _iterativeRefiner = iterativeRefiner;
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

            // 1) Get the initial SQL from either the Decomposer or the direct LLM pipeline:
            string initialSql = useDecomposer
                ? await _decomposer.DecomposeAndGenerateFinalSqlAsync(inputValue)
                : await _llmService.GenerateSqlAsync(inputValue);

            // 2) Pass that initialSql to the Iterative Refiner
            //    We'll let it try up to 3 times (for example)
            string finalSql = await _iterativeRefiner.RefineUntilExecutableAsync(initialSql, inputValue, maxAttempts: 3);

            ViewBag.Prompt = inputValue;
            ViewBag.UseDecomposer = useDecomposer;
            ViewBag.FinalSql = finalSql;
            return View();
        }
    }
}

