# 系统与同步架构

## 拓扑

```text
Windows桌面端 ─┐
Android手机端 ──┼─ 局域网 HTTP API ─ Nginx ─ ASP.NET Core ─ MySQL
HarmonyOS手机端 ┘

Windows/Android：MAUI界面 + SQLite缓存 + 待同步队列
HarmonyOS：ArkUI界面 + Preferences应用私有缓存 + 待同步队列
```

PC和手机不直接互相传数据库。二维码只让手机取得同一个服务器地址和短效配对授权；配对后两端都与服务器同步，因此不会依赖PC一直开机。

## 为什么这样设计

- MySQL是唯一权威数据，避免PC数据库与手机数据库互相覆盖。
- SQLite让PC或手机临时离开局域网时仍可继续记录。
- 服务端变更流水使用全局递增`sequence`，客户端只拉取上次序号之后的变化。
- 每条业务记录保留`row_version`用于同步序列与幂等判断，并保留UTC格式`updatedAt`作为合并依据。
- 同一记录出现多端修改时采用Last Write Wins：`updatedAt`较新的版本覆盖较旧版本；时间完全相同时保留服务器版本。
- 每个客户端变更有`mutation_id`，网络重试不会重复写入。

## 二维码配对

1. PC使用账号密码登录服务器。
2. PC向服务器申请5分钟一次性票据。
3. PC显示`pwb://pair?...`二维码。
4. Android或HarmonyOS手机扫码后直接向服务器兑换票据。
5. 服务器标记票据已使用，为手机签发独立设备令牌。
6. 手机先推送离线修改，服务器按`updatedAt`合并，再拉取所有设备的最新状态。

二维码不包含账号密码、MySQL连接串或长期访问令牌。

## 多端合并规则

个人单用户仍可能在PC、Android和HarmonyOS离线时修改同一项目。本版本采用实体级Last Write Wins：

1. 客户端每次本地修改都更新UTC格式`updatedAt`并进入待同步队列。
2. 连接服务器后先上传本地队列；服务器比较同一实体的客户端与服务器`updatedAt`。
3. 客户端时间更新则写入MySQL并生成新变更序号；服务器时间更新或相同则保留服务器内容。
4. 客户端随后按`sequence`拉取，最终让PC、Android、HarmonyOS与服务器收敛到同一最新版本。

因此设备时间应保持系统自动校时。若手机或PC时间被手工调到未来，该设备的修改可能被误判为最新。
