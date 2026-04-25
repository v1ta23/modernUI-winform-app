# 工业设备点检系统

> C# WinForms | .NET 8 | SQL Server | TCP 通信 | AI 分析

面向制造业的设备点检桌面应用，覆盖设备台账、点检记录、报警闭环、CSV 数据导入、风险看板、TCP 设备通信与 AI 智能分析，将点检数据转化为风险结论。

## 页面展示

<img width="1372" height="1280" alt="Snipaste_2026-04-26_03-04-09" src="https://github.com/user-attachments/assets/b06bba97-47d5-44c9-958b-e48fdc5fb6df" />
<img width="1581" height="1011" alt="Snipaste_2026-04-26_02-59-02" src="https://github.com/user-attachments/assets/10e2a31d-0338-4e97-887e-390cb1d3988a" />



## 功能模块

| 模块    | 说明                       |
| ----- | ------------------------ |
| 首页总览  | 关键指标、近期动态和快捷入口           |
| 用户登录  | 注册、登录、记住密码               |
| 设备台账  | 设备基础信息、产线归属和状态维护         |
| 设备监控  | 设备运行状态查看                 |
| 报警中心  | 异常记录汇总，支持闭环处理            |
| 巡检管理  | 新增、编辑、筛选、撤回、闭环点检记录       |
| 数据导入  | CSV 批量导入，含字段校验           |
| 风险看板  | 趋势图、异常分布、产线风险和待关注记录      |
| 通信测试  | TCP 连接测试，支持发送指令和读取设备回复   |
| AI 分析 | 生成风险等级、原因分析、责任建议和处理时限    |
| AI 协同 | 按设备部、生产部、质量部、管理层视角分别生成建议 |
| AI 历史 | 保存最近 50 次 AI 记录，支持复盘和复制  |

## 技术栈

- **语言与框架**：C# · .NET 8 · WinForms
- **数据库**：SQL Server
- **数据访问**：ADO.NET
- **通信**：TCP Socket
- **AI 接口**：大语言模型 API
- **数据处理**：ClosedXML
- **界面**：暗色主题 · 自绘图表 · 双缓冲绘制

## 项目结构

```
App.Core            业务模型、接口、点检与风险分析领域逻辑
App.Infrastructure  SQL Server 仓储、AI 接口服务
WinFormsApp         界面、控制器、视图模型、导入导出
```

## 运行方式

1. 安装 `.NET 8 SDK`，准备可用的 SQL Server 实例。

2. 修改 `WinFormsApp/appsettings.json` 中的连接字符串：

```json
{
  "SqlServer": {
    "ConnectionString": "Server=localhost;Database=TestDB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

3. 还原并运行：

```powershell
dotnet restore
dotnet run --project WinFormsApp/WinFormsApp.csproj
```

4. 如需调用 AI API ，请在软件内 `AI 设置` 中填写接口地址、模型和 Key，或配置以下环境变量：

```powershell
AI_API_KEY=你的接口密钥
AI_API_BASE_URL=https://你的接口地址
AI_MODEL=你的模型名称
```

> API Key 只应保存在本机环境或本机设置中，不要提交到 Git 仓库。
