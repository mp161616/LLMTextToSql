import json
import psycopg2
import pandas as pd
from pathlib import Path
from typing import Optional, Dict
from loguru import logger
import datetime
import asyncio
from collections import defaultdict

# LOGGING
logger.add("evaluation_debug.log", level="DEBUG", rotation="500 KB")

# DATABASE CONFIG
DB_CONFIG = {
    "HOST": "localhost",
    "PORT": 5432,
    "USER": "postgres",
    "PASSWORD": "admin",  # Change this if needed
    "NAME": "card_games"
}

# PATHS 
EVALUATION_FILE = "refiner_evaluation_results.txt"
RESULTS_PATH = Path("output/refiner_evaluation_results.json")

# DB CONNECTOR 
class PostgreSQLConnector:
    def __init__(self, host, port, user, password, dbname):
        self.conn = psycopg2.connect(
            host=host, port=port, user=user,
            password=password, dbname=dbname
        )

    def execute_query(self, sql_query, return_type="raw"):
        with self.conn.cursor() as cur:
            cur.execute(sql_query)
            rows = cur.fetchall()
            columns = [desc[0] for desc in cur.description]
            return rows if return_type == "raw" else (rows, columns)

# EXECUTION 
def execute_sql_query(db_config: dict, sql_query: str):
    db = PostgreSQLConnector(
        host=db_config["HOST"],
        port=db_config["PORT"],
        user=db_config["USER"],
        password=db_config["PASSWORD"],
        dbname=db_config["NAME"]
    )
    return db.execute_query(sql_query)

# COMPARISON HELPERS
from pandas.testing import assert_frame_equal

from pandas.testing import assert_frame_equal

def compare_pandas_table(pred: pd.DataFrame, gold: pd.DataFrame, ignore_order=False) -> bool:
    try:
        # Drop index & sort column names + values for full match
        pred = pred.reset_index(drop=True)
        gold = gold.reset_index(drop=True)

        if ignore_order:
            pred = pred.sort_index(axis=1).sort_values(by=list(pred.columns)).reset_index(drop=True)
            gold = gold.sort_index(axis=1).sort_values(by=list(gold.columns)).reset_index(drop=True)

        # Force string coercion for robustness
        pred = pred.astype(str)
        gold = gold.astype(str)

        assert_frame_equal(pred, gold, check_dtype=False, check_column_type=False, check_names=False)
        return True
    except AssertionError as e:
        logger.debug(f"assert_frame_equal failed: {e}")
        return False



def coerce_table_to_same_type(pred, gold):
    return pred.astype(str), gold.astype(str)

def check_pandas_table_partial_equality(expected, actual):
    for _, expected_row in expected.iterrows():
        # Align columns before comparing
        expected_row_aligned = expected_row[actual.columns]
        if any((actual == expected_row_aligned).all(axis=1)):
            return True
    return False

# LOGGING OUTPUT
def write_evaluation_results(evaluation_file, question, expected_sql, generated_sql, result, expected_result, is_success, difficulty="unknown"):
    timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    if isinstance(result, str) and result.count('\n') > 10:
        result_lines = result.splitlines()[:10]
        result = '\n'.join(result_lines) + "\n[... truncated ...]"

    if isinstance(expected_result, str) and expected_result.count('\n') > 10:
        expected_lines = expected_result.splitlines()[:10]
        expected_result = '\n'.join(expected_lines) + "\n[... truncated ...]"

    with open(evaluation_file, 'a', encoding='utf-8') as f:
        f.write(f"\n{'=' * 80}\n")
        f.write(f"EVALUATION RUN: {timestamp}\n")
        f.write(f"{'=' * 80}\n\n")
        f.write(f"QUESTION: {question}\n\n")
        f.write(f"DIFFICULTY: {difficulty}\n\n")
        f.write(f"STATUS: {'SUCCESS' if is_success else 'FAILURE'}\n\n")
        f.write(f"EXPECTED SQL:\n{expected_sql}\n\n")
        f.write(f"GENERATED SQL:\n{generated_sql}\n\n")
        f.write(f"RESULT:\n{result}\n\n")
        f.write(f"EXPECTED RESULT:\n{expected_result}\n")

# MAIN EVALUATION
async def run_evaluation():
    data = json.loads(RESULTS_PATH.read_text(encoding="utf-8"))

    num_success = 0
    num_total = 0
    difficulty_summary = defaultdict(lambda: {"correct": 0, "total": 0})

    for entry in data:
        question = entry["question"]
        expected_sql = entry["expected_sql"]
        generated_sql = entry.get("refiner_output") or entry.get("selector_output")
        difficulty = entry.get("difficulty", "unknown")

        logger.info(f"Evaluating: {question}")
        num_total += 1
        difficulty_summary[difficulty]["total"] += 1

        if not generated_sql or "[Error]" in generated_sql or "Timeout" in generated_sql:
            logger.warning(f"Skipping due to invalid SQL:\n{generated_sql}")
            write_evaluation_results(EVALUATION_FILE, question, expected_sql, generated_sql, "No result", "N/A", False, difficulty)
            continue

        try:
            expected_result = pd.DataFrame(execute_sql_query(DB_CONFIG, expected_sql))
        except Exception as e:
            logger.error(f"Expected SQL failed: {e}")
            write_evaluation_results(EVALUATION_FILE, question, expected_sql, generated_sql, "N/A", str(e), False, difficulty)
            continue

        try:
            generated_result = pd.DataFrame(execute_sql_query(DB_CONFIG, generated_sql))
        except Exception as e:
            logger.error(f"Generated SQL failed: {e}")
            write_evaluation_results(EVALUATION_FILE, question, expected_sql, generated_sql, str(e), expected_result.to_string(), False, difficulty)
            continue

        expected_result, generated_result = coerce_table_to_same_type(expected_result, generated_result)

        is_equal = compare_pandas_table(generated_result, expected_result)
        is_partial = False
        if not is_equal:
            is_partial = compare_pandas_table(generated_result, expected_result, ignore_order=True)
            if not is_partial:
                is_partial = check_pandas_table_partial_equality(expected_result, generated_result)

        if is_equal or is_partial:
            num_success += 1
            difficulty_summary[difficulty]["correct"] += 1
            logger.success("Query matched expected result." if is_equal else "Partial match.")
        else:
            logger.error("Query did NOT match expected result.")

        write_evaluation_results(
            EVALUATION_FILE,
            question,
            expected_sql,
            generated_sql,
            generated_result.to_string(),
            expected_result.to_string(),
            is_equal or is_partial,
            difficulty
        )

    # FINAL SUMMARY
    logger.info("\n\n========= SUMMARY =========")
    for diff, stats in difficulty_summary.items():
        correct = stats["correct"]
        total = stats["total"]
        acc = (correct / total) * 100 if total > 0 else 0
        logger.info(f"{diff.capitalize():<12}: {correct}/{total} correct ({acc:.2f}%)")

    overall_acc = (num_success / num_total) * 100 if num_total > 0 else 0
    logger.info(f"\nOverall Accuracy: {num_success}/{num_total} ({overall_acc:.2f}%)")

    # Write final summary to file
    with open(EVALUATION_FILE, 'a', encoding='utf-8') as f:
       f.write(f"\n\n{'=' * 80}\n")
       f.write(f"EVALUATION SUMMARY: {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
       f.write(f"{'=' * 80}\n\n")
       for diff, stats in difficulty_summary.items():
           correct = stats["correct"]
           total = stats["total"]
           acc = (correct / total) * 100 if total > 0 else 0
           f.write(f"{diff.capitalize():<12}: {correct}/{total} correct ({acc:.2f}%)\n")

       f.write(f"\nOverall Accuracy: {num_success}/{num_total} ({overall_acc:.2f}%)\n")
       f.write(f"{'=' * 80}\n")


# ENTRY POINT
if __name__ == "__main__":
    asyncio.run(run_evaluation())

