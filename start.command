#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")"

clear
printf '\n========================================\n'
printf ' FUTUREM 外贸采购跟单系统 一键启动\n'
printf '========================================\n\n'

pause_on_error() {
  local code=$?
  printf '\n启动失败，错误代码：%s\n' "$code"
  printf '请把上面的错误信息截图发给开发人员。\n'
  read -r -p '按回车键关闭窗口...'
  exit "$code"
}
trap pause_on_error ERR

if ! command -v docker >/dev/null 2>&1; then
  printf '未检测到 Docker。请先安装并启动 Docker Desktop。\n'
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  printf 'Docker Desktop 尚未运行，正在尝试启动...\n'
  open -a Docker >/dev/null 2>&1 || true

  for i in $(seq 1 60); do
    if docker info >/dev/null 2>&1; then
      break
    fi
    sleep 2
  done
fi

if ! docker info >/dev/null 2>&1; then
  printf 'Docker Desktop 启动失败，请手动打开 Docker Desktop 后重试。\n'
  exit 1
fi

printf '[1/3] 检查端口占用...\n'
for port in 3000 8080 3307 6379; do
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
    printf '提示：端口 %s 已被占用，若启动失败请先关闭占用该端口的软件。\n' "$port"
  fi
done

printf '\n[2/3] 构建并启动服务...\n'
docker compose up -d --build

printf '\n[3/3] 检查服务状态...\n'
docker compose ps

printf '\n========================================\n'
printf ' FUTUREM 已启动\n'
printf '========================================\n'
printf 'Web:   http://localhost:3000\n'
printf 'API:   http://localhost:8080\n'
printf 'MySQL: localhost:3307\n'
printf 'Redis: localhost:6379\n\n'

open http://localhost:3000 >/dev/null 2>&1 || true

printf '浏览器已打开。此窗口可以关闭，Docker 服务会继续运行。\n'
read -r -p '按回车键关闭窗口...'
