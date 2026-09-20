import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const root = process.cwd();
const files = {
  server: readFileSync(join(root, 'src/PersonalWorkBoard.Server/Sync/SyncService.cs'), 'utf8'),
  policy: readFileSync(join(root, 'src/PersonalWorkBoard.Domain/Sync/LastWriteWinsPolicy.cs'), 'utf8'),
  app: readFileSync(join(root, 'src/PersonalWorkBoard.Client/App.xaml.cs'), 'utf8'),
  api: readFileSync(join(root, 'src/PersonalWorkBoard.Client/Services/ApiClient.cs'), 'utf8'),
  login: readFileSync(join(root, 'src/PersonalWorkBoard.Client/Pages/LoginPage.xaml'), 'utf8'),
  startup: readFileSync(join(root, 'src/PersonalWorkBoard.Client/Pages/StartupPage.xaml.cs'), 'utf8'),
  scanner: readFileSync(join(root, 'src/PersonalWorkBoard.Client/Pages/QrScannerPage.xaml.cs'), 'utf8'),
  harmony: readFileSync(join(root, 'src/PersonalWorkBoard.Harmony/entry/src/main/ets/pages/Index.ets'), 'utf8')
};

const checks = [
  [files.policy, 'incomingUpdatedAt.ToUniversalTime() > serverUpdatedAt.ToUniversalTime()', 'LWW策略必须比较UTC更新时间'],
  [files.server, 'LastWriteWinsPolicy.IncomingWins(incoming.UpdatedAt, current.UpdatedAt)', '服务端WorkItem/WorkTask未接入LWW策略'],
  [files.server, 'UpdatedAt = incoming.UpdatedAt.ToUniversalTime()', '服务端必须保留客户端实际修改时间'],
  [files.app, 'Connectivity.ConnectivityChanged += OnConnectivityChanged', 'MAUI客户端未监听网络恢复'],
  [files.app, 'window.Resumed +=', 'MAUI客户端未在回到前台时同步'],
  [files.scanner, 'await _sync.SyncNowAsync()', 'Android扫码后未立即同步'],
  [files.api, 'EnableOfflineModeAsync', 'Android缺少首次离线模式状态'],
  [files.startup, 'CanEnterWorkspaceAsync()', 'Android重启后不能恢复离线工作台'],
  [files.login, '暂不连接服务器，离线进入', 'Android登录页缺少首次离线入口'],
  [files.harmony, 'await this.synchronize(false)', 'HarmonyOS扫码/启动后未立即同步'],
  [files.harmony, 'setInterval(', 'HarmonyOS前台未定时重试同步']
];

const failures = [];
for (const [text, fragment, message] of checks) {
  if (!text.includes(fragment)) {
    failures.push(message);
  }
}

if ((files.server.match(/LastWriteWinsPolicy\.IncomingWins/g) ?? []).length !== 2) {
  failures.push('LWW策略必须同时应用于WorkItem和WorkTask');
}

if (failures.length > 0) {
  console.error(failures.join('\n'));
  process.exit(1);
}

console.log('Offline sync and last-write-wins source validation passed.');
