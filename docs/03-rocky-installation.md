# Rocky Linux 8.9 安装

## 前提

- Rocky Linux 8.9 x86_64
- root权限
- 固定局域网IP
- PC、Android和服务器可以互相访问
- 已从GitHub Actions下载`personal-work-board-server-linux-x64`

## 安装步骤

1. 解压完整项目发布包。
2. 将Linux服务端Artifact解压到项目根目录`publish/server/`。
3. 执行：

```bash
chmod +x deploy/rocky8/*.sh
sudo bash deploy/rocky8/install.sh
```

4. 按提示输入服务器局域网IP、管理员密码和MySQL应用密码。
5. 安装结束后访问：

```bash
curl http://服务器IP/api/health
```

## 安装内容

- `/opt/personal-work-board`：服务端程序
- `/etc/personal-work-board/server.env`：服务端密钥配置，权限0600
- `personal-work-board.service`：systemd服务
- Nginx 80端口反向代理，客户端使用IP且无需端口
- firewalld开放HTTP，不开放MySQL与5088端口
- 每日02:30 MySQL自动备份，保留30天

## 网络

服务器建议使用DHCP静态租约或固定IP。手机必须连接与服务器互通的Wi-Fi；访客Wi-Fi常启用客户端隔离，不能使用。
