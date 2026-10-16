# 档案管理系统 —— API 接口清单（REST v1.0 定稿）

> 风格：REST + JSON；鉴权：Bearer JWT；统一返回 `{code,msg,data}`。
> 前缀：`/api`；角色访问控制由后端按 `sys_permission` 五维拦截。

---

## 1. 认证 Auth
| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/auth/login` | 登录，返回 token + 用户信息(含角色/权限) |
| POST | `/api/auth/logout` | 登出 |
| GET  | `/api/auth/me` | 当前用户 + 权限范围 |

## 2. 档案门类 Category
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/categories` | 门类列表(启用的) |
| POST | `/api/categories` | 新建门类(含 template_json) |
| PUT  | `/api/categories/{id}` | 编辑门类/模板 |
| DELETE | `/api/categories/{id}` | 停用/删除 |
| GET  | `/api/categories/{id}/template` | 取某门类元数据模板(前端动态渲染表单) |

## 3. 档案(案卷) Archive
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/archives?categoryId=&status=&keyword=` | 列表(分页、按权限过滤) |
| GET  | `/api/archives/{id}` | 详情 + dynamic_fields |
| GET  | `/api/archives/{id}/tree` | 三级树: 档案→文件→附件 |
| POST | `/api/archives` | 新建档案(草稿)，body 含 dynamic_fields |
| PUT  | `/api/archives/{id}` | 编辑(仅草稿/退回可改) |
| DELETE | `/api/archives/{id}` | 逻辑删除 |
| POST | `/api/archives/{id}/submit` | 提交归档(→待审核) |
| POST | `/api/archives/{id}/audit` | 审核通过/退回 `{action:approve|reject,comment}` |
| POST | `/api/archives/batch-import` | Excel 批量导入 |

## 4. 文件(件) FileItem
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/archives/{archiveId}/files` | 案卷内文件列表(卷内目录) |
| POST | `/api/archives/{archiveId}/files` | 新增文件(含 dynamic_fields) |
| PUT  | `/api/files/{id}` | 编辑文件 |
| DELETE | `/api/files/{id}` | 删除文件 |

## 5. 附件 Attachment
| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/files/{fileId}/attachments` | 上传附件(分片/直传对象存储) |
| GET  | `/api/attachments/{id}/download` | 下载(带水印策略校验) |
| GET  | `/api/attachments/{id}/preview` | 在线预览(流式/转码) |
| DELETE | `/api/attachments/{id}` | 删除附件 |
| POST | `/api/attachments/{id}/ocr` | 触发 OCR(二期) |

## 6. 检索 Search
| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/search` | 多条件检索(门类/年度/题名/责任者/期限/密级) |
| POST | `/api/search/fulltext` | 全文检索(Elasticsearch, 基于 OCR/文本) |
| GET  | `/api/search/suggest?q=` | 检索联想 |

## 7. 借阅利用 Borrow
| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/borrow` | 发起借阅申请 `{targetType,targetId,reason,days}` |
| GET  | `/api/borrow?role=applicant\|approver` | 我的申请 / 待我审批 |
| PUT  | `/api/borrow/{id}/approve` | 审批通过(按密级/门类路由) |
| PUT  | `/api/borrow/{id}/reject` | 驳回(附理由) |
| PUT  | `/api/borrow/{id}/return` | 归还 |
| POST | `/api/borrow/{id}/renew` | 续期 |

## 8. 鉴定销毁 Appraisal
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/appraisal/due` | 到期保管期限清单 |
| POST | `/api/appraisal` | 发起鉴定提议 |
| PUT  | `/api/appraisal/{id}/approve` | 鉴定审批(续存/销毁) |
| PUT  | `/api/appraisal/{id}/execute` | 执行销毁(置 status=已销毁 + 记录) |

## 9. 统计报表 Stats
| 方法 | 路径 | 说明 |
|---|---|---|
| GET  | `/api/stats/overview` | 按门类/年度/期限/密级的数量与容量 |
| GET  | `/api/stats/borrow` | 借阅利用人次/频次排行 |
| GET  | `/api/stats/due-warning` | 到期未鉴定预警 |

## 10. 系统管理 System
| 方法 | 路径 | 说明 |
|---|---|---|
| CRUD | `/api/users` `/api/roles` | 用户、角色 |
| PUT  | `/api/roles/{id}/permissions` | 配置五维权限 |
| GET  | `/api/audit-logs?from=&to=` | 审计日志(不可篡改、只读) |
| POST | `/api/system/backup` | 触发备份 |
| GET  | `/api/system/health` | 健康检查 |

---

## 关键设计点
- **权限拦截**：所有 Archive/File/Attachment 读写在 Controller 层按 `角色×门类×op×max_secrecy` 校验；无权限返回 403。
- **档号生成**：后端按 `category.fk_rule` 在 submit/audit 时生成并校验唯一。
- **状态机**：档案 `草稿→待审核→已归档`（或 `退回`）；归档后仅鉴定/销毁可改。
- **附件安全**：下载/预览前校验借阅权限 + 密级，开启水印时注入访客水印。
- **审计**：写操作统一写 `audit_log`，含操作用户/IP/前后值。
