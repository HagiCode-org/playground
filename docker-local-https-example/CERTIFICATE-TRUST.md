# 浏览器信任自签名证书指南

由于 Caddy 使用自签名证书（内部 CA），浏览器默认不信任这些证书。本指南将帮助你信任这些证书以便本地开发。

## 提取证书

首先，需要从 Caddy 容器中提取证书：

```bash
# 复制根证书
docker cp caddy-proxy:/data/caddy/pki/authorities/local/root.crt ./local-ca.crt
```

## macOS

### 方法 1: 通过钥匙串访问（推荐）

1. 打开 **钥匙串访问** (Keychain Access)
2. 选择 **系统** 钥匙串
3. 将 `local-ca.crt` 拖入钥匙串
4. 双击导入的证书
5. 展开 **信任** 部分
6. 将 **使用此证书时** 改为 **始终信任**

### 方法 2: 命令行

```bash
# 添加到系统钥匙串（需要 sudo）
sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain ./local-ca.crt

# 移除证书（如需要）
# sudo security remove-trusted-cert -d ./local-ca.crt
```

### Safari/Chrome

Safari 和 Chrome（使用系统钥匙串）重启后会自动信任。

### Firefox

Firefox 有自己的证书存储：

1. 打开 Firefox 设置
2. 搜索 "证书" → 隐私与安全 → 证书
3. 点击 **查看证书**
4. 选择 **证书颁发机构** 标签
5. 点击 **导入**
6. 选择 `local-ca.crt`
7. 勾选 **信任此证书用于识别网站**

## Windows

### 方法 1: 通过 PowerShell（推荐）

```powershell
# 以管理员身份运行 PowerShell
Import-Certificate -FilePath ".\local-ca.crt" -CertStoreLocation Cert:\LocalMachine\Root
```

### 方法 2: 通过 mmc

1. 按 `Win + R`，输入 `mmc`
2. 文件 → 添加/删除管理单元
3. 选择 **证书** → 添加 → 计算机账户 → 本地计算机
4. 展开 **受信任的根证书颁发机构** → 证书
5. 右键 → 所有任务 → 导入
6. 选择 `local-ca.crt`

### Edge/Chrome

Windows 存储证书被 Edge 和 Chrome 共享，重启后生效。

### Firefox

Firefox 需要手动导入（同 macOS）：
1. 设置 → 隐私与安全 → 证书 → 查看证书
2. 导入 → 信任此证书

## Linux

### Ubuntu/Debian

```bash
# 复制到系统证书目录
sudo cp local-ca.crt /usr/local/share/ca-certificates/local-ca.crt

# 更新证书存储
sudo update-ca-certificates
```

### Fedora/RHEL

```bash
# 复制到系统证书目录
sudo cp local-ca.crt /etc/pki/ca-trust/source/anchors/

# 更新证书存储
sudo update-ca-trust
```

### Chrome

Chrome 在 Linux 上使用系统证书存储。执行上述命令后重启 Chrome。

### Firefox

Firefox 在 Linux 上也需要手动导入（同 macOS）。

## 验证

信任证书后，访问 https://localhost:45443/ 应该不再显示证书警告。

```bash
curl -k https://localhost:45443/
```

## 注意事项

- **自签名证书仅用于本地开发**
- 证书有有效期（默认 24 小时），但会自动续期
- 如果看到证书警告，检查是否正确导入了根证书
- 每次新建证书后需要重新导入根证书

## 故障排除

如果仍然看到证书警告：

1. 确认导入的是根证书（`root.crt`），不是服务器证书（`localhost.crt`）
2. 重启浏览器
3. 清除浏览器缓存
4. 检查系统时间是否正确
