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

## GitHub Actions产物

- `personal-work-board-server-linux-x64`
- `personal-work-board-windows-x64`
- `personal-work-board-android-apk`

发布前应在真实Windows 10/11、至少一台Android手机和Rocky Linux 8.9服务器完成验收。
