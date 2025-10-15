import sys, subprocess, re, psycopg2, unicodedata

MAX_ROUNDS, FETCH_LIMIT = 5, 1

def debug(msg):
    print(f"[DEBUG] {msg}", flush=True)

def load_schema(path):
    debug(f"Loading schema from path: {path}")
    return open(path, encoding='utf-8').read()

def test_sql(conn, sql: str):
    with conn.cursor() as cur:
        sql_type = sql.strip().split()[0].upper()
        debug(f"Detected SQL type: {sql_type}")
        debug(f"SQL being executed: {sql}")

        if sql_type == "SELECT":
            sql_cleaned = sql.strip().rstrip(';')
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

def call_ollama(prompt, model="sqlcoder"):
    proc = subprocess.run(
        ['ollama', 'run', model],
        input=prompt,
        capture_output=True,
        encoding='utf-8',
        errors='ignore'
    )
    if proc.returncode != 0:
        raise RuntimeError(proc.stderr)
    return proc.stdout

def extract_sql(ollama_output: str) -> str:
    text = ollama_output.strip()
    text = text.replace("```sql", "").replace("```", "")
    text = unicodedata.normalize('NFKC', text)
    text = ''.join(c for c in text if not unicodedata.category(c).startswith('C'))
    return text.strip(" '\n\r\t")

def main():
    if len(sys.argv) != 7:
        print("Usage: refiner.py <sql> <err> <q> <schema> <dsn>")
        sys.exit(1)

    flawed, err, q, evidence, schema_path, dsn = sys.argv[1:]
    debug(f"Received flawed SQL: {flawed}")
    debug(f"Received error message: {err}")
    debug(f"Received question: {q}")
    debug(f"Using schema path: {schema_path}")
    debug(f"Using DB connection string: {dsn}")

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
            raw = call_ollama(prompt)
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