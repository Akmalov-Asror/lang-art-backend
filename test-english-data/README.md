# Test English Grammar Curriculum

Scraped JSON content from test-english.com — used by `TestEnglishSeeder`.

## Contents

- `a1/` — 35 grammar topics (Beginner)
- `a2/` — Elementary topics
- `b1/` — Intermediate topics
- `b2/` — Upper-intermediate topics
- `c1/` — Advanced topics
- `_summary.json` — index of all topics

Total: 156 lessons.

## How to seed

The seeder picks this directory up automatically if you point the env var at it:

```bash
TEST_ENGLISH_SEED_DIR=/app/test-english-data dotnet LangArt.Api.dll seed:test-english
```

Or via Docker Compose — bind-mount this folder into the backend container and
set `TEST_ENGLISH_SEED_DIR` in the `backend` service:

```yaml
backend:
  environment:
    TEST_ENGLISH_SEED_DIR: /app/test-english-data
  volumes:
    - ./lang-art-back/test-english-data:/app/test-english-data:ro
```

Then run:

```bash
docker compose exec backend dotnet LangArt.Api.dll seed:test-english
```

Idempotent — re-running skips existing lessons. Use `seed:test-english:force`
to overwrite.
