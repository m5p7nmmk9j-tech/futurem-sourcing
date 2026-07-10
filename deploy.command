#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")"
PROJECT_DIR="$(pwd -P)"

clear
printf '\n========================================\n'
printf ' FUTUREM Docker 部署\n'
printf '========================================\n\n'

fail() {
  local code=$?
  printf '\n部署失败，错误代码：%s\n' "$code"
  printf '请把上面的完整错误信息截图发给开发人员。\n'
  read -r -p '按回车键关闭窗口...'
  exit "$code"
}
trap fail ERR

if ! command -v docker >/dev/null 2>&1; then
  printf '未检测到 Docker，请先安装 Docker Desktop。\n'
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  printf '[1/6] 正在启动 Docker Desktop...\n'
  open -a Docker >/dev/null 2>&1 || true
  for _ in $(seq 1 60); do
    docker info >/dev/null 2>&1 && break
    sleep 2
  done
fi

if ! docker info >/dev/null 2>&1; then
  printf 'Docker Desktop 未能启动，请手动打开后再运行。\n'
  exit 1
fi

printf '[1/6] Docker 已就绪。\n'

if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  printf '\n[2/6] 拉取 GitHub main 分支最新代码...\n'
  git pull --ff-only origin main
else
  printf '\n[2/6] 当前目录不是 Git 仓库，使用现有代码部署。\n'
fi

printf '\n[3/6] 停止当前项目旧容器（保留数据库卷）...\n'
docker compose down --remove-orphans || true

printf '\n[4/6] 检查并清理同名冲突容器...\n'
for name in futurem_web futurem_api futurem_redis futurem_mysql; do
  if docker inspect "$name" >/dev/null 2>&1; then
    owner_dir="$(docker inspect "$name" --format '{{ index .Config.Labels "com.docker.compose.project.working_dir" }}' 2>/dev/null || true)"
    if [ -n "$owner_dir" ] && [ "$owner_dir" != "$PROJECT_DIR" ]; then
      printf '发现旧部署冲突：%s（来源：%s），正在移除容器，数据卷不会删除。\n' "$name" "$owner_dir"
    else
      printf '发现残留容器：%s，正在移除，数据卷不会删除。\n' "$name"
    fi
    docker rm -f "$name" >/dev/null
  fi
done

printf '\n[5/6] 重新构建并启动 MySQL、Redis、API、Web...\n'
docker compose up -d --build

printf '\n[6/6] 等待服务启动并检查状态...\n'
web_ok=0
api_ok=0
for _ in $(seq 1 60); do
  if curl -fsS http://localhost:3000 >/dev/null 2>&1; then web_ok=1; fi
  if curl -fsS http://localhost:8080/swagger/index.html >/dev/null 2>&1 || curl -fsS http://localhost:8080 >/dev/null 2>&1; then api_ok=1; fi
  if [ "$web_ok" -eq 1 ] && [ "$api_ok" -eq 1 ]; then break; fi
  sleep 2
done

docker compose ps

printf '\n========================================\n'
if [ "$web_ok" -eq 1 ]; then
  printf ' Web：正常  http://localhost:3000\n'
else
  printf ' Web：尚未响应，请查看 futurem_web 日志\n'
fi
if [ "$api_ok" -eq 1 ]; then
  printf ' API：正常  http://localhost:8080\n'
else
  printf ' API：尚未响应，请查看 futurem_api 日志\n'
fi
printf ' MySQL：localhost:3307\n'
printf ' Redis：localhost:6379\n'
printf '========================================\n\n'

if [ "$web_ok" -ne 1 ] || [ "$api_ok" -ne 1 ]; then
  printf '最近日志：\n'
  docker compose logs --tail=80 web api
  exit 1
fi

open http://localhost:3000 >/dev/null 2>&1 || true
printf '部署完成，浏览器已打开。数据库数据已保留。\n'
read -r -p '按回车键关闭窗口...'
