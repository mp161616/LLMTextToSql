import json
import subprocess
import os
import re

# Load Paths from configuration
def load_settings(path="../appsettings.json"):
    with open(path, "r", encoding="utf-8") as f:
        config = json.load(f)
    return config["Paths"]["ClaudeSelectorGeneratedQueries"], config["Paths"]["ClaudeRefinerGeneratedQueries"]

selector_results, refiner_results = load_settings()

EVIDENCE_FILE = "Config/config_european_football.json"
SELECTOR_RESULTS_FILE = selector_results
REFINER_RESULTS_FILE = refiner_results

SCHEMA_PATH = "../Schemas/european_football_schema.json"
DSN = "host=localhost port=5432 dbname=european_football user=postgres password=admin"

with open(EVIDENCE_FILE, "r", encoding="utf-8") as f:
    evidence_data = json.load(f)

evidence_map = {item["question"]: item["evidence"] for item in evidence_data}

with open(SELECTOR_RESULTS_FILE, "r", encoding="utf-8") as f:
    selector_results = json.load(f)

def normalize_sql(sql: str) -> str:
    return re.sub(r'\s+', ' ', sql.strip().lower()).rstrip(';')

results = []

for test in selector_results:
    question = test["question"]
    expected_sql = test["expected_sql"]
    selector_output = test["selector_output"]
    difficulty = test["difficulty"]

    evidence = evidence_map.get(question, "")

    if normalize_sql(expected_sql) == normalize_sql(selector_output):
        print(f"Skipping refiner for identical SQL (question: {question[:60]}...)")
        results.append({
            "question": question,
            "expected_sql": expected_sql,
            "refiner_output": selector_output,
            "difficulty": difficulty,
        })
        continue

    try:
        full_output = subprocess.check_output(
           [
              "python", "ClaudeAgents/Agents/refiner.py",
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

         # Get the last non-empty line (Claude's final SQL output)
        lines = full_output.splitlines()
        output = next((line for line in reversed(lines) if line.strip() and not line.startswith("[DEBUG]")), "[Error] No output")

    except subprocess.CalledProcessError as e:
        output = f"[Error] {e.output.strip()}"
    except subprocess.TimeoutExpired:
        output = "[Timeout]"

    results.append({
        "question": question,
        "expected_sql": expected_sql,
        "refiner_output": output,
        "difficulty" : difficulty
    })

os.makedirs("output", exist_ok=True)
with open(REFINER_RESULTS_FILE, "w", encoding="utf-8") as f:
    json.dump(results, f, indent=2, ensure_ascii=False)

print(f"Refiner evaluation complete. Check {REFINER_RESULTS_FILE}")

