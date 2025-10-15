import json
import subprocess
import os

# Load Paths from configuration
def load_settings(path="../appsettings.json"):
    with open(path, "r", encoding="utf-8") as f:
        config = json.load(f)
    return config["Paths"]["ClaudeSelectorGeneratedQueries"]

with open("Config/config_european_football.json", "r", encoding="utf-8") as f:
    test_cases = json.load(f)

results = []

for idx, test in enumerate(test_cases):
    question = test["question"]
    expected_sql = test["SQL"]
    difficulty = test["difficulty"]

    try:
        output = subprocess.check_output(
            ["python", "ClaudeAgents/Agents/selector.py", question],
            stderr=subprocess.STDOUT,
            text=True,
            timeout=300
        ).strip()

        with open("output/selector_raw_output.log", "a", encoding="utf-8") as log_file:
            log_file.write(f"\n\n=== Question {idx+1}: {question} ===\n{output}\n")

        sql_candidates = [line.strip().split("--")[0].strip() for line in output.splitlines()
                  if line.strip().startswith(("SELECT", "WITH", "INSERT", "UPDATE", "DELETE"))]

        if sql_candidates:
            selector_result = sql_candidates[-1]
        else:
            selector_result = "[Error] No SQL found in output"

    except subprocess.CalledProcessError as e:
        selector_result = f"[Error] {e.output.strip()}"
    except subprocess.TimeoutExpired:
        selector_result = "[Timeout]"

    results.append({
        "question": question,
        "expected_sql": expected_sql,
        "selector_output": selector_result,
        "difficulty": difficulty
    })

generated_queries_path = load_settings()

os.makedirs("output", exist_ok=True)
with open(generated_queries_path, "w", encoding="utf-8") as f:
    json.dump(results, f, indent=2, ensure_ascii=False)

print(f"Evaluation complete. Check selector_eval_results.json")

