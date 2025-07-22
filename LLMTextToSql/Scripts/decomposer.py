#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import sys
import json
from ollama import chat  # pip install ollama

print(">>> [DEBUG] Starting decomposer.py")

if len(sys.argv) < 3:
    print("Usage: decomposer.py \"<question>\" \"<schema.json path>\"", file=sys.stderr)
    sys.exit(1)

question = sys.argv[1]
schema_path = sys.argv[2]

print(f">>> [DEBUG] Received question: {question}")
print(f">>> [DEBUG] Using schema path: {schema_path}")

# Safely read schema.json using utf-8 with error handling
try:
    with open(schema_path, 'r', encoding='utf-8') as f:
        schema_meta = f.read()
        print(f">>> [DEBUG] Schema file loaded successfully. Size: {len(schema_meta)} characters")
except UnicodeDecodeError:
    print("-- [ERROR] Failed to read schema.json due to encoding error.", file=sys.stderr)
    sys.exit(1)
except Exception as e:
    print(f"-- [ERROR] Failed to open schema file: {e}", file=sys.stderr)
    sys.exit(1)

# Few-shot examples
shots = """
### Example
Sub-question 1: What are all books released after 2010?
SQL 1: SELECT * FROM books WHERE release_year > 2010;

Sub-question 2: Get their titles
SQL 2: SELECT title FROM books WHERE release_year > 2010;

Final SQL: SELECT title FROM books WHERE release_year > 2010;
"""


# Build the full prompt
prompt = f"""
You are an expert SQL assistant. Decompose the user's question into sub-questions,
write SQL for each sub-question, and finally combine them into a single SQL query.

{schema_meta}

{shots}

Now, answer this question:
User: "{question}"
"""

print(f">>> [DEBUG] Prompt built. Total characters: {len(prompt)}")
print(f">>> [DEBUG] Sending prompt to Ollama with model 'sqlcoder'")

# Run the Ollama chat model
try:
    response = chat(
        model="sqlcoder",
        messages=[{"role": "user", "content": prompt}]
    )
except Exception as e:
    print(f"-- [ERROR] Ollama call failed: {e}", file=sys.stderr)
    sys.exit(1)

# Output chain-of-thought reasoning
print(">>> [DEBUG] Ollama responded with content:")
print("========== BEGIN RESPONSE ==========")
print(response["message"]["content"].strip())
print("=========== END RESPONSE ===========")
