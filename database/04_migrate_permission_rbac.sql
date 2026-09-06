-- ============================================================================
-- 权限系统 RBAC 迁移脚本（仅用于已存在旧版 SYS_PERMISSION 结构的数据库）
--
-- 执行方式：使用 Oracle SQLcl、SQL*Plus 或 SQL Developer 的“运行脚本”模式执行。
-- 执行前：
--   1. 备份当前 schema，确认 SYS_PERMISSION 仍包含 "resource" 和 action 字段。
--   2. 确认不存在同名角色；脚本也会在变更前再次检查。
--   3. 停止应用写入。Oracle DDL 会隐式提交，本脚本不能依赖 ROLLBACK 撤销结构变更。
--
-- 脚本保留以下数据备份表，不会自动删除：
--   SYS_PERMISSION_RBAC_BACKUP
--   SYS_ROLE_PERMISSION_RBAC_BACKUP
-- 验证应用运行正常后可由开发者手动删除这两个备份表。
-- ============================================================================

WHENEVER SQLERROR EXIT SQL.SQLCODE
SET DEFINE OFF

PROMPT [1/9] 检查角色名称是否唯一
DECLARE
  v_duplicate_count PLS_INTEGER;
BEGIN
  SELECT COUNT(*)
    INTO v_duplicate_count
    FROM (
      SELECT role_name
        FROM sys_role
       GROUP BY role_name
      HAVING COUNT(*) > 1
    );

  IF v_duplicate_count > 0 THEN
    RAISE_APPLICATION_ERROR(-20010, 'SYS_ROLE 存在重复 ROLE_NAME，请先处理后再迁移');
  END IF;
END;
/

PROMPT [2/9] 备份旧权限目录和角色授权关系
CREATE TABLE sys_permission_rbac_backup AS
SELECT * FROM sys_permission;

CREATE TABLE sys_role_permission_rbac_backup AS
SELECT * FROM sys_role_permission;

PROMPT [3/9] 为权限目录增加稳定权限码和展示元数据
ALTER TABLE sys_permission ADD (
  permission_code VARCHAR2(100),
  module_name     VARCHAR2(50),
  resource_name   VARCHAR2(100),
  action_name     VARCHAR2(50),
  description     VARCHAR2(200),
  sort_order      NUMBER DEFAULT 0,
  status          VARCHAR2(10) DEFAULT 'valid'
);

PROMPT [4/9] 建立旧权限到新权限码的迁移映射
CREATE TABLE sys_permission_rbac_map (
  legacy_resource VARCHAR2(100) NOT NULL,
  legacy_action   VARCHAR2(50) NOT NULL,
  permission_code VARCHAR2(100) NOT NULL
);

INSERT ALL
  INTO sys_permission_rbac_map VALUES ('物料','查看','material:item:view')
  INTO sys_permission_rbac_map VALUES ('物料','查看','material:category:view')
  INTO sys_permission_rbac_map VALUES ('物料','创建','material:item:create')
  INTO sys_permission_rbac_map VALUES ('物料','创建','material:category:create')
  INTO sys_permission_rbac_map VALUES ('物料','修改','material:item:update')
  INTO sys_permission_rbac_map VALUES ('物料','修改','material:item:delete')
  INTO sys_permission_rbac_map VALUES ('物料','修改','material:category:update')
  INTO sys_permission_rbac_map VALUES ('物料','修改','material:category:delete')
  INTO sys_permission_rbac_map VALUES ('BOM','查看','material:bom-version:view')
  INTO sys_permission_rbac_map VALUES ('BOM','查看','material:bom:view')
  INTO sys_permission_rbac_map VALUES ('BOM','查看','material:bom:check-cycle')
  INTO sys_permission_rbac_map VALUES ('BOM','查看','material:bom:tree:view')
  INTO sys_permission_rbac_map VALUES ('BOM','查看','material:bom:reverse:view')
  INTO sys_permission_rbac_map VALUES ('BOM','查看','material:cost:calculate')
  INTO sys_permission_rbac_map VALUES ('BOM','查看','material:loss:calculate')
  INTO sys_permission_rbac_map VALUES ('BOM','创建','material:bom-version:create')
  INTO sys_permission_rbac_map VALUES ('BOM','创建','material:bom:create')
  INTO sys_permission_rbac_map VALUES ('BOM','修改','material:bom-version:update')
  INTO sys_permission_rbac_map VALUES ('BOM','修改','material:bom-version:delete')
  INTO sys_permission_rbac_map VALUES ('BOM','修改','material:bom:update')
  INTO sys_permission_rbac_map VALUES ('BOM','修改','material:bom:delete')
  INTO sys_permission_rbac_map VALUES ('库存','查看','inventory:stock:view')
  INTO sys_permission_rbac_map VALUES ('库存','查看','inventory:alert:view')
  INTO sys_permission_rbac_map VALUES ('库存','查看','inventory:lock:view')
  INTO sys_permission_rbac_map VALUES ('库存','查看','inventory:obsolete:view')
  INTO sys_permission_rbac_map VALUES ('库存','查看','inventory:completion:view')
  INTO sys_permission_rbac_map VALUES ('库存','修改','inventory:shortage:calculate')
  INTO sys_permission_rbac_map VALUES ('库存','修改','inventory:alert:generate')
  INTO sys_permission_rbac_map VALUES ('库存','修改','inventory:alert:handle')
  INTO sys_permission_rbac_map VALUES ('库存','修改','inventory:lock:create')
  INTO sys_permission_rbac_map VALUES ('库存','修改','inventory:lock:release')
  INTO sys_permission_rbac_map VALUES ('库存','修改','inventory:obsolete:detect')
  INTO sys_permission_rbac_map VALUES ('库存','修改','inventory:obsolete:handle')
  INTO sys_permission_rbac_map VALUES ('库存','修改','inventory:completion:create')
  INTO sys_permission_rbac_map VALUES ('采购订单','查看','purchase:supplier:view')
  INTO sys_permission_rbac_map VALUES ('采购订单','查看','purchase:buyer:view')
  INTO sys_permission_rbac_map VALUES ('采购订单','查看','purchase:order:view')
  INTO sys_permission_rbac_map VALUES ('采购订单','查看','purchase:receipt:view')
  INTO sys_permission_rbac_map VALUES ('采购订单','查看','purchase:overdue:view')
  INTO sys_permission_rbac_map VALUES ('采购订单','创建','purchase:buyer:eligible')
  INTO sys_permission_rbac_map VALUES ('采购订单','创建','purchase:order:create')
  INTO sys_permission_rbac_map VALUES ('采购订单','创建','purchase:receipt:create')
  INTO sys_permission_rbac_map VALUES ('采购订单','创建','purchase:overdue:generate')
  INTO sys_permission_rbac_map VALUES ('采购订单','修改','purchase:order:submit')
  INTO sys_permission_rbac_map VALUES ('采购订单','修改','purchase:order:cancel')
  INTO sys_permission_rbac_map VALUES ('采购订单','修改','purchase:overdue:handle')
  INTO sys_permission_rbac_map VALUES ('采购订单','审核','purchase:order:submit')
  INTO sys_permission_rbac_map VALUES ('生产订单','查看','production:order:view')
  INTO sys_permission_rbac_map VALUES ('生产订单','创建','production:order:create')
  INTO sys_permission_rbac_map VALUES ('生产订单','审核','production:order:update')
  INTO sys_permission_rbac_map VALUES ('生产订单','审核','production:order:approve')
  INTO sys_permission_rbac_map VALUES ('生产订单','审核','production:order:start')
  INTO sys_permission_rbac_map VALUES ('生产订单','审核','production:order:finish')
  INTO sys_permission_rbac_map VALUES ('生产订单','审核','production:order:cancel')
  INTO sys_permission_rbac_map VALUES ('外部订单','查看','external-order:view-all')
  INTO sys_permission_rbac_map VALUES ('外部订单','创建','external-order:create-for-customer')
  INTO sys_permission_rbac_map VALUES ('外部订单','审核','external-order:review')
  INTO sys_permission_rbac_map VALUES ('外部订单','审核','external-order:convert')
  INTO sys_permission_rbac_map VALUES ('质量追溯','查看','trace:consumption:view')
  INTO sys_permission_rbac_map VALUES ('质量追溯','查看','trace:consumption:create')
  INTO sys_permission_rbac_map VALUES ('质量追溯','查看','trace:consumption:update')
  INTO sys_permission_rbac_map VALUES ('质量追溯','查看','trace:consumption:delete')
  INTO sys_permission_rbac_map VALUES ('质量追溯','查看','trace:product:view')
  INTO sys_permission_rbac_map VALUES ('质量追溯','查看','trace:material:view')
  INTO sys_permission_rbac_map VALUES ('质量追溯','查看','trace:impact:analyze')
  INTO sys_permission_rbac_map VALUES ('生产线','查看','production:line:view')
  INTO sys_permission_rbac_map VALUES ('生产线','查看','production:line-type:view')
  INTO sys_permission_rbac_map VALUES ('生产线','查看','production:capacity-config:view')
  INTO sys_permission_rbac_map VALUES ('生产线','查看','production:calendar:view')
  INTO sys_permission_rbac_map VALUES ('生产线','查看','production:fault:view')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:line:create')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:line:update')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:line-type:update')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:capacity-config:update')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:calendar:update')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:calendar:delete')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:capacity:estimate')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:capacity:detect')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:capacity:balance')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:fault:report')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:fault:claim')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:fault:update-assigned')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:fault:update-any')
  INTO sys_permission_rbac_map VALUES ('生产线','修改','production:line-status:update')
  INTO sys_permission_rbac_map VALUES ('用户管理','查看','system:user:view')
  INTO sys_permission_rbac_map VALUES ('用户管理','查看','system:role:view')
  INTO sys_permission_rbac_map VALUES ('用户管理','查看','system:permission:view')
  INTO sys_permission_rbac_map VALUES ('用户管理','查看','system:audit:login:view')
  INTO sys_permission_rbac_map VALUES ('用户管理','查看','system:audit:operation:view')
  INTO sys_permission_rbac_map VALUES ('用户管理','创建','system:user:create')
  INTO sys_permission_rbac_map VALUES ('用户管理','创建','system:role:create')
  INTO sys_permission_rbac_map VALUES ('用户管理','创建','system:audit:operation:create')
  INTO sys_permission_rbac_map VALUES ('用户管理','修改','system:user:update')
  INTO sys_permission_rbac_map VALUES ('用户管理','修改','system:user:delete')
  INTO sys_permission_rbac_map VALUES ('用户管理','修改','system:user:assign-role')
  INTO sys_permission_rbac_map VALUES ('用户管理','修改','system:role:update')
  INTO sys_permission_rbac_map VALUES ('用户管理','修改','system:role:delete')
  INTO sys_permission_rbac_map VALUES ('用户管理','修改','system:role:assign-permission')
SELECT 1 FROM dual;

PROMPT [5/9] 替换权限目录并写入内置角色默认授权
DELETE FROM sys_role_permission;
DELETE FROM sys_permission;

@@permission_catalog.sql

PROMPT [6/9] 将自定义角色的旧授权映射到新权限码
INSERT INTO sys_role_permission (role_id, permission_id)
SELECT DISTINCT old_rp.role_id, new_p.permission_id
FROM sys_role_permission_rbac_backup old_rp
JOIN sys_permission_rbac_backup old_p
  ON old_p.permission_id = old_rp.permission_id
JOIN sys_permission_rbac_map map
  ON map.legacy_resource = old_p."resource"
 AND map.legacy_action = old_p.action
JOIN sys_permission new_p
  ON new_p.permission_code = map.permission_code
JOIN sys_role r
  ON r.role_id = old_rp.role_id
WHERE r.role_name NOT IN (
  '系统管理员','生产管理员','采购员','库存管理员','质量管理员','设备管理员','外部客户'
)
  AND NOT EXISTS (
    SELECT 1
    FROM sys_role_permission current_rp
    WHERE current_rp.role_id = old_rp.role_id
      AND current_rp.permission_id = new_p.permission_id
  );

PROMPT [7/9] 验证目录、关联和内置管理员权限
DECLARE
  v_count          PLS_INTEGER;
  v_permission_cnt PLS_INTEGER;
BEGIN
  SELECT COUNT(*) INTO v_permission_cnt FROM sys_permission;
  IF v_permission_cnt <> 99 THEN
    RAISE_APPLICATION_ERROR(-20011, '新权限目录数量应为 99，实际为 ' || v_permission_cnt);
  END IF;

  SELECT COUNT(*)
    INTO v_count
    FROM sys_permission
   WHERE permission_code IS NULL
      OR module_name IS NULL
      OR resource_name IS NULL
      OR action_name IS NULL
      OR sort_order IS NULL
      OR status IS NULL;
  IF v_count > 0 THEN
    RAISE_APPLICATION_ERROR(-20012, '新权限目录存在必填字段为空的记录');
  END IF;

  SELECT COUNT(*)
    INTO v_count
    FROM sys_role_permission rp
    LEFT JOIN sys_role r ON r.role_id = rp.role_id
    LEFT JOIN sys_permission p ON p.permission_id = rp.permission_id
   WHERE r.role_id IS NULL OR p.permission_id IS NULL;
  IF v_count > 0 THEN
    RAISE_APPLICATION_ERROR(-20013, '角色权限关系存在孤立记录');
  END IF;

  SELECT COUNT(*)
    INTO v_count
    FROM sys_role r
    JOIN sys_role_permission rp ON rp.role_id = r.role_id
   WHERE r.role_name = '系统管理员';
  IF v_count <> v_permission_cnt THEN
    RAISE_APPLICATION_ERROR(-20014, '系统管理员未关联完整权限目录');
  END IF;
END;
/

PROMPT [8/9] 收紧约束并删除旧资源操作字段
ALTER TABLE sys_permission MODIFY (
  permission_code NOT NULL,
  module_name NOT NULL,
  resource_name NOT NULL,
  action_name NOT NULL,
  sort_order NOT NULL,
  status NOT NULL
);

ALTER TABLE sys_permission DROP CONSTRAINT uk_perm;
ALTER TABLE sys_permission DROP COLUMN "resource";
ALTER TABLE sys_permission DROP COLUMN action;

ALTER TABLE sys_permission ADD CONSTRAINT uk_permission_code UNIQUE (permission_code);
ALTER TABLE sys_permission ADD CONSTRAINT ck_permission_sort CHECK (sort_order >= 0);
ALTER TABLE sys_permission ADD CONSTRAINT ck_permission_status CHECK (status IN ('valid','disabled'));
ALTER TABLE sys_role ADD CONSTRAINT uk_sys_role_name UNIQUE (role_name);
CREATE INDEX idx_permission_catalog ON sys_permission(module_name, sort_order, permission_id);

ALTER TABLE sys_permission MODIFY (
  permission_id GENERATED BY DEFAULT ON NULL AS IDENTITY (START WITH LIMIT VALUE)
);

PROMPT [9/9] 清理临时映射并提交数据
DROP TABLE sys_permission_rbac_map PURGE;
COMMIT;

PROMPT 权限系统数据库迁移完成。请保留备份表直至后端和前端验收通过。

-- 恢复提示：如迁移后需要回退，请停止应用写入，并根据备份表恢复旧结构。
-- 典型顺序为：删除当前角色权限和权限目录；恢复 "resource"/action 字段及 UK_PERM；
-- 从 SYS_PERMISSION_RBAC_BACKUP 按原 permission_id 写回权限；再从
-- SYS_ROLE_PERMISSION_RBAC_BACKUP 写回角色权限。由于 DDL 已隐式提交，回退应由开发者
-- 审核后分步执行，不要仅运行 ROLLBACK。
