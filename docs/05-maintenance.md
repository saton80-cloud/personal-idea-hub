# 维护与备份

## 常用命令

```bash
systemctl status personal-work-board
journalctl -u personal-work-board -n 200 --no-pager
curl http://127.0.0.1/api/health
systemctl status mysqld nginx
```

## 手动备份

```bash
sudo /usr/local/sbin/pwb-backup
```

备份目录：`/var/backups/personal-work-board`，默认保留30天。

## 升级

下载新的Linux服务端Artifact并解压到`publish/server`后运行：

```bash
sudo bash deploy/rocky8/upgrade.sh
```

脚本先备份旧程序，再更新并执行健康检查；失败时自动恢复旧版本。数据库升级必须使用新增迁移，禁止修改已执行迁移。

## 密钥和设备

- `/etc/personal-work-board/server.env`仅root可读。
- 手机丢失时应在数据库或后续设备管理页撤销对应设备和会话。
- 不要通过微信、邮件或Git提交服务器密码。
