#!/usr/bin/env bash
set -Eeuo pipefail

if [[ ${EUID} -ne 0 ]]; then echo "请使用root运行"; exit 1; fi
APP_DIR=/opt/personal-work-board
SOURCE_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
PUBLISH_DIR=${PWB_SERVER_PUBLISH_DIR:-${SOURCE_DIR}/publish/server}
BACKUP_DIR=/var/backups/personal-work-board/$(date +%Y%m%d-%H%M%S)

if [[ ! -x "${PUBLISH_DIR}/PersonalWorkBoard.Server" ]]; then echo "缺少新的服务端发布文件"; exit 1; fi
install -d -m 0750 "${BACKUP_DIR}"
systemctl stop personal-work-board
cp -a "${APP_DIR}" "${BACKUP_DIR}/server"
cp -a "${PUBLISH_DIR}/." "${APP_DIR}/"
chown -R pwb:pwb "${APP_DIR}"
systemctl start personal-work-board
sleep 2
if ! curl --fail --silent http://127.0.0.1/api/health >/dev/null; then
  systemctl stop personal-work-board
  cp -a "${BACKUP_DIR}/server/." "${APP_DIR}/"
  chown -R pwb:pwb "${APP_DIR}"
  systemctl start personal-work-board
  echo "升级健康检查失败，已自动回退。备份：${BACKUP_DIR}"
  exit 1
fi
echo "升级完成。备份：${BACKUP_DIR}"
