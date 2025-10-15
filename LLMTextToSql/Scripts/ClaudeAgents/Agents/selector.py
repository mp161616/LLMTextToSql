import sys
import json
import anthropic
from vanna.chromadb import ChromaDB_VectorStore
from vanna.base import VannaBase
import io
import contextlib
import logging

# Error handler
def error_exit(msg): 
    print(f"[ERROR] {msg}", file=sys.stderr)
    sys.exit(1)

# Check input
if len(sys.argv) != 2:
    error_exit("Usage: selector.py '<question>'")

question = sys.argv[1]

# Load config file with hints
CONFIG_PATH = "Config/config_european_football.json"
try:
    with open(CONFIG_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)
except Exception as e:
    error_exit(f"Failed to load config.json: {e}")

# Load model API Key from configuration
def load_settings(path="../appsettings.json"):
    with open(path, "r", encoding="utf-8") as f:
        config = json.load(f)
    return config["ApiKeys"]["Claude"]


# Claude wrapper for Vanna
class Claude(VannaBase):
    def __init__(self, api_key: str, model: str = "claude-opus-4-1-20250805", config=None):
        super().__init__(config=config)
        self.client = anthropic.Anthropic(api_key=api_key)
        self.model = model

    def system_message(self, content: str):
        return {"role": "system", "content": content}

    def user_message(self, content: str):
        return {"role": "user", "content": content}

    def assistant_message(self, content: str):
        return {"role": "assistant", "content": content}

    def submit_prompt(self, prompt: str, **kwargs) -> str:
        try:
            response = self.client.messages.create(
                model=self.model,
                max_tokens=1024,
                messages=[
                 {"role": "user", "content": str(prompt)}
                ]
            )
            return response.content[0].text
            print("\n=== Prompt Sent to Claude ===")
            print(str(prompt))
            print("=== End of Prompt ===\n")
        except anthropic.AuthenticationError as e:
          raise RuntimeError(f"Claude auth error: {e}")
        except Exception as e:
          raise RuntimeError(f"Claude API failed: {e}")



# Hybrid class
class MyVanna(ChromaDB_VectorStore, Claude):
    def __init__(self, config=None):
        ChromaDB_VectorStore.__init__(self, config=config)
        Claude.__init__(self, api_key=config["api_key"], model=config["model"], config=config)


claude_key = load_settings()

vn = MyVanna(config={
    "model": "claude-opus-4-1-20250805",
    "api_key": claude_key
})

try:
    vn.connect_to_postgres(
        host="localhost",
        port="5432",
        dbname="european_football",
        user="postgres",
        password="admin"
    )
except Exception as e:
    error_exit(f"Postgres connection failed: {e}")


with open("Config/config_european_football.json", "r", encoding="utf-8") as f:
    all_examples = json.load(f)


for ex in all_examples:
    try:
        if ex.get("SQL"):
            vn.train(question=ex["question"], sql=ex["SQL"])
    except Exception as e:
        print(f" Failed on question {ex['question_id']}: {e}")


logging.getLogger().handlers.clear()
logging.getLogger().setLevel(logging.CRITICAL)

try:
    with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
        sql = vn.generate_sql(question, allow_llm_to_see_data=True)
    print(sql.strip())
except Exception as e:
    # Write errors to stderr (not stdout)
    error_exit(f"Vanna failed to generate SQL: {e}")
