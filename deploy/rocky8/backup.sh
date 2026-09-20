#!/usr/bin/env bash
set -Eeuo pipefail

if [[ ${EUID} -ne 0 ]]; then echo "请使用root运行"; exit 1; fi
BACKUP_ROOT=/var/backups/personal-work-board
STAMP=$(date +%Y%m%d-%H%M%S)
install -d -m 0750 "${BACKUP_ROOT}"
set -a
source /etc/personal-work-board/server.env
set +a
DB_PASSWORD=$(printf '%s' "${ConnectionStrings__Main}" | sed -n 's/.*Password=\([^;]*\).*/\1/p')
MYSQL_PWD="${DB_PASSWORD}" mysqldump -h127.0.0.1 -upwb_app --single-transaction --routines --triggers personal_work_board | gzip -9 >"${BACKUP_ROOT}/personal-work-board-${STAMP}.sql.gz"
find "${BACKUP_ROOT}" -type f -name 'personal-work-board-*.sql.gz' -mtime +30 -delete
echo "备份完成：${BACKUP_ROOT}/personal-work-board-${STAMP}.sql.gz"
