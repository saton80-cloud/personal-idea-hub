#!/usr/bin/env bash
set -Eeuo pipefail

if [[ ${EUID} -ne 0 ]]; then
  echo "请使用 root 运行：sudo bash install.sh"
  exit 1
fi

APP_DIR=/opt/personal-work-board
DATA_DIR=/var/lib/personal-work-board
ENV_FILE=/etc/personal-work-board/server.env
SERVICE_FILE=/etc/systemd/system/personal-work-board.service
NGINX_FILE=/etc/nginx/conf.d/personal-work-board.conf
SOURCE_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
PUBLISH_DIR=${PWB_SERVER_PUBLISH_DIR:-${SOURCE_DIR}/publish/server}

if [[ ! -x "${PUBLISH_DIR}/PersonalWorkBoard.Server" ]]; then
  echo "缺少Linux服务端发布文件：${PUBLISH_DIR}/PersonalWorkBoard.Server"
  echo "请先从GitHub Actions下载 server-linux-x64，并解压到 publish/server。"
  exit 1
fi

read -r -p "服务器局域网IP（例如 192.168.1.100）: " SERVER_IP
read -r -p "管理员用户名 [admin]: " ADMIN_USER
ADMIN_USER=${ADMIN_USER:-admin}
read -r -s -p "管理员密码（至少8位，仅字母数字及 @%_+=:,.!~-）: " ADMIN_PASSWORD
echo
read -r -s -p "MySQL应用账号密码（至少12位，仅字母数字及 @%_+=:,.!~-）: " DB_PASSWORD
echo

if [[ ${#ADMIN_PASSWORD} -lt 8 || ${#DB_PASSWORD} -lt 12 ]]; then
  echo "密码长度不符合要求。"
  exit 1
fi
if [[ ! ${ADMIN_USER} =~ ^[A-Za-z0-9._-]{1,80}$ ]]; then
  echo "管理员用户名仅允许字母、数字、点、下划线和短横线。"
  exit 1
fi
if [[ ! ${ADMIN_PASSWORD} =~ ^[A-Za-z0-9@%_+=:,\.\!~-]+$ || ! ${DB_PASSWORD} =~ ^[A-Za-z0-9@%_+=:,\.\!~-]+$ ]]; then
  echo "密码包含安装脚本不允许的字符。"
  exit 1
fi
if [[ ! ${SERVER_IP} =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ ]]; then
  echo "服务器IP格式不正确。"
  exit 1
fi

dnf install -y nginx firewalld policycoreutils-python-utils
if ! command -v mysqld >/dev/null 2>&1; then
  dnf install -y mysql-server
fi

systemctl enable --now mysqld
systemctl enable --now nginx
systemctl enable --now firewalld

DB_PASSWORD_SQL=${DB_PASSWORD//\'/\'\'}
mysql -uroot <<SQL
CREATE DATABASE IF NOT EXISTS personal_work_board CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'pwb_app'@'127.0.0.1' IDENTIFIED BY '${DB_PASSWORD_SQL}';
ALTER USER 'pwb_app'@'127.0.0.1' IDENTIFIED BY '${DB_PASSWORD_SQL}';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES ON personal_work_board.* TO 'pwb_app'@'127.0.0.1';
FLUSH PRIVILEGES;
SQL

id pwb >/dev/null 2>&1 || useradd --system --home-dir "${APP_DIR}" --shell /sbin/nologin pwb
install -d -o pwb -g pwb -m 0750 "${APP_DIR}" "${DATA_DIR}" /etc/personal-work-board
cp -a "${PUBLISH_DIR}/." "${APP_DIR}/"
chown -R pwb:pwb "${APP_DIR}" "${DATA_DIR}"

cat >"${ENV_FILE}" <<EOF
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Main='Server=127.0.0.1;Port=3306;Database=personal_work_board;UserID=pwb_app;Password=${DB_PASSWORD};CharacterSet=utf8mb4;SslMode=Preferred;'
Bootstrap__AdminUserName=${ADMIN_USER}
Bootstrap__AdminDisplayName='我的工作看板'
Bootstrap__AdminPassword='${ADMIN_PASSWORD}'
Server__PublicBaseUrl=http://${SERVER_IP}
Server__Name='个人工作看板局域网服务器'
EOF
chmod 0600 "${ENV_FILE}"

cat >"${SERVICE_FILE}" <<'EOF'
[Unit]
Description=Personal Work Board LAN Server
After=network-online.target mysqld.service
Wants=network-online.target
Requires=mysqld.service

[Service]
Type=simple
User=pwb
Group=pwb
WorkingDirectory=/opt/personal-work-board
EnvironmentFile=/etc/personal-work-board/server.env
ExecStart=/opt/personal-work-board/PersonalWorkBoard.Server
Restart=on-failure
RestartSec=5
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/personal-work-board

[Install]
WantedBy=multi-user.target
EOF

cat >"${NGINX_FILE}" <<EOF
server {
    listen 80;
    server_name ${SERVER_IP};
    client_max_body_size 10m;

    location / {
        proxy_pass http://127.0.0.1:5088;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_connect_timeout 10s;
        proxy_read_timeout 60s;
    }
}
EOF

nginx -t
restorecon -Rv "${APP_DIR}" "${DATA_DIR}" /etc/personal-work-board >/dev/null || true
setsebool -P httpd_can_network_connect 1
firewall-cmd --permanent --add-service=http
firewall-cmd --reload
systemctl daemon-reload
systemctl enable --now personal-work-board
systemctl restart nginx

install -m 0750 "${SOURCE_DIR}/deploy/rocky8/backup.sh" /usr/local/sbin/pwb-backup
install -m 0644 "${SOURCE_DIR}/deploy/rocky8/personal-work-board-backup.service" /etc/systemd/system/personal-work-board-backup.service
install -m 0644 "${SOURCE_DIR}/deploy/rocky8/personal-work-board-backup.timer" /etc/systemd/system/personal-work-board-backup.timer
systemctl daemon-reload
systemctl enable --now personal-work-board-backup.timer

sleep 2
curl --fail --silent "http://127.0.0.1/api/health" >/dev/null
echo "安装完成。PC端服务器地址：http://${SERVER_IP}"
echo "请确保手机、PC和服务器位于同一局域网。"
