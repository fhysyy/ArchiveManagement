export interface CategoryDto {
  id: number
  code: string
  name: string
  description?: string
  status: number
  templateJson: string        // 案卷级元数据模板
  fileTemplateJson: string    // 卷内文件级元数据模板
  fkRule?: string
  defaultRetention?: string
}

export interface CreateCategoryDto {
  code: string
  name: string
  description?: string
  templateJson: string        // 案卷级
  fileTemplateJson: string    // 卷内文件级
  fkRule?: string
  defaultRetention?: string
}

export interface ArchiveDto {
  id: number
  categoryId: number
  parentId?: number
  isProject: boolean
  archiveNo: string
  title: string
  year?: number
  retention?: string
  secrecyLevel?: string
  orgUnit?: string
  dynamicFields?: string
  status: string
}

export interface CreateArchiveDto {
  categoryId: number
  parentId?: number
  isProject?: boolean
  title: string
  year?: number
  retention?: string
  secrecyLevel?: string
  orgUnit?: string
  dynamicFields?: string
}

export interface LoginResult {
  token: string
  username: string
  name: string
  role: string
}

export interface TemplateField {
  key: string
  label: string
  type: 'text' | 'select' | 'date' | 'number'
  required?: boolean
  enum?: string[]
}

// ===== 文件（件级）=====
export interface FileItemDto {
  id: number
  archiveId: number
  title: string
  docDate?: string
  author?: string
  pages?: number
  seqNo?: number
  fileNo?: string
  projectNo?: string
  dynamicFields?: string
  createdAt: string
}

export interface CreateFileItemDto {
  archiveId: number
  title: string
  docDate?: string
  author?: string
  pages?: number
  seqNo?: number
  fileNo?: string
  projectNo?: string
  dynamicFields?: string
}

// ===== 附件（电子文件级）=====
export interface AttachmentDto {
  id: number
  fileItemId: number
  fileName: string
  originalName?: string
  mimeType?: string
  sizeBytes?: number
  hash?: string
  version: number
  watermarkFlag: boolean
  uploadedAt: string
}

export interface AttachmentUploadResultDto {
  id: number
  originalName?: string
  hash?: string
  sizeBytes?: number
}

// ===== 条件检索（T11）=====
export interface MetaFilterDto {
  key: string
  op: string   // contains | eq | gt | lt | between
  value?: string
}

export interface SearchArchiveDto {
  meta?: MetaFilterDto[]
  categoryId?: number
  yearStart?: number
  yearEnd?: number
  title?: string
  author?: string
  retention?: string
  secrecyLevel?: string
  status?: string
  page?: number
  pageSize?: number
  sortBy?: string
  sortDir?: string
}

export interface PagedArchiveResult {
  total: number
  page: number
  pageSize: number
  totalPages: number
  items: ArchiveDto[]
}

// ===== 借阅利用（T12）=====
export interface CreateBorrowDto {
  targetType: string // archive | file
  targetId: number
  days?: number
  reason?: string
}

export interface BorrowDto {
  id: number
  applicantId: number
  applicantName: string
  targetType: string
  targetId: number
  targetTitle: string
  days?: number
  reason?: string
  status: string // Pending | Approved | Rejected | Returned | Expired
  approvedById?: number
  approvedByName: string
  approvedAt?: string
  expireAt?: string
  createdAt: string
}

export interface BorrowAuditDto {
  comment?: string
  days?: number
}

// ===== 归档审核（T13）=====
export interface ArchiveAuditDto {
  action: string // approve | reject
  comment?: string
}

// ===== 鉴定销毁（T14）=====
export interface CreateAppraisalDto {
  targetType: string // archive | file
  targetId: number
  proposeReason?: string
}

export interface AppraisalDto {
  id: number
  targetType: string
  targetId: number
  targetTitle: string
  proposeReason?: string
  status: string // Pending | Approved | Destroyed | Kept
  approvedById?: number
  approvedByName: string
  executedAt?: string
  createdAt: string
}

export interface AppraisalAuditDto {
  action: string // keep | destroy
  comment?: string
}

export interface DueArchiveDto {
  archive: ArchiveDto
  expiryYear: number
  appraisalId?: number | null // 在途鉴定任务 Id（Pending/Approved）
  appraisalStatus?: string | null
}

export interface PagedAuditResult {
  total: number
  page: number
  pageSize: number
  totalPages: number
  items: AuditLogDto[]
}

// ===== 全宗管理 =====
export interface FondsDto {
  id: number
  code: string
  name: string
  description?: string
  status: number
  createdAt: string
  updatedAt: string
  categoryCount: number
  archiveCount: number
}
export interface SaveFondsDto {
  id: number
  code: string
  name: string
  description?: string
  status?: number
}

export interface ProjectTreeNode {
  id: number
  label: string
  type: string // project | archive | file
  archiveNo?: string | null
  status?: string | null
  fileCount: number
  children: ProjectTreeNode[]
}

export interface PagedAppraisalResult {
  total: number
  page: number
  pageSize: number
  totalPages: number
  items: AppraisalDto[]
}
export interface PagedDueResult {
  total: number
  page: number
  pageSize: number
  totalPages: number
  items: DueArchiveDto[]
}

// ===== 统计报表（T15）=====
export interface KvDto { key: string; count: number }
export interface CategoryStatDto { categoryId: number; categoryName: string; count: number }
export interface StatsOverviewDto {
  total: number
  archived: number
  draft: number
  byCategory: CategoryStatDto[]
  byYear: KvDto[]
  byRetention: KvDto[]
  bySecrecy: KvDto[]
}
export interface BorrowTargetStat { targetType: string; targetId: number; targetTitle: string; count: number }
export interface BorrowApplicantStat { applicantId: number; applicantName: string; count: number }
export interface BorrowStatsDto {
  totalBorrows: number
  pendingBorrows: number
  byTarget: BorrowTargetStat[]
  byApplicant: BorrowApplicantStat[]
}
export interface DueWarningItem { archive: ArchiveDto; expiryYear: number; appraised: boolean }
export interface DueWarningDto { total: number; items: DueWarningItem[] }

// ===== 审计日志（T16）=====
export interface AuditLogDto {
  id: number
  userId?: number
  userName: string
  action?: string
  targetType?: string
  targetId?: number
  detail?: string
  ip?: string
  ts: string
}

// ===== 系统管理（T17）=====
export interface HealthDto { status: string; dbOk: boolean; time: string; version: string }
export interface BackupResultDto { ok: boolean; message: string; file?: string; ts: string }
export interface UserDto {
  id: number
  username: string
  name: string
  dept?: string
  status: number
  roleCode?: string
  roleName?: string
}
export interface CreateUserDto {
  username: string
  name: string
  dept?: string
  password: string
  roleCode: string
}
export interface RoleDto { id: number; code: string; name: string }
export interface CreateRoleDto { code: string; name: string }

// 五维权限：角色 × 门类 × 操作 × 密级
export interface PermissionDto {
  id: number
  roleId: number
  categoryId?: number
  categoryName: string
  op: string
  maxSecrecy?: string
}
export interface SetPermissionsRequest { items: PermissionDto[] }
