# 个人工作看板

一个面向个人使用的局域网多端工作与项目管理系统。

- Windows桌面端：C# / .NET MAUI
- Android手机端：C# / .NET MAUI
- HarmonyOS手机端：ArkTS / ArkUI原生Stage模型（面向HarmonyOS 7）
- 局域网服务器：ASP.NET Core / .NET 10 LTS
- 中心数据库：MySQL 8
- 部署系统：Rocky Linux 8.9
- 数据同步：本地SQLite缓存 + MySQL权威数据 + 双向增量同步

## 当前范围 V0.3.0

- 产品统一名称改为“个人工作看板”
- 管理代码、网站、产品创意、新品开发、普通点子和日常工作
- 今日工作、优先级、预计耗时和每日计划/复盘提醒
- 项目看板、进度、阻塞、下一步、子创意、版本分支和阶段验收
- Windows与Android离线记录，恢复局域网后自动同步
- PC生成5分钟一次性二维码，Android扫码安全配对
- HarmonyOS 7原生客户端支持扫码/粘贴配对、离线记录与双向增量同步
- Android首次安装无需服务器即可离线进入，后续扫码时再上传本地数据并合并
- 多端修改同一记录时按`updatedAt`自动合并，最新修改覆盖旧版本
- AI分析中心暂停，不进入本版本导航、接口和数据库

## 目录

```text
src/PersonalWorkBoard.Domain       领域模型与同步协议
src/PersonalWorkBoard.Contracts    API数据合同
src/PersonalWorkBoard.Server       Rocky Linux上的ASP.NET Core服务
src/PersonalWorkBoard.Client       Windows/Android共享C#客户端
src/PersonalWorkBoard.Harmony      HarmonyOS 7原生ArkTS/ArkUI客户端
tests/                             自动化测试
deploy/rocky8                      安装、升级、备份脚本
docs/                              产品、架构、安装、维护和验收文档
```

## 开发构建

要求 .NET 10 SDK。Windows与Android客户端还需对应MAUI workload。

```bash
dotnet restore src/PersonalWorkBoard.Server/PersonalWorkBoard.Server.csproj
dotnet test tests/PersonalWorkBoard.Domain.Tests/PersonalWorkBoard.Domain.Tests.csproj -c Release
dotnet publish src/PersonalWorkBoard.Server/PersonalWorkBoard.Server.csproj -c Release -r linux-x64 --self-contained true -o publish/server
```

Windows构建：

```powershell
dotnet workload install maui-windows
dotnet publish src/PersonalWorkBoard.Client/PersonalWorkBoard.Client.csproj -f net10.0-windows10.0.19041.0 -c Release -p:RuntimeIdentifierOverride=win-x64 -p:WindowsPackageType=None
```

Android构建：

```powershell
dotnet workload install maui-android
dotnet publish src/PersonalWorkBoard.Client/PersonalWorkBoard.Client.csproj -f net10.0-android -c Release -p:AndroidPackageFormats=apk
```

HarmonyOS构建：

1. 使用支持HarmonyOS 7的最新版DevEco Studio打开`src/PersonalWorkBoard.Harmony`。
2. 在Project Structure中为`entry`启用自动签名。
3. 连接HarmonyOS 7手机后运行`entry`，或通过`Build > Build Hap(s)/APP(s) > Build Hap(s)`生成HAP。

完整步骤见[HarmonyOS 7构建与安装](docs/09-harmonyos7-build-install.md)。

## 部署

GitHub Actions会生成Linux服务端、Windows桌面端和Android APK三个Artifact，并执行HarmonyOS源码/接口接线检查。HarmonyOS HAP必须使用开发者自己的签名证书，在DevEco Studio中构建。服务器部署见[安装文档](docs/03-rocky-installation.md)。

## 文档

- [产品范围](docs/01-product-scope.md)
- [系统与同步架构](docs/02-architecture-and-sync.md)
- [Rocky Linux 8.9安装](docs/03-rocky-installation.md)
- [PC、Android与HarmonyOS使用手册](docs/04-user-guide.md)
- [维护与备份](docs/05-maintenance.md)
- [开发和发布](docs/06-development-release.md)
- [安全说明](docs/07-security.md)
- [验收清单](docs/08-acceptance-checklist.md)
- [HarmonyOS 7构建与安装](docs/09-harmonyos7-build-install.md)

## 当前交付状态

V0.3.0新增HarmonyOS 7原生工程。现有GitHub Actions已验证.NET领域测试、Linux服务端发布、Windows发布与Android APK；HarmonyOS工程已完成源码与接口静态检查，仍需在DevEco Studio中使用个人签名并在`HarmonyOS 7.0.0.107`真机上完成编译、安装、扫码和断网重连验收。
