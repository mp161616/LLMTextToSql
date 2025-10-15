import sys, re, psycopg2, unicodedata
from anthropic.types import TextBlock
import json
from openai import OpenAI
import re
import os


# Load Claude API Key from configuration
def load_settings(path="../appsettings.json"):
    current_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.abspath(os.path.join(current_dir, "..", "..", ".."))  # go from Agents → OpenAiAgents → Scripts → LLMTextToSql
    path = os.path.join(project_root, "appsettings.json")

    with open(path, "r", encoding="utf-8") as f:
        config = json.load(f)
    return config["ApiKeys"]["OpenAI"]

# Claude setup
openai_key = load_settings()
OPENAI_MODEL = "gpt-5"
client = OpenAI(api_key=openai_key)

# Settings
current_dir = os.path.dirname(os.path.abspath(__file__))  # -> .../Agents
project_scripts_dir = os.path.abspath(os.path.join(current_dir, "..", ".."))  # -> .../Scripts
CONFIG_PATH = os.path.join(project_scripts_dir, "Config", "config.json")

MAX_ROUNDS = 5
FETCH_LIMIT = 1

def debug(msg):
    print(f"[DEBUG] {msg}", flush=True)

def load_schema(path):
    debug(f"Loading schema from path: {path}")
    return open(path, encoding='utf-8').read()

def load_evidence_for_question(question):
    """Fetch the evidence (hints) for the given question from the config file"""
    try:
        with open(CONFIG_PATH, "r", encoding="utf-8") as f:
            data = json.load(f)
        for item in data:
            if item["question"].strip().lower() == question.strip().lower():
                return item.get("evidence", "")
    except Exception as e:
        debug(f"Failed to load evidence: {e}")
    return ""

def test_sql(conn, sql: str):
    with conn.cursor() as cur:
        sql_type = sql.strip().split()[0].upper()
        debug(f"Detected SQL type: {sql_type}")
        debug(f"SQL being executed: {sql}")

        if sql_type == "SELECT":
            sql_cleaned = sql.strip().rstrip(';')
            if re.search(r"\bLIMIT\b", sql_cleaned, re.IGNORECASE):
                limited_sql = sql_cleaned
            else:
                limited_sql = f"{sql_cleaned} LIMIT {FETCH_LIMIT}"
            cur.execute(limited_sql)
            return cur.fetchall()
        else:
            conn.autocommit = False
            try:
                cur.execute(sql)
                conn.rollback()
                return [("Mutation query executed successfully",)]
            except Exception as e:
                conn.rollback()
                debug(f"SQL execution error: {e}")
                raise e
            finally:
                conn.autocommit = True

def call_gpt5(prompt: str) -> str:
    try:
        response = client.chat.completions.create(
                model=OPENAI_MODEL,
                messages=[
                    {"role": "user", "content": str(prompt)}
                ],
                max_completion_tokens=1024,
                temperature=1
        )
        return response.choices[0].message.content
    except Exception as e:
            raise RuntimeError(f"GPT-5 API failed: {e}")

def extract_sql(gpt5_output: str) -> str:
    text = gpt5_output.strip()
    text = text.replace("```sql", "").replace("```", "")
    text = unicodedata.normalize('NFKC', text)
    text = ''.join(c for c in text if not unicodedata.category(c).startswith('C'))
    return text.strip(" '\n\r\t")

def main():
    if len(sys.argv) != 7:
        print("Usage: refiner.py <sql> <err> <q> <evidence> <schema> <dsn>")
        sys.exit(1)

    flawed, err, q, evidence, schema_path, dsn = sys.argv[1:]
    debug(f"Received flawed SQL: {flawed}")
    debug(f"Received error message: {err}")
    debug(f"Received question: {q}")
    debug(f"Using schema path: {schema_path}")
    debug(f"Using DB connection string: {dsn}")

    auto_evidence = load_evidence_for_question(q)
    if auto_evidence:
        debug("Auto evidence found in config.json")
        evidence = auto_evidence
    else:
        debug("No evidence found in config.json, using passed argument")

    schema = load_schema(schema_path)

    try:
        conn = psycopg2.connect(dsn)
        conn.autocommit = True
    except Exception as e:
        print(f"[ERROR] DB connect failed: {e}", flush=True)
        sys.exit(1)

    try:
        if test_sql(conn, flawed):
            print(flawed, flush=True)
            return
        else:
            err = "Ran without error but zero rows."
    except Exception as e:
        debug(f"Initial SQL execution error: {e}")
        err = str(e)

    cur = flawed

    for _ in range(MAX_ROUNDS):
        prompt = f"""
You are an expert SQL assistant.

Goal: Correct the SQL using the DB error, the schema, and the evidence.

HARD RULES:
- Use ONLY columns/tables in the schema. No invented names.
- Add casts if comparing text to numbers.
- Always have a FROM clause.
- Return ONLY the corrected SQL (no explanations/markdown/comments).
- Keep SELECT minimal (usually just id) unless more fields are explicitly needed.

Schema (typed):
{schema}

Evidence / Hints:
{evidence}

User Question:
{q}

Flawed SQL (may be empty if synthesizing new):
{cur or '[none provided]'}

Database Error:
{err}

Corrected SQL:
""".strip()

        try:
            raw = call_gpt5(prompt)
            cur = extract_sql(raw)

            if "<s>" in cur or "/***" in cur or not cur.lower().startswith(('select', 'update', 'delete')):
                debug("Model returned invalid SQL-like garbage.")
                raise ValueError("Model failed to produce usable SQL.")

            if test_sql(conn, cur):
                print(cur, flush=True)
                return
            else:
                err = "Ran without error but zero rows."

        except Exception as e:
            debug(f"ERRRORRRR: {e}")
            err = str(e)

    debug(f"Final failed SQL: {cur}")
    print("[ERROR] Model failed to produce a valid SQL query after all retries.", flush=True)
    sys.exit(2)

if __name__ == "__main__":
    main()
