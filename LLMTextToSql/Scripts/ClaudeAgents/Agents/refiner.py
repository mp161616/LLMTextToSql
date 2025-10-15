import sys, re, psycopg2, unicodedata
import anthropic
from anthropic.types import TextBlock
import json

# Load Claude API Key from configuration
def load_settings(path="../appsettings.json"):
    with open(path, "r", encoding="utf-8") as f:
        config = json.load(f)
    return config["ApiKeys"]["Claude"]

# Claude setup
claude_key = load_settings()
CLAUDE_MODEL = "claude-opus-4-1-20250805"
client = anthropic.Anthropic(api_key=claude_key)
SCHEMA_PATH = "../Schemas/european_football_schema.json"

# Settings
CONFIG_PATH = "Config/config_european_football.json"
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

def call_claude(prompt: str) -> str:
    try:
        response = client.messages.create(
            model=CLAUDE_MODEL,
            max_tokens=1024,
            messages=[
                {
                    "role": "user",
                    "content": str(prompt)
                }
            ]
        )

        return response.content[0].text
    except Exception as e:
        raise RuntimeError(f"Claude API failed: {e}")

def extract_sql(claude_output: str) -> str:
    text = claude_output.strip()
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
    debug(f"Using schema path: {SCHEMA_PATH}")
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
{SCHEMA_PATH}

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
            raw = call_claude(prompt)
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
