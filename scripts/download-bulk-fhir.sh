#!/usr/bin/env bash
set -euo pipefail

# FHIR Bulk Data Download Script
# Downloads all resources from the SMART Health IT Bulk Data reference server

BASE_URL="https://bulk-data.smarthealthit.org/eyJlcnIiOiIiLCJwYWdlIjoxMDAwMCwidGx0IjoxNSwibSI6MTAsImRlbCI6MCwic2VjdXJlIjowfQ/fhir"
OUTPUT_DIR="${1:-patient-1000}"

mkdir -p "$OUTPUT_DIR"

echo "=== FHIR Bulk Data Export ==="
echo "Server: $BASE_URL"
echo "Output: $OUTPUT_DIR"
echo ""

# Step 1: Kick off the export
echo "[1/3] Kicking off bulk export..."
RESPONSE=$(curl -s -D - -o /dev/null \
  -H "Accept: application/fhir+json" \
  -H "Prefer: respond-async" \
  "$BASE_URL/\$export")

CONTENT_LOCATION=$(echo "$RESPONSE" | grep -i "content-location:" | tr -d '\r' | sed 's/[Cc]ontent-[Ll]ocation: //')

if [ -z "$CONTENT_LOCATION" ]; then
  echo "ERROR: No Content-Location header in response. Full response:"
  echo "$RESPONSE"
  exit 1
fi

echo "  Poll URL: $CONTENT_LOCATION"
echo ""

# Step 2: Poll until complete
echo "[2/3] Polling for export completion..."
while true; do
  HTTP_CODE=$(curl -s -o /tmp/bulk-fhir-poll.json -w "%{http_code}" \
    -H "Accept: application/fhir+json" \
    "$CONTENT_LOCATION")

  if [ "$HTTP_CODE" = "200" ]; then
    echo "  Export complete!"
    break
  elif [ "$HTTP_CODE" = "202" ]; then
    PROGRESS=$(grep -o '"progress":"[^"]*"' /tmp/bulk-fhir-poll.json 2>/dev/null || echo "in progress")
    echo "  Status: $HTTP_CODE ($PROGRESS) - waiting 2s..."
    sleep 2
  else
    echo "  ERROR: Unexpected status $HTTP_CODE"
    cat /tmp/bulk-fhir-poll.json
    exit 1
  fi
done
echo ""

# Step 3: Download all files from the manifest
echo "[3/3] Downloading NDJSON files..."
MANIFEST=$(cat /tmp/bulk-fhir-poll.json)

# Extract all file URLs from the output array
FILE_URLS=$(echo "$MANIFEST" | python3 -c "
import json, sys
data = json.load(sys.stdin)
for entry in data.get('output', []):
    url = entry.get('url', '')
    rtype = entry.get('type', 'unknown')
    print(f'{rtype}\t{url}')
")

TOTAL=$(echo "$FILE_URLS" | wc -l | tr -d ' ')
COUNT=0

echo "  Found $TOTAL files to download"
echo ""

# Track counts per resource type for summary
declare -A TYPE_COUNTS

while IFS=$'\t' read -r RTYPE URL; do
  COUNT=$((COUNT + 1))
  # Extract filename from URL or construct one
  FILENAME=$(basename "$URL")

  # If filename doesn't end with .ndjson, construct a better name
  if [[ ! "$FILENAME" == *.ndjson ]]; then
    IDX=${TYPE_COUNTS[$RTYPE]:-0}
    IDX=$((IDX + 1))
    TYPE_COUNTS[$RTYPE]=$IDX
    FILENAME="${RTYPE}.${IDX}.ndjson"
  fi

  printf "  [%3d/%d] Downloading %s..." "$COUNT" "$TOTAL" "$FILENAME"
  curl -s -o "$OUTPUT_DIR/$FILENAME" "$URL"
  LINES=$(wc -l < "$OUTPUT_DIR/$FILENAME" | tr -d ' ')
  echo " ${LINES} lines"
done <<< "$FILE_URLS"

echo ""
echo "=== Download Complete ==="
echo "Files saved to: $OUTPUT_DIR/"
echo ""
du -sh "$OUTPUT_DIR"
echo ""
ls -1 "$OUTPUT_DIR" | head -20
TOTAL_FILES=$(ls -1 "$OUTPUT_DIR" | wc -l | tr -d ' ')
if [ "$TOTAL_FILES" -gt 20 ]; then
  echo "  ... and $((TOTAL_FILES - 20)) more files"
fi
