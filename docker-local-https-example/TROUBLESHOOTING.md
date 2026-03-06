# 故障排除指南

本文档列出常见问题及其解决方案。

## 常见问题

### 1. 端口被占用

**症状**: `EADDRINUSE: address already in use`

**解决方案**:

```bash
# 查找占用端口的进程
lsof -i :45000
lsof -i :45443

# 停止占用进程或修改 docker-compose.yml 中的端口
```

### 2. 后端服务无法启动

**症状**: 后端容器持续重启

**解决方案**:

```bash
# 查看日志
docker compose logs backend

# 常见原因：端口被占用
# 修改 docker-compose.yml 中的 PORT 环境变量
```

### 3. 浏览器显示证书警告

**症状**: 访问 HTTPS 时浏览器显示不安全警告

**解决方案**:

- 参见 [CERTIFICATE-TRUST.md](./CERTIFICATE-TRUST.md) 导入根证书
- 使用 `curl -k` 跳过证书验证（仅用于测试）

### 4. HTTP 重定向不工作

**症状**: HTTP 端口不重定向到 HTTPS

**解决方案**:

检查 Caddyfile 配置：

```
http://localhost:45000 {
    respond * https://localhost:45443{uri} 301
}
```

重启 Caddy：
```bash
docker compose restart caddy
```

### 5. 证书未持久化

**症状**: 重启后需要重新生成证书

**解决方案**:

确保 Docker 卷已正确配置：

```yaml
volumes:
  caddy_data:
    driver: local
```

检查卷是否存在：
```bash
docker volume ls | grep docker-local-https
```

### 6. X-Forwarded 头部未添加

**症状**: 后端服务未收到代理头部

**解决方案**:

检查 Caddyfile 中的头部配置：

```
header X-Forwarded-For {remote}
header X-Forwarded-Proto {scheme}
```

重启 Caddy：
```bash
docker compose restart caddy
```

### 7. Windows WSL2 问题

**症状**: 在 WSL2 中无法访问服务

**解决方案**:

1. 确保 Docker Desktop WSL2 集成已启用
2. 在 WSL2 中使用 `localhost` 访问
3. 如需从 Windows 主机访问，使用 WSL2 IP：

```bash
# 获取 WSL2 IP
hostname -I | awk '{print $1}'
```

然后在 Windows 浏览器中访问 `http://<WSL2-IP>:45000`

### 8. Docker 网络问题

**症状**: 容器间无法通信

**解决方案**:

本配置使用 `network_mode: host`，确保：
- 宿主机上没有其他服务占用 45000, 45443, 45001 端口
- 检查防火墙设置

## 调试命令

### 查看服务状态
```bash
docker compose ps
```

### 查看实时日志
```bash
# 所有服务
docker compose logs -f

# 特定服务
docker compose logs -f caddy
docker compose logs -f backend
```

### 进入容器
```bash
# 进入 Caddy 容器
docker exec -it caddy-proxy sh

# 进入后端容器
docker exec -it backend-service sh
```

### 检查端口监听
```bash
ss -tlnp | grep -E '45000|45443|45001'
```

### 测试端到端连接
```bash
# HTTP
curl -v http://localhost:45000/

# HTTPS
curl -k -v https://localhost:45443/

# 直接访问后端
curl http://localhost:45001/
```

## 获取帮助

如果以上方案无法解决问题：

1. 查看完整日志：`docker compose logs`
2. 检查 Docker 状态：`docker info`
3. 重启 Docker Desktop/Engine
4. 清理并重新创建：
   ```bash
   docker compose down -v
   docker compose up -d
   ```
