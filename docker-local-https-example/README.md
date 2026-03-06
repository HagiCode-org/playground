# Docker Local HTTPS 快速开始指南

本指南将帮助你快速搭建本地 HTTPS 开发环境。

## 前置要求

- Docker Desktop (macOS/Windows) 或 Docker Engine (Linux)
- Docker Compose
- 浏览器（用于访问 HTTPS）

## 快速启动

### 1. 进入示例目录

```bash
cd playground/docker-local-https-example
```

### 2. 启动服务

```bash
docker compose up -d
```

### 3. 验证服务状态

```bash
docker compose ps
```

### 4. 测试访问

- **HTTP**: http://localhost:45000 (会自动重定向到 HTTPS)
- **HTTPS**: https://localhost:45443

### 5. 查看日志

```bash
# 查看所有服务日志
docker compose logs

# 查看 Caddy 日志
docker compose logs caddy

# 查看后端服务日志
docker compose logs backend
```

### 6. 停止服务

```bash
docker compose down
```

## 目录结构

```
docker-local-https-example/
├── Caddyfile              # Caddy 反向代理配置
├── docker-compose.yml     # Docker Compose 配置
├── backend/
│   ├── server.js          # 示例后端服务 (Node.js)
│   ├── Dockerfile         # 后端容器配置
│   └── package.json       # Node.js 依赖
└── README.md              # 本文档
```

## 验证功能

### 验证 HTTPS 访问

```bash
curl -k https://localhost:45443/
```

### 验证 HTTP 到 HTTPS 重定向

```bash
curl -I http://localhost:45000/
# 应该返回 301 重定向到 https://localhost:45443/
```

### 验证证书自动生成

```bash
# 证书存储在 Docker 卷中
docker volume ls | grep caddy
```

## 配置说明

### 修改后端服务端口

编辑 `docker-compose.yml`：

```yaml
environment:
  - PORT=45003  # 改为其他端口
```

然后更新 `Caddyfile` 中的 `reverse_proxy` 端口。

### 添加自定义域名

1. 编辑 `/etc/hosts`（需要 sudo）：
   ```
   127.0.0.1 myapp.local
   ```

2. 修改 `Caddyfile`：
   ```
   myapp.local:45443 {
       reverse_proxy localhost:45001
       tls internal
   }
   ```

3. 重启服务：
   ```bash
   docker compose restart
   ```

## 多服务示例

如果需要运行多服务路由：

```bash
# 使用多服务配置
docker compose -f docker-compose.multiservice.yml up -d
```

然后访问：
- API: https://localhost:45443/api/
- Web: https://localhost:45443/

## 下一步

- [浏览器信任自签名证书说明](./CERTIFICATE-TRUST.md)
- [故障排除指南](./TROUBLESHOOTING.md)
