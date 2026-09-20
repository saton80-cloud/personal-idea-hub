# HarmonyOS 7 构建与安装

本文适用于系统版本`HarmonyOS 7.0.0.107`。客户端是原生ArkTS/ArkUI Stage工程，不是Android APK，也不依赖Android兼容层。

## 已实现范围

- 账号密码登录现有局域网服务器
- 调用系统扫码界面读取PC端一次性二维码
- `pwb://pair?server=...&ticket=...`粘贴配对备用入口
- 今日工作、优先级、预计时间与完成状态
- 代码、网站、产品创意、新品开发、日常工作和普通点子看板
- 项目内新增子创意、项目进度推进
- 应用私有离线缓存、待同步队列、双向增量同步与冲突提示
- 上午计划和晚间复盘的应用内提醒

系统后台定时通知、冲突三方合并和验收多分支的完整编辑器不在本次鸿蒙适配范围内。

## 环境准备

1. 在华为开发者官网下载支持HarmonyOS 7的最新版DevEco Studio。
2. 登录你的华为开发者账号并安装HarmonyOS SDK。工程的编译SDK为`6.0.0(20)`、最低兼容SDK为`5.0.0(12)`，可运行于HarmonyOS 7。
3. 手机与开发电脑通过USB或无线调试连接，并在手机开发者选项中允许调试。
4. 确保手机、Windows PC与Rocky Linux服务器处在同一局域网；服务器健康检查地址应可从手机浏览器访问，例如`http://192.168.1.100/api/health`。

## 导入与签名

1. 在DevEco Studio选择`Open`，打开仓库中的`src/PersonalWorkBoard.Harmony`目录。
2. 等待Hvigor和ohpm同步完成。如果本机只安装了更新SDK，可在Project Structure中将compile/target SDK切换为本机已安装的稳定版本，最低兼容版本保持不高于设备SDK。
3. 打开`File > Project Structure > Signing Configs`。
4. 为`entry`模块启用自动签名，并选择已登录的华为开发者账号。
5. 不要把`.p12`、`.cer`、`.p7b`、`.jks`、私钥或签名密码提交到GitHub。

HAP必须签名后才能安装。签名证书、Profile和设备授权与开发者账号关联，因此仓库不提供通用签名HAP。

## 真机运行

1. 在设备选择器中选中你的HarmonyOS 7手机。
2. 选择`entry`模块和`default`产品。
3. 点击Run。DevEco Studio会编译、签名、安装并启动应用。
4. 首次打开后，可直接输入服务器地址、用户名和密码；也可以在PC端生成二维码，然后点击“扫一扫配对”。

默认扫描组件由系统提供扫码UI；同时保留粘贴配对内容的入口，便于相机或系统扫码异常时诊断。

## 生成HAP

1. 确认`entry`签名配置有效。
2. 选择`Build > Build Hap(s)/APP(s) > Build Hap(s)`。
3. 构建完成后，在`entry/build/default/outputs/default/`查找HAP；具体子目录可能随DevEco Studio版本变化。
4. 调试HAP只适合已登记的开发设备；需要长期分发时应改用正式发布证书并按华为流程签名。

## 与服务器配对

1. 在Windows端登录后进入同步中心，生成5分钟一次性二维码。
2. 鸿蒙端点击“扫一扫配对”，扫码后会从二维码取得服务器地址与一次性票据。
3. 鸿蒙端直接向Rocky Linux服务器兑换独立设备令牌，二维码随即失效。
4. 手机会上传本地待处理修改，再按变更序号拉取服务器最新数据。

PC只负责展示二维码，不是数据中转站；配对后即使PC关闭，只要手机能访问服务器仍可同步。

## 常见问题

### 手机提示无法连接服务器

- 用手机浏览器访问`http://服务器IP/api/health`。
- 确认手机没有使用访客Wi-Fi或开启客户端隔离。
- 确认Rocky Linux防火墙允许局域网访问Nginx的80端口。
- 二维码中的服务器地址必须是手机可访问的局域网IP，不能是`localhost`或`127.0.0.1`。

### 二维码无效

配对票据只有5分钟有效期且只能使用一次。请在PC重新生成，不要重复使用截图中的旧票据。

### DevEco提示没有签名

回到Project Structure的Signing Configs，为`entry/default`启用自动签名；确认华为账号已登录、网络可用且手机已被开发环境识别。

### 为什么仓库里没有可直接安装的HAP

HarmonyOS安装包必须使用开发者身份签名，调试Profile还会绑定开发设备。把其他人的签名HAP直接交付既无法保证安装，也会泄露或混用签名身份。因此仓库交付可审计源码，由你的DevEco Studio生成属于你的HAP。

## 发布前验收

按[验收清单](08-acceptance-checklist.md)完成真机扫码、首次同步、离线新增、恢复网络、冲突和界面检查后，再保留签名HAP作为个人安装包。

## 官方参考

- [使用ArkTS开发Stage模型应用](https://developer.huawei.com/consumer/cn/doc/harmonyos-guides-V5/start-with-ets-stage-V5)
- [应用/服务签名说明](https://developer.huawei.com/consumer/cn/doc/HMSCore-Guides/harmonyos-java-config-app-signing-0000001199536987)
- [默认扫码界面](https://developer.huawei.com/consumer/cn/doc/harmonyos-guides/scan-scanbarcode)
