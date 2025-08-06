#!/usr/bin/env python3
# -*- coding: utf-8 -*-

#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import sys
from vanna.chromadb import ChromaDB_VectorStore
from vanna.ollama import Ollama

# === Logger ===
#def debug(msg): print(f">>> [DEBUG] {msg}", flush=True)
def error_exit(msg): print(f"[ERROR] {msg}", file=sys.stderr); sys.exit(1)

# === Args ===
if len(sys.argv) != 2:
    error_exit("Usage: selector_vanna.py '<question>'")

question = sys.argv[1]

# === Define Vanna wrapper ===
class MyVanna(ChromaDB_VectorStore, Ollama):
    def __init__(self, config=None):
        ChromaDB_VectorStore.__init__(self, config=config)
        Ollama.__init__(self, config=config)

# === Setup your DB + LLM config ===
vn = MyVanna(config={
    "model": "phi3:mini",
    "api_base": "http://localhost:11434"
})

# === Connect to Postgres
try:
    vn.connect_to_postgres(
        host="localhost",
        port="5432",
        dbname="pagila",
        user="postgres",
        password="admin"
    )
except Exception as e:
    error_exit(f"Postgres connection failed: {e}")

# === Ask Vanna
try:
  #   schema = vn.run_sql("""
  #  SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
  #  WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
  #  """)
     #plan = vn.get_training_plan_generic(schema)
     #vn.train(plan=plan)

     vn.train(sql="SELECT * FROM film")


except Exception as e:
    error_exit(f"Schema training failed: {e}")

import io
import contextlib
import logging

# Turn off all logging from external libraries
logging.getLogger().handlers.clear()
logging.getLogger().setLevel(logging.CRITICAL)

try:
    with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
        sql = vn.generate_sql(question, allow_llm_to_see_data=True)
    print(sql.strip())  # Only this goes to stdout
except Exception as e:
    # Write errors to stderr (not stdout)
    error_exit(f"Vanna failed to generate SQL: {e}")

