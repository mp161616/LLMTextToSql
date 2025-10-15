import sys
from vanna.chromadb import ChromaDB_VectorStore
from vanna.ollama import Ollama
import json
import io
import contextlib
import logging

# Error handler
def error_exit(msg): print(f"[ERROR] {msg}", file=sys.stderr); sys.exit(1)

# Check input
if len(sys.argv) != 2:
    error_exit("Usage: selector.py '<question>'")

question = sys.argv[1]

# Load config file with hints
CONFIG_PATH = "/Config/config.json"
try:
    with open(CONFIG_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)
except Exception as e:
    error_exit(f"Failed to load config.json: {e}")

# SqlCoder wrapper for Vanna
class MyVanna(ChromaDB_VectorStore, Ollama):
    def __init__(self, config=None):
        ChromaDB_VectorStore.__init__(self, config=config)
        Ollama.__init__(self, config=config)

vn = MyVanna(config={
    "model": "phi3:mini",
    "api_base": "http://localhost:11434"
})

try:
    vn.connect_to_postgres(
        host="localhost",
        port="5432",
        dbname="card_games",
        user="postgres",
        password="admin"
    )
except Exception as e:
    error_exit(f"Postgres connection failed: {e}")


with open("config.json", "r", encoding="utf-8") as f:
    all_examples = json.load(f)


# Train the model
for ex in all_examples:
    try:
        if ex.get("SQL"):
            vn.train(question=ex["question"], sql=ex["SQL"])
    except Exception as e:
        print(f" Failed on question {ex['question_id']}: {e}")


logging.getLogger().handlers.clear()
logging.getLogger().setLevel(logging.CRITICAL)

# Generate SQL from question
try:
    with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
        sql = vn.generate_sql(question, allow_llm_to_see_data=True)
    print(sql.strip())  # Only this goes to stdout
except Exception as e:
    # Write errors to stderr (not stdout)
    error_exit(f"Vanna failed to generate SQL: {e}")