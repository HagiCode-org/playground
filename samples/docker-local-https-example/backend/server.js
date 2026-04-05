const http = require('http');

const PORT = process.env.PORT || 45000;
const HOST = process.env.HOST || '0.0.0.0';

const server = http.createServer((req, res) => {
  console.log(`${new Date().toISOString()} - ${req.method} ${req.url}`);

  // Log incoming headers for debugging
  console.log('Incoming headers:', JSON.stringify(req.headers, null, 2));

  // Set response headers
  res.setHeader('Content-Type', 'application/json');
  res.setHeader('X-Backend-Service', 'nodejs-backend');

  // Return response with request info
  const response = {
    message: 'Hello from HTTPS-backed service!',
    timestamp: new Date().toISOString(),
    method: req.method,
    path: req.url,
    headers: {
      // Echo back forwarded headers if present
      xForwardedFor: req.headers['x-forwarded-for'],
      xForwardedProto: req.headers['x-forwarded-proto'],
      xRealIp: req.headers['x-real-ip'],
    },
  };

  res.end(JSON.stringify(response, null, 2));
});

server.listen(PORT, HOST, () => {
  console.log(`Backend service running on http://${HOST}:${PORT}`);
  console.log(`Environment: ${process.env.NODE_ENV || 'development'}`);
});
