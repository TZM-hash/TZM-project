# Windows Server + IIS 部署手册

1. 安装与 .NET 10 匹配的 ASP.NET Core Hosting Bundle，并启用 IIS WebSocket、静态内容和 Windows 身份验证（如采用集成认证）。
2. 创建独立应用池，设为“无托管代码”，站点目录使用 `artifacts/publish` 的正式复制件；为应用池账号授予站点读取、`App_Data` 写入权限。
3. 通过 IIS 配置或环境变量设置 `ConnectionStrings__DefaultConnection`，禁止复制 Development 测试连接字符串和测试账号。
4. 绑定正式域名和 HTTPS 证书，关闭不必要的 HTTP 入口；生产环境设置 `ASPNETCORE_ENVIRONMENT=Production`。
5. 上线前备份正式数据库和附件；由运维人工执行已审核的 EF Migration。发布脚本不会连接生产数据库或自动迁移。
6. 验证 `/health/live`、`/health/ready`、登录、附件目录、日志和备份目录权限。
7. 回滚时停止站点，恢复上一发布目录；若数据库发生不可逆迁移，按对应备份恢复，不得用测试库覆盖。
