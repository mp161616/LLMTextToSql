import sys, subprocess, tempfile, os, re, psycopg2

MAX_ROUNDS, FETCH_LIMIT = 3, 1

def load_schema(p): return open(p, encoding='utf-8').read()

def test_sql(conn, sql: str):
    """
    Executes the SQL safely:
    - SELECT: append LIMIT and check for rows.
    - INSERT/UPDATE/DELETE: run inside transaction, always rollback.
    """
    with conn.cursor() as cur:
        sql_type = sql.strip().split()[0].upper()

        if sql_type == "SELECT":
            cur.execute(f"{sql} LIMIT {FETCH_LIMIT}")
            return cur.fetchall()
        else:
            # Begin transaction
            conn.autocommit = False
            try:
                cur.execute(sql)
                conn.rollback()  # Always rollback mutations
                return [("Mutation query executed successfully",)]
            except Exception as e:
                conn.rollback()
                raise e
            finally:
                conn.autocommit = True


def call_ollama(prompt, model="sqlcoder"):
    proc = subprocess.run(
        ['ollama','run',model],
        input=prompt, capture_output=True,
        encoding='utf-8', errors='ignore'
    )
    if proc.returncode!=0: raise RuntimeError(proc.stderr)
    return proc.stdout

def extract_sql(o):
    t = re.sub(r"^<s>\s*", "", o.strip())
    if t.startswith("```") and t.endswith("```"):
        lines = t.splitlines()
        return "\n".join(lines[1:-1]).strip()
    return t


def main():
    if len(sys.argv)!=6:
        print("Usage: refiner.py <sql> <err> <q> <schema> <dsn>"); sys.exit(1)

    flawed, err, q, schP, dsn = sys.argv[1:]
    schema = load_schema(schP)

    try: conn=psycopg2.connect(dsn)
    except Exception as e:
        print(f"[ERROR] DB connect failed: {e}", flush=True); sys.exit(1)

    try:
        if test_sql(conn, flawed):
            print(flawed, flush=True); return
        else: err="Ran without error but zero rows."
    except Exception as e:
        err=str(e)

    cur=flawed

    for _ in range(MAX_ROUNDS):
        prompt = f"""
        You are an expert SQL assistant. Your job is to **inspect** and **correct** a flawed SQL query
        based on an error message from the database, while remaining consistent with the given schema.
        You may generate SELECT, UPDATE, or DELETE queries based on the user's intent and the given schema.

        Below is the **filtered/compressed schema** (only tables & columns relevant to the question):
        
        Schema:
        {schema}

        User Question:
        {q}

        The model attempts this SQL:
        ```sql
        {cur}

        When executed on the database above, it produced this error:
        {err}

        Please diagnose what is wrong with the SQL (syntax, wrong table/column, invalid join, etc.), 
        and **provide a corrected, fully valid SQL query** that will run successfully under the same schema. 
        Return **only the corrected SQL** (no additional explanation).

        Corrected SQL:
        """
        raw = call_ollama(prompt)
        cur = extract_sql(raw)
        try:
            if test_sql(conn, cur):
                print(cur, flush=True)
                return
            else:
                err = "Ran without error but zero rows."
        except Exception as e:
            err = str(e)

    print(cur, flush=True)

if __name__ == "__main__":
    main()

