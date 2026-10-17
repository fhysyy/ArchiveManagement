# 档案管理系统 —— API 接口清单（v1.1 实现对齐版）

> 风格：REST + JSON；鉴权：Bearer JWT；统一返回 `{code,msg,data}`（`code=0` 表示成功）。
> 前缀：`/api`；权限由后端按 `sys_permission` 五维（角色 × 门类 × 操作 × 密级）拦截。
> 本版按**运行时 Swagger 实际路由**核对生成，标注了尚未实现（二期）的接口。

## 通用约定

| 项 | 约定 |
|---|---|
| 枚举序列化 | 全部以**字符串**输出/接收（`Draft` / `Approved` / `Destroyed` …），便于前端直接判断 |
| 分页参数 | 列表接口统一支持 `page` + `pageSize`（`pageSize` 上限 200，默认 20） |
| 分页返回 | **带 `page`/`pageSize` 时**返回 `{total,page,pageSize,totalPages,items}`；**不带时**返回数组（向后兼容） |
| 错误码 | `0` 成功；`400` 参数错误；`401` 未登录/Token 失效；`403` 无权限；`500` 服务端异常 |
| 权限异常 | 无权限时后端抛 `UnauthorizedAccessException`，由全局中间件转 `403` |

---

## 1. 认证 Auth
| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/auth/login` | 登录，返回 `token` + 用户信息（含角色） |

> 二期：`POST /api/auth/logout`、`GET /api/auth/me`（当前由前端清除本地 token 实现登出）。

## 2. 全宗 Fonds
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/fonds` | 全宗列表（含门类数、档案数概览） |
| GET  | `/api/fonds/{id}` | 全宗详情 |
| PUT  | `/api/fonds` | 新增/更新全宗（`id=0` 为新增），仅 `admin` 角色 |

> `fonds.code` 即档号规则中的 `{fonds}` 占位符；一期单全宗，多全宗结构已预留。

## 3. 档案门类 Category
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/categories` | 门类列表（仅 `status=1` 启用） |
| GET  | `/api/categories/{id}` | 门类详情（含 `templateJson` 案卷级、`fileTemplateJson` 文件级） |
| POST | `/api/categories` | 新建门类（含两级元数据模板、`fkRule`、默认保管期限） |
| PUT  | `/api/categories/{id}` | 编辑门类/模板 |
| DELETE | `/api/categories/{id}` | 停用（存在档案时拒绝删除） |

## 4. 档案（案卷）Archive
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/archives?categoryId=&status=&page=&pageSize=` | 列表（按门类 + 密级权限过滤，逻辑删除项不可见） |
| GET  | `/api/archives/{id}` | 详情（含 `dynamicFields`）；无权限按"不存在"返回，避免泄露 |
| GET  | `/api/archives/{id}/tree` | 三级树：档案 → 文件 → 附件 |
| GET  | `/api/archives/project-tree?categoryId=` | 门类结构树：项目 → 案卷 → 卷内文件（基建类用） |
| POST | `/api/archives` | 新建档案（草稿），`parentId` + `isProject` 表达"项目/案卷" |
| PUT  | `/api/archives/{id}` | 编辑（仅 `Draft` / `Rejected` 可改） |
| DELETE | `/api/archives/{id}` | 逻辑删除（级联文件 → 附件 → 对象存储） |
| POST | `/api/archives/{id}/submit` | 提交归档（`Draft` → `Submitted`） |
| POST | `/api/archives/{id}/audit` | 审核 `{action: approve\|reject, comment}`（→ `Archived` / `Rejected`） |
| POST | `/api/archives/batch-import` | 批量导入（JSON：一行一条档案） |
| POST | `/api/archives/import-excel` | Excel 导入（`multipart/form-data`：`file` + `categoryId`） |
| GET  | `/api/archives/import-template?categoryId=` | 下载导入模板（表头按门类元数据字段生成） |

**Excel 导入表头映射**：固定列 `题名 / 年度 / 保管期限 / 密级 / 归档单位 / 是否项目 / 所属项目档号`；其余列按门类 `templateJson` 的 `label` 或 `key` 自动映射进 `dynamicFields`。返回 `{created, failed, errors[], items[]}`。

## 5. 文件（件）FileItem
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/files?archiveId=` | 案卷内文件列表（卷内目录数据源） |
| GET  | `/api/files/{id}` | 文件详情 |
| POST | `/api/files` | 新增文件（含 `dynamicFields`，取门类**文件级**模板） |
| PUT  | `/api/files/{id}` | 编辑文件 |
| DELETE | `/api/files/{id}` | 逻辑删除（级联附件与对象存储） |

> 父案卷为 `Archived` / `Destroyed` 时禁止增删改卷内文件。

## 6. 附件 Attachment
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/files/{fileId}/attachments` | 附件列表（含 SHA256、大小、版本） |
| POST | `/api/files/{fileId}/attachments` | 上传附件（算 SHA256 防篡改，落对象存储） |
| GET  | `/api/attachments/{id}/download` | 下载（权限校验 + 图片自动加水印） |
| GET  | `/api/attachments/{id}/preview` | 在线预览（图片同样带水印） |
| DELETE | `/api/attachments/{id}` | 删除附件（同步删除对象存储文件） |

> 访问条件：有该门类 `view` 权限，**或**本人存在未逾期的已批准借阅。图片水印内容为「用户名 + 时间」，可用 `Storage:Watermark=false` 关闭。

## 7. 检索 Search
| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/search` | 多条件检索：门类/年度/题名/责任者/保管期限/密级/状态 + **自定义字段 `meta`** |
| GET  | `/api/search/suggest?q=` | 检索联想（题名/档号前缀） |

**自定义字段检索**（`meta` 数组，每项 `{key, op, value}`）：
- `op` 支持 `contains` / `eq` / `gt` / `lt` / `between`（`value` 用 `min|max`）
- 后端用 `JSON_VALUE` + `ISJSON` 在 SQL 侧过滤，key 走白名单正则 `^[A-Za-z_][A-Za-z0-9_]{0,63}$`，value 全部参数化（防注入）

> 二期：`POST /api/search/fulltext`（依赖 Elasticsearch）。

## 8. 借阅利用 Borrow
| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/borrow` | 发起申请 `{targetType,targetId,reason,days}` |
| GET  | `/api/borrow?role=applicant\|approver&page=&pageSize=` | 我的申请 / 待我审批（支持分页） |
| PUT  | `/api/borrow/{id}/approve` | 审批通过 `{days,comment}` → 写入到期时间 |
| PUT  | `/api/borrow/{id}/reject` | 驳回 `{comment}` |
| PUT  | `/api/borrow/{id}/return` | 归还 |
| POST | `/api/borrow/{id}/renew` | 续期 `{days}`（仅已批准且未逾期） |

> 状态：`Pending → Approved / Rejected`；`Approved → Returned / Expired`。逾期由 `SweepExpiredAsync` 扫描置为 `Expired`。

## 9. 鉴定销毁 Appraisal
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/appraisal/due?excludeProposed=&page=&pageSize=` | 到期保管期限清单（标注在途鉴定 `appraisalId`/`appraisalStatus`） |
| GET  | `/api/appraisal?status=&page=&pageSize=` | 鉴定任务列表（支持按状态过滤 + 分页） |
| POST | `/api/appraisal` | 发起鉴定提议 `{targetType,targetId,proposeReason}` |
| PUT  | `/api/appraisal/{id}/approve` | 鉴定审批 `{action: keep\|destroy}` |
| PUT  | `/api/appraisal/{id}/execute` | 执行销毁（档案置 `Destroyed`，不可恢复） |

> 到期判定：`retention` 解析出 N 年，`(年度 + N) <= 当前年`；"永久"不列入。`excludeProposed=true` 只返回尚未发起鉴定的（用于预警计数）。

## 10. 统计报表 Stats
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/stats/overview` | 总量/已归档/草稿 + 按门类/年度/保管期限/密级分布（前端渲染条形图） |
| GET  | `/api/stats/borrow` | 借阅总人次、待审批 + 高频档案 TOP10、高频利用者 TOP10 |
| GET  | `/api/stats/due-warning` | 到期未鉴定预警清单 |

## 11. 审计日志 Audit
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/audit-logs?from=&to=&page=&pageSize=` | 审计日志（只读，含用户/IP/动作/对象/详情/时间） |

## 12. 系统管理 System
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/system/health` | 健康检查（服务 + 数据库连通） |
| POST | `/api/system/backup` | **真实备份**：执行 `BACKUP DATABASE` 到 `Backup:Path`，返回 `.bak` 路径与大小 |
| GET  | `/api/users` | 用户列表（含角色） |
| POST | `/api/users` | 新建用户 |
| DELETE | `/api/users/{id}` | 删除用户 |
| PUT  | `/api/users/{id}/password` | 重置密码（PBKDF2 重新加盐哈希） |
| PUT  | `/api/users/{id}/status` | 启用/禁用 |
| GET  | `/api/roles` | 角色列表 |
| POST | `/api/roles` | 新建角色 |
| DELETE | `/api/roles/{id}` | 删除角色（同时清理其权限行） |
| GET  | `/api/roles/{id}/permissions` | 读取五维权限 |
| PUT  | `/api/roles/{id}/permissions` | 配置五维权限 |

---

## 关键设计点

- **权限拦截**：Archive / File / Attachment 的读写均在服务层校验 `角色 × 门类 × 操作 × 最高密级`；角色**未配置任何权限行时视为不限制**（避免种子角色上线即锁死），配置后才收敛。
- **档号生成**：按 `category.fkRule`（`{fonds}-{code}-{year}-{seq}`）生成，唯一冲突时递增序号（最多 1000 次）。
- **状态机**：档案 `Draft → Submitted → Archived / Rejected`；归档后仅鉴定/销毁可变更。
- **附件安全**：DB 只存对象 key + SHA256；下载/预览校验权限或有效借阅；图片服务端注入水印。
- **逻辑删除**：`archive` / `file_item` 置 `IsDeleted`，级联清理附件与对象存储。
- **审计**：所有写操作写 `audit_log`（用户/操作/IP/对象/详情/时间）。
- **日志**：Serilog 输出到控制台 + 按天滚动文件（默认 90 天留存）。
- **JWT**：密钥优先取环境变量 `Jwt__Key`；弱密钥在 Development 仅告警，生产环境**阻止启动**。

## 二期（未实现）
| 能力 | 说明 |
|---|---|
| `POST /api/search/fulltext` | 全文检索，需 Elasticsearch + 文本抽取 |
| `POST /api/attachments/{id}/ocr` | 附件 OCR |
| 电子签章 / CA 认证 | 需第三方服务 |
| SSO / OA 对接 | 一期仅预留接口位 |
