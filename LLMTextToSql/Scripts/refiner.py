# refiner.py

import sys
import json
import ollama     # pip install ollama
import time       # for timeout example

def load_schema(schema_path: str) -> str:
    """
    Reads the compressed schema JSON from disk.
    """
    print(f"DEBUG: load_schema() reading: {schema_path}", flush=True)
    with open(schema_path, 'r', encoding='utf-8') as f:
        data = f.read()
    print(f"DEBUG: load_schema() succeeded, length={len(data)} bytes", flush=True)
    return data

def build_refiner_prompt(flawed_sql: str, error_msg: str, question: str, schema_json: str) -> str:
    """
    Constructs a Chain-of-Thought style prompt for the Refiner.
    """
    prompt = f"""
You are an expert SQL assistant. Your job is to **inspect** and **correct** a flawed SQL query
based on an error message from the database, while remaining consistent with the given schema.
You may generate SELECT, UPDATE, or DELETE queries based on the user's intent and the given schema.

Below is the **filtered/compressed schema** (only tables & columns relevant to the question):

{schema_json}

User question: "{question}"

The model attempts this SQL:
---
{flawed_sql}
---

When executed on the database above, it produced this error:
---
{error_msg}
---

Please diagnose what is wrong with the SQL (syntax, wrong table/column, invalid join, etc.), 
and **provide a corrected, fully valid SQL query** that will run successfully under the same schema. 
Return **only the corrected SQL** (no additional explanation).

Corrected SQL:
"""
    print(f"DEBUG: Prompt built, length={len(prompt)} characters", flush=True)
    return prompt

def call_ollama_refiner(prompt: str, model_name: str = "sqlcoder") -> str:
    """
    Sends the prompt to Ollama (SQLCoder) as a single‐turn chat and returns the model's raw response.
    """
    start_time = time.time()

    # Make sure we do NOT use stream=True
    response = ollama.chat(
        model=model_name,
        messages=[{"role": "user", "content": prompt}]
    )

    elapsed = time.time() - start_time
    return response["message"]["content"]

def extract_correct_sql(ollama_output: str) -> str:
    """
    Extract the first SQL-looking block, stripping backticks if present.
    """
    text = ollama_output.strip()
    if text.startswith("```") and text.endswith("```"):
        lines = text.splitlines()
        return "\n".join(lines[1:-1]).strip()
    return text

def main():

    if len(sys.argv) != 5:
        print("Usage: python refiner.py \"<flawed_sql>\" \"<error_message>\" \"<original_question>\" \"<schema_json_path>\"", flush=True)
        sys.exit(1)

    flawed_sql  = sys.argv[1]
    error_msg   = sys.argv[2]
    question    = sys.argv[3]
    schema_path = sys.argv[4]

    # 1) Load the filtered/compressed schema
    schema_json = load_schema(schema_path)

    # 2) Build the refiner prompt
    prompt = build_refiner_prompt(flawed_sql, error_msg, question, schema_json)

    # 3) Call Ollama (SQLCoder) to “refine” the SQL
    try:
        ollama_raw = call_ollama_refiner(prompt, model_name="sqlcoder")
    except Exception as e:
        print(f"[ERROR] Exception during ollama.chat(): {e}", flush=True)
        sys.exit(1)

    # 4) Extract just the corrected SQL
    corrected_sql = extract_correct_sql(ollama_raw)

    print(corrected_sql, flush=True)

if __name__ == "__main__":
    main()

