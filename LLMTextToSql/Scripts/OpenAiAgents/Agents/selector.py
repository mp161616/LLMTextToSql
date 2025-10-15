import sys
import json
from openai import OpenAI
from vanna.chromadb import ChromaDB_VectorStore
from vanna.base import VannaBase
import logging
import io
import contextlib
import os

# Error handler
def error_exit(msg): 
    print(f"[ERROR] {msg}", file=sys.stderr)
    sys.exit(1)

# Check input
if len(sys.argv) != 2:
    error_exit("Usage: selector.py '<question>'")

question = sys.argv[1]

# Load config file with hints
current_dir = os.path.dirname(os.path.abspath(__file__))  # -> .../Agents
project_scripts_dir = os.path.abspath(os.path.join(current_dir, "..", ".."))  # -> .../Scripts
CONFIG_PATH = os.path.join(project_scripts_dir, "Config", "config.json")

try:
    with open(CONFIG_PATH, "r", encoding="utf-8") as f:
        config_data = json.load(f)
except Exception as e:
    error_exit(f"Failed to load config.json: {e}")

# Load OpenAI API Key from configuration
def load_settings(path="../appsettings.json"):
    current_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.abspath(os.path.join(current_dir, "..", "..", ".."))  # go from Agents → OpenAiAgents → Scripts → LLMTextToSql
    path = os.path.join(project_root, "appsettings.json")

    with open(path, "r", encoding="utf-8") as f:
        config = json.load(f)
    return config["ApiKeys"]["OpenAI"]

# GPT-5 wrapper for Vanna
class GPT5(VannaBase):
    def __init__(self, api_key: str, model: str = "gpt-5", config=None):
        super().__init__(config=config)
        self.client = OpenAI(api_key=api_key)
        self.model = model

    def system_message(self, content: str):
        return {"role": "system", "content": content}

    def user_message(self, content: str):
        return {"role": "user", "content": content}

    def assistant_message(self, content: str):
        return {"role": "assistant", "content": content}

    def submit_prompt(self, prompt: str, **kwargs) -> str:
        try:
            print("\n=== Prompt Sent to GPT-4o ===")
            print(prompt)
            print("=== End of Prompt ===\n")

            response = self.client.chat.completions.create(
                model=self.model,
                messages=[
                    {"role": "user", "content": str(prompt)}
                ],
                max_completion_tokens=1024,
                temperature=1
            )
            return response.choices[0].message.content
        except Exception as e:
            raise RuntimeError(f"GPT-5 API failed: {e}")

# Hybrid class
class MyVanna(ChromaDB_VectorStore, GPT5):
    def __init__(self, config=None):
        ChromaDB_VectorStore.__init__(self, config=config)
        GPT5.__init__(self, api_key=config["api_key"], model=config["model"], config=config)

# Initialize Vanna with GPT backend

openai_key = load_settings()

vn = MyVanna(config={
    "model": "gpt-5",
    "api_key": openai_key
})

# Connect to database
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

# Train the model
for ex in config_data:
    try:
        if ex.get("SQL") and ex.get("question"):
            vn.train(question=ex["question"], sql=ex["SQL"])
    except Exception as e:
        print(f"Failed on question: {ex.get('question_id', '?')} — {e}")

# Generate SQL from question
try:
    with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
        sql = vn.generate_sql(question, allow_llm_to_see_data=True)
    print(sql.strip())
    sys.exit(0)
except Exception as e:
    error_exit(f"Vanna failed to generate SQL: {e}")
