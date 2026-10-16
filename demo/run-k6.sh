#!/usr/bin/env bash
# Runs the sample k6 test against the local QuickPizza and sends the metrics to the Prometheus of the demo stack.
# Usage: ./run-k6.sh [test-id]    (the test id is the testid label, default: run-1)
set -euo pipefail

cd "$(dirname "$0")/.."

K6_PROMETHEUS_RW_SERVER_URL=http://localhost:9090/api/v1/write \
K6_PROMETHEUS_RW_TREND_STATS="med,p(90),p(95),p(99)" \
k6 run -o experimental-prometheus-rw \
  --tag testname=quickpizza-load-test --tag "testid=${1:-run-1}" \
  -e BASE_URL=http://localhost:3333 \
  --stage 20s:20 --stage 40s:20 --stage 10s:0 \
  samples/quickpizza-load-test.js
