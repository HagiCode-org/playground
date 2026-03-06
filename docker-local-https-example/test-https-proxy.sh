# Integration Tests

This directory contains integration tests for the Docker Local HTTPS Proxy.

## Test Script

Create a test script to verify the end-to-end HTTPS flow:

```bash
#!/bin/bash
# test-https-proxy.sh

set -e

echo "=== Docker Local HTTPS Proxy Integration Tests ==="
echo ""

# Test 1: Check if services are running
echo "Test 1: Checking if services are running..."
docker compose ps | grep -q "caddy-proxy" || { echo "FAIL: Caddy not running"; exit 1; }
docker compose ps | grep -q "backend-service" || { echo "FAIL: Backend not running"; exit 1; }
echo "PASS: Services running"
echo ""

# Test 2: Verify HTTP to HTTPS redirect
echo "Test 2: Verifying HTTP to HTTPS redirect..."
HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:45000/)
if [ "$HTTP_STATUS" = "301" ]; then
    echo "PASS: HTTP redirects to HTTPS"
else
    echo "FAIL: Expected 301, got $HTTP_STATUS"
    exit 1
fi
echo ""

# Test 3: Verify HTTPS access
echo "Test 3: Verifying HTTPS access..."
HTTPS_RESPONSE=$(curl -k -s https://localhost:45443/)
if echo "$HTTPS_RESPONSE" | grep -q "Hello from HTTPS-backed service"; then
    echo "PASS: HTTPS access working"
else
    echo "FAIL: HTTPS response unexpected"
    exit 1
fi
echo ""

# Test 4: Verify X-Forwarded headers
echo "Test 4: Verifying X-Forwarded headers..."
if echo "$HTTPS_RESPONSE" | grep -q "xForwardedFor"; then
    echo "PASS: X-Forwarded headers present"
else
    echo "FAIL: X-Forwarded headers missing"
    exit 1
fi
echo ""

# Test 5: Verify certificate persistence
echo "Test 5: Verifying certificate persistence..."
CERT_CHECKSUM_BEFORE=$(docker cp caddy-proxy:/data/caddy/certificates/local/localhost/localhost.crt /tmp/cert_before.crt 2>/dev/null && md5sum /tmp/cert_before.crt | awk '{print $1}')
docker compose restart caddy
sleep 2
CERT_CHECKSUM_AFTER=$(docker cp caddy-proxy:/data/caddy/certificates/local/localhost/localhost.crt /tmp/cert_after.crt 2>/dev/null && md5sum /tmp/cert_after.crt | awk '{print $1}')
if [ "$CERT_CHECKSUM_BEFORE" = "$CERT_CHECKSUM_AFTER" ]; then
    echo "PASS: Certificate persisted after restart"
else
    echo "FAIL: Certificate was regenerated"
    exit 1
fi
echo ""

echo "=== All tests passed! ==="
```

## Running the Tests

```bash
cd playground/docker-local-https-example

# Ensure services are running
docker compose up -d

# Run tests
chmod +x test-https-proxy.sh
./test-https-proxy.sh
```

## Multi-Service Tests

For testing multi-service scenarios, use:

```bash
docker compose -f docker-compose.multiservice.yml up -d

# Test API route
curl -k https://localhost:45443/api/

# Test web route
curl -k https://localhost:45443/
```

## CI/CD Integration

Add to your CI pipeline:

```yaml
# .github/workflows/https-proxy-test.yml
name: HTTPS Proxy Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run Docker Compose
        run: cd playground/docker-local-https-example && docker compose up -d
      - name: Run Integration Tests
        run: |
          sleep 5
          cd playground/docker-local-https-example
          ./test-https-proxy.sh
      - name: Cleanup
        if: always()
        run: cd playground/docker-local-https-example && docker compose down
```
