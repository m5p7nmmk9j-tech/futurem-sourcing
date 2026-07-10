#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

dump_logs() {
  docker compose ps --all || true
  docker compose logs --tail=150 api mysql redis web || true
}

trap 'status=$?; if [ "$status" -ne 0 ]; then dump_logs; fi; exit "$status"' EXIT

docker compose up -d --build

for attempt in $(seq 1 30); do
  if curl --fail --silent --show-error http://localhost:8080/ >/dev/null \
    && curl --fail --silent --show-error http://localhost:3000/ >/dev/null \
    && docker compose exec -T mysql mysqladmin ping --silent -h localhost -uroot -p"${MYSQL_ROOT_PASSWORD:-futurem123456}" \
    && [ "$(docker compose exec -T redis redis-cli ping)" = "PONG" ]; then
    echo "FUTUREM Docker smoke test passed"
    exit 0
  fi
  sleep 2
done

echo "FUTUREM Docker smoke test timed out" >&2
exit 1
