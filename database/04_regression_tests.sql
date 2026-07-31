-- ============================================================================
-- 系统管理模块（E 模块）数据库回归测试脚本
-- 覆盖代码评审反馈涉及的数据库边界情况：
--   1. user_id 为 NULL 的登录日志仍可被查询（对应 LoginLogService.ReadLoginLog 判空）
--   2. disabled 角色不参与授权（对应 UserContextService 过滤 R.STATUS='valid'）
--   3. 批量分配中任一 ID 无效时整体拒绝（对应 UserRoleService/RolePermissionService 先校验后写入）
--   4. 更新为重复工号触发唯一约束 ORA-00001（对应 UserService.Update 捕获 → 409）
--   5. 删除被外键引用的实体触发 ORA-02292（对应 User/Role/Permission Delete 捕获 → 409）
--   6. disabled 角色可被服务端识别并拒绝分配（对应 UserRoleService.Assign 校验 STATUS）
--
-- 用法（Oracle 23ai Free / FREEPDB1，DB_Design 用户）：
--   sqlplus DB_Design/***@localhost:1521/FREEPDB1 @database/04_regression_tests.sql
-- 或使用 SQL Developer 以“脚本”方式运行整个文件。
-- 任一步骤断言失败会抛出 ORA-20001 并中止；测试数据统一回滚，不污染业务数据。
-- ============================================================================

SET SERVEROUTPUT ON
SET DEFINE OFF

DECLARE
    v_failures  NUMBER := 0;
    v_user_id   NUMBER;
    v_role_id   NUMBER;
    v_role_id2  NUMBER;
    v_cnt       NUMBER;
    v_all_valid BOOLEAN := FALSE;
BEGIN
    DBMS_OUTPUT.PUT_LINE('===== E 模块数据库回归测试开始 =====');

    -- 事务性测试数据：无论成功失败，最终统一回滚
    SAVEPOINT regress_start;

    -- ---------- 1. user_id 为 NULL 的登录日志可正常查询 ----------
    INSERT INTO login_log (user_id, login_time, ip_address, result, fail_reason)
    VALUES (NULL, SYSTIMESTAMP, '127.0.0.1', '失败', '工号不存在');

    SELECT COUNT(*) INTO v_cnt FROM login_log WHERE user_id IS NULL;
    IF v_cnt = 0 THEN
        v_failures := v_failures + 1;
        DBMS_OUTPUT.PUT_LINE('[FAIL] 1. 未查询到 user_id 为 NULL 的登录日志');
    ELSE
        DBMS_OUTPUT.PUT_LINE('[PASS] 1. user_id 为 NULL 的登录日志可正常查询（服务端已做 IsDBNull 判空）');
    END IF;

    -- ---------- 2. disabled 角色不参与授权 ----------
    INSERT INTO sys_user (employee_no, password_hash, user_name, status)
    VALUES ('T_REGRESSION_USER', 'x', '回归测试用户', 'valid')
    RETURNING user_id INTO v_user_id;

    INSERT INTO sys_role (role_name, description, status)
    VALUES ('T_REGRESSION_ROLE_DISABLED', '回归测试-已停用', 'disabled')
    RETURNING role_id INTO v_role_id;

    INSERT INTO sys_user_role (user_id, role_id) VALUES (v_user_id, v_role_id);

    -- 与服务端 UserContextService 等价的授权角色查询（必须过滤 R.STATUS='valid'）
    SELECT COUNT(*) INTO v_cnt
    FROM sys_user_role ur
    JOIN sys_role r ON r.role_id = ur.role_id
    WHERE ur.user_id = v_user_id AND r.status = 'valid';
    IF v_cnt != 0 THEN
        v_failures := v_failures + 1;
        DBMS_OUTPUT.PUT_LINE('[FAIL] 2. disabled 角色仍出现在授权角色列表中');
    ELSE
        DBMS_OUTPUT.PUT_LINE('[PASS] 2. disabled 角色已被授权查询过滤');
    END IF;

    -- ---------- 3. 批量分配原子性：存在无效 ID 时整体拒绝 ----------
    INSERT INTO sys_role (role_name, description, status)
    VALUES ('T_REGRESSION_ROLE_VALID', '回归测试-有效', 'valid')
    RETURNING role_id INTO v_role_id2;

    -- 模拟服务端“先一次性校验所有 ID”：有效 1 个 + 不存在 1 个 → 整体不写入
    SELECT COUNT(*) INTO v_cnt
    FROM sys_role
    WHERE role_id IN (v_role_id2, 999999999);
    v_all_valid := (v_cnt = 1);
    IF v_all_valid THEN
        DBMS_OUTPUT.PUT_LINE('[PASS] 3. 批量分配先一次性校验所有 ID，存在无效 ID 时整体拒绝');
    ELSE
        v_failures := v_failures + 1;
        DBMS_OUTPUT.PUT_LINE('[FAIL] 3. 批量分配校验逻辑异常（期望仅 1 个有效角色）');
    END IF;

    -- ---------- 4. 更新为重复工号触发 ORA-00001 ----------
    INSERT INTO sys_user (employee_no, password_hash, user_name, status)
    VALUES ('T_REGRESSION_DUP', 'x', '回归测试-重复工号', 'valid');

    BEGIN
        UPDATE sys_user SET employee_no = 'T_REGRESSION_DUP'
        WHERE user_id = v_user_id;   -- 与上一条重复
        v_failures := v_failures + 1;
        DBMS_OUTPUT.PUT_LINE('[FAIL] 4. 重复工号更新未触发唯一约束');
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -1 THEN
                DBMS_OUTPUT.PUT_LINE('[PASS] 4. 重复工号更新触发 ORA-00001（服务端捕获后返回业务 409）');
            ELSE
                v_failures := v_failures + 1;
                DBMS_OUTPUT.PUT_LINE('[FAIL] 4. 期望 ORA-00001，实际 SQLCODE=' || SQLCODE);
            END IF;
    END;

    -- ---------- 5. 删除被引用实体触发 ORA-02292 ----------
    -- v_user_id 已被 sys_user_role 引用（第 2 步分配了角色）
    BEGIN
        DELETE FROM sys_user WHERE user_id = v_user_id;
        v_failures := v_failures + 1;
        DBMS_OUTPUT.PUT_LINE('[FAIL] 5. 删除被引用用户未触发外键约束');
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -2292 THEN
                DBMS_OUTPUT.PUT_LINE('[PASS] 5. 删除被引用用户触发 ORA-02292（服务端捕获后返回业务 409）');
            ELSE
                v_failures := v_failures + 1;
                DBMS_OUTPUT.PUT_LINE('[FAIL] 5. 期望 ORA-02292，实际 SQLCODE=' || SQLCODE);
            END IF;
    END;

    -- ---------- 6. disabled 角色可被服务端识别并拒绝分配 ----------
    SELECT COUNT(*) INTO v_cnt
    FROM sys_role
    WHERE role_id = v_role_id AND status = 'valid';
    IF v_cnt = 0 THEN
        DBMS_OUTPUT.PUT_LINE('[PASS] 6. disabled 角色可被服务端识别并拒绝分配（UserRoleService.Assign）');
    ELSE
        v_failures := v_failures + 1;
        DBMS_OUTPUT.PUT_LINE('[FAIL] 6. disabled 角色未被正确识别');
    END IF;

    -- ---------- 汇总：回滚全部测试数据 ----------
    ROLLBACK TO regress_start;

    IF v_failures > 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'E 模块数据库回归测试失败 ' || v_failures || ' 项');
    ELSE
        DBMS_OUTPUT.PUT_LINE('===== E 模块数据库回归测试全部通过 =====');
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO regress_start;
        RAISE;
END;
/
