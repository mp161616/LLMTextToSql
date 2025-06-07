# decomposer.py

import sys
import json
import ollama  # pip install ollama

if len(sys.argv) < 3:
    print("Usage: decomposer.py \"<question>\" \"<schema.json path>\"")
    sys.exit(1)

question = sys.argv[1]
schema_path = sys.argv[2]

with open(schema_path, 'r') as f:
    schema_json = f.read()

prompt = f"""
You are an intelligent SQL assistant. Given the database schema below, break down the user question
into sub-questions and their respective SQL queries (chain-of-thought), then produce the final SQL.

Schema:
{schema_json}

Format:
Sub-question 1: ...
SQL 1: ...
...
Final SQL: ...

User question: "{question}"
"""

# Send a single-turn chat to Ollama
response = ollama.chat(
    model='sqlcoder',
    messages=[{"role": "user", "content": prompt}]
)

# Print the full chain-of-thought including 'Final SQL: ...'
print(response['message']['content'])
