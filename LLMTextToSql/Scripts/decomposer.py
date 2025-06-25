#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import sys
import json
from ollama import chat  # pip install ollama

if len(sys.argv) < 3:
    print("Usage: decomposer.py \"<question>\" \"<schema.json path>\"")
    sys.exit(1)

question = sys.argv[1]
schema_path = sys.argv[2]

# Safely read schema.json using utf-8 with error handling
try:
    with open(schema_path, 'r', encoding='utf-8') as f:
        schema_meta = f.read()
except UnicodeDecodeError:
    print("-- Failed to read schema.json due to encoding error.", file=sys.stderr)
    sys.exit(1)

# Few-shot examples (covering simple select, join, filter, group)
shots = """
### Example 1
User: List all film titles.
Sub-question 1: What are all film titles?
SQL 1: SELECT title FROM film;
Final SQL: SELECT title FROM film;

### Example 2
User: How many films were released in 2006?
Sub-question 1: Which films have release_year = 2006?
SQL 1: SELECT film_id FROM film WHERE release_year = 2006;
Sub-question 2: Count those films.
SQL 2: SELECT COUNT(*) FROM (SELECT film_id FROM film WHERE release_year = 2006) AS t;
Final SQL: SELECT COUNT(*) FROM film WHERE release_year = 2006;

### Example 3
User: What are the names of all languages used in the films?
Sub-question 1: How are films linked to languages?
SQL 1: SELECT DISTINCT language_id FROM film;
Sub-question 2: What are the names of these languages?
SQL 2: SELECT name FROM language WHERE language_id IN (SELECT DISTINCT language_id FROM film);
Final SQL: SELECT DISTINCT l.name FROM language l JOIN film f ON l.language_id = f.language_id;

### Example 4
User: List titles of all horror films.
Sub-question 1: Which film IDs belong to the 'Horror' category?
SQL 1: SELECT film_id FROM film_category fc JOIN category c ON fc.category_id = c.category_id WHERE c.name = 'Horror';
Sub-question 2: What are the titles of these films?
SQL 2: SELECT title FROM film WHERE film_id IN (
            SELECT film_id FROM film_category fc JOIN category c ON fc.category_id = c.category_id WHERE c.name = 'Horror'
       );
Final SQL: SELECT title FROM film WHERE film_id IN (
    SELECT film_id FROM film_category fc JOIN category c ON fc.category_id = c.category_id WHERE c.name = 'Horror'
);

### Example 5
User: How many films were released each year?
Sub-question 1: Group films by release_year and count them.
SQL 1: SELECT release_year, COUNT(*) FROM film GROUP BY release_year;
Final SQL: SELECT release_year, COUNT(*) FROM film GROUP BY release_year;
"""

# Build the full prompt
prompt = f"""
You are an expert SQL assistant. Given the detailed schema metadata below, you must:
  1. Decompose the user question into 1 to 5 numbered sub-questions.
  2. For each sub-question, produce a corresponding SQL query (SQL 1:, SQL 2:, ...).
  3. Then produce a single Final SQL query that answers the original question.

Schema metadata:
{schema_meta}

{shots}

Now, answer this user question:
User: "{question}"
"""

# Run the Ollama chat model
try:
    response = chat(
        model="sqlcoder",
        messages=[{"role": "user", "content": prompt}]
    )
except Exception as e:
    print(f"-- Ollama call failed: {e}", file=sys.stderr)
    sys.exit(1)

# Output chain-of-thought (for display/logging or Final SQL extraction)
print(response["message"]["content"].strip())
