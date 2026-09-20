import { readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const root = process.cwd();
const harmonyRoot = join(root, 'src', 'PersonalWorkBoard.Harmony');
const requiredFiles = [
  'AppScope/app.json5',
  'build-profile.json5',
  'hvigorfile.ts',
  'hvigor/hvigor-config.json5',
  'oh-package.json5',
  'hvigor/hvigor-config.json5',
  'entry/build-profile.json5',
  'entry/hvigorfile.ts',
  'entry/oh-package.json5',
  'entry/src/main/module.json5',
  'entry/src/main/resources/base/profile/main_pages.json',
  'entry/src/main/ets/entryability/EntryAbility.ets',
  'entry/src/main/ets/models/Contracts.ets',
  'entry/src/main/ets/services/StorageService.ets',
  'entry/src/main/ets/services/ApiClient.ets',
  'entry/src/main/ets/services/SyncService.ets',
  'entry/src/main/ets/pages/Index.ets'
];

const failures = [];
for (const relativePath of requiredFiles) {
  if (!existsSync(join(harmonyRoot, relativePath))) {
    failures.push(`缺少文件：${relativePath}`);
  }
}

const jsonFiles = [
  'AppScope/app.json5',
  'AppScope/resources/base/element/string.json',
  'build-profile.json5',
  'oh-package.json5',
  'entry/build-profile.json5',
  'entry/oh-package.json5',
  'entry/src/main/module.json5',
  'entry/src/main/resources/base/element/string.json',
  'entry/src/main/resources/base/element/color.json',
  'entry/src/main/resources/base/profile/main_pages.json'
];
for (const relativePath of jsonFiles) {
  try {
    JSON.parse(readFileSync(join(harmonyRoot, relativePath), 'utf8'));
  } catch (error) {
    failures.push(`JSON5基础结构无法解析：${relativePath} (${error.message})`);
  }
}

if (failures.length === 0) {
  const moduleText = readFileSync(join(harmonyRoot, 'entry/src/main/module.json5'), 'utf8');
  const apiText = readFileSync(join(harmonyRoot, 'entry/src/main/ets/services/ApiClient.ets'), 'utf8');
  const syncText = readFileSync(join(harmonyRoot, 'entry/src/main/ets/services/SyncService.ets'), 'utf8');
  const uiText = readFileSync(join(harmonyRoot, 'entry/src/main/ets/pages/Index.ets'), 'utf8');

  const requiredFragments = [
    [moduleText, 'ohos.permission.INTERNET', '缺少局域网网络权限'],
    [apiText, '/api/auth/login', '缺少登录接口'],
    [apiText, '/api/pairing/redeem', '缺少二维码兑换接口'],
    [apiText, '/api/sync/pull', '缺少增量拉取接口'],
    [apiText, '/api/sync/push', '缺少增量推送接口'],
    [apiText, "devicePlatform: 'HarmonyOS'", '设备平台标识不正确'],
    [syncText, "queueMutation('WorkItem'", '工作项目未进入待同步队列'],
    [syncText, "queueMutation('WorkTask'", '今日工作未进入待同步队列'],
    [uiText, "pwb://pair?", '缺少个人工作看板二维码协议'],
    [uiText, '新增子创意', '缺少子创意入口'],
    [uiText, '今日工作', '缺少今日工作界面'],
    [uiText, '同步中心', '缺少同步中心界面']
  ];
  for (const [text, fragment, message] of requiredFragments) {
    if (!text.includes(fragment)) {
      failures.push(message);
    }
  }
}

if (failures.length > 0) {
  console.error(failures.join('\n'));
  process.exit(1);
}

console.log(`HarmonyOS source validation passed (${requiredFiles.length} required files).`);
