#!/usr/bin/env bash
# Imports a dashboard JSON file in the Grafana of the demo stack, linked to its Prometheus datasource.
# Usage: ./import-dashboard.sh dashboard.json
set -euo pipefail

file="${1:?Usage: $0 dashboard.json}"
grafana="${GRAFANA_URL:-http://localhost:3000}"

python3 -I - "$file" <<'PY' | curl -sS -X POST "$grafana/api/dashboards/import" -H 'Content-Type: application/json' -d @-
import json, sys

dashboard = json.load(open(sys.argv[1]))
print(json.dumps({
    "dashboard": dashboard,
    "overwrite": True,
    "inputs": [{"name": "DS_PROMETHEUS", "type": "datasource", "pluginId": "prometheus", "value": "prometheus-demo"}],
}))
PY
echo
