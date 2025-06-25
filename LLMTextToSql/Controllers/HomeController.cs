using Microsoft.AspNetCore.Mvc;
using LLMTextToSql.Interfaces.Agents;
using LLMTextToSql.Interfaces.Services;
using System.Text.RegularExpressions;
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
        public async Task<IActionResult> Index(string inputValue, bool useDecomposer)
        {
            if (string.IsNullOrWhiteSpace(inputValue))
            {
                ViewBag.Error = "Please enter a question.";
                return View();
            }

            string chain = null;
            string sqlToRefine;

            if (useDecomposer)
            {
                // get the full chain-of-thought
                chain = await _decomposer.DecomposeChainOfThoughtAsync(inputValue);

                //  pull out the Final SQL so the refiner still works
                var m = Regex.Match(chain, @"Final SQL:\s*(.+)", RegexOptions.Singleline);
                sqlToRefine = m.Success
                    ? m.Groups[1].Value.Trim()
                    : chain;
            }
            else
            {
                sqlToRefine = await _llmService.GenerateSqlAsync(inputValue);
            }

            //  refine / polish the SQL
            string finalSql = await _refinerAgent
                .RefineAndGenerateSqlAsync(sqlToRefine, errorMessage: "", question: inputValue);

            // push everything into the ViewBag
            ViewBag.Prompt = inputValue;
            ViewBag.UseDecomposer = useDecomposer;
            ViewBag.ChainOfThought = chain;
            ViewBag.FinalSql = finalSql;

            return View();
        }


    }
}