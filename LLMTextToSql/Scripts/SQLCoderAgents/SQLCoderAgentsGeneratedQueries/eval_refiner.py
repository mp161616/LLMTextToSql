import json
import subprocess
import os

# Load Paths from configuration
def load_settings(path="../appsettings.json"):
    with open(path, "r", encoding="utf-8") as f:
        config = json.load(f)
    return config["Paths"]["SqlCoderSelectorGeneratedQueries"], config["Paths"]["SqlCoderRefinerGeneratedQueries"]

selector_results, refiner_results = load_settings()
EVIDENCE_FILE = "Config/config.json"
SELECTOR_RESULTS_FILE = selector_results
REFINER_RESULTS_FILE = refiner_results

SCHEMA_PATH = "../Schemas/card_games_schema.json"
DSN = "host=localhost port=5432 dbname=card_games user=postgres password=admin"

with open(EVIDENCE_FILE, "r", encoding="utf-8") as f:
    evidence_data = json.load(f)

evidence_map = {item["question"]: item["evidence"] for item in evidence_data}

with open(SELECTOR_RESULTS_FILE, "r", encoding="utf-8") as f:
    selector_results = json.load(f)

results = []

for test in selector_results:
    question = test["question"]
    expected_sql = test["expected_sql"]
    selector_output = test["selector_output"]

    evidence = evidence_map.get(question, "")

    try:
        full_output = subprocess.check_output(
           [
              "python", "SqlCoderAgents/Agents/refiner.py",
              selector_output,
              "[SQL Error] No SQL" if "Error" in selector_output else "",
              question,
              evidence,
              SCHEMA_PATH,
              DSN
          ],
          stderr=subprocess.STDOUT,
          text=True,
          timeout=300
        ).strip()

         # Get the last non-empty line (SqlCoder's final SQL output)
        lines = full_output.splitlines()
        output = next((line for line in reversed(lines) if line.strip() and not line.startswith("[DEBUG]")), "[Error] No output")

    except subprocess.CalledProcessError as e:
        output = f"[Error] {e.output.strip()}"
    except subprocess.TimeoutExpired:
        output = "[Timeout]"

    results.append({
        "question": question,
        "expected_sql": expected_sql,
        "refiner_output": output
    })

os.makedirs("output", exist_ok=True)
with open(REFINER_RESULTS_FILE, "w", encoding="utf-8") as f:
    json.dump(results, f, indent=2, ensure_ascii=False)

print(f"Refiner evaluation complete. Check {REFINER_RESULTS_FILE}")

