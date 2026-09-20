# 开发和发布

## 分支与版本

- `main`：可发布版本
- `develop`：集成版本
- 功能分支：`feat/<task>`
- 修复分支：`fix/<task>`

## 提交前

```bash
dotnet restore src/PersonalWorkBoard.Server/PersonalWorkBoard.Server.csproj
dotnet test tests/PersonalWorkBoard.Domain.Tests/PersonalWorkBoard.Domain.Tests.csproj -c Release
dotnet build src/PersonalWorkBoard.Server/PersonalWorkBoard.Server.csproj -c Release
```

MAUI客户端必须在安装对应workload的Windows构建机完成Windows和Android双目标编译。

HarmonyOS源码可在任意平台进行结构检查：

```bash
node scripts/validate-harmony-project.mjs
```

HAP必须在安装HarmonyOS SDK的DevEco Studio中编译，并使用开发者账号的调试或发布签名。源码检查不能代替ArkTS编译和真机验收。

同步实现必须保持以下顺序：本地写入与排队 → 推送待同步变更 → 服务端按`updatedAt`执行Last Write Wins → 客户端按`sequence`拉取最终状态。不要把拉取放到推送之前，否则服务器旧值可能覆盖尚未上传的离线修改。

## GitHub Actions产物

- `personal-work-board-server-linux-x64`
- `personal-work-board-windows-x64`
- `personal-work-board-android-apk`
- HarmonyOS源码检查（HAP不上传到公开仓库，避免混入个人签名材料）

发布前应在真实Windows 10/11、至少一台Android手机、一台HarmonyOS 7手机和Rocky Linux 8.9服务器完成验收。
