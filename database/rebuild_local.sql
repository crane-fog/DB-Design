-- ============================================================================
-- 本地开发数据库重建入口
-- 调用示例（SQLcl/SQL*Plus）：@database/rebuild_local.sql
-- 仅允许连接到 PYZ2190@FREEPDB1 后执行。
-- ============================================================================

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET DEFINE OFF
SET SERVEROUTPUT ON
SET VERIFY OFF

DECLARE
    l_user         VARCHAR2(128) := UPPER(USER);
    l_service_name VARCHAR2(128) := UPPER(SYS_CONTEXT('USERENV', 'SERVICE_NAME'));
BEGIN
    IF l_user <> 'PYZ2190' OR l_service_name <> 'FREEPDB1' THEN
        RAISE_APPLICATION_ERROR(
            -20998,
            'Refusing rebuild: expected PYZ2190@FREEPDB1, got '
            || l_user || '@' || l_service_name);
    END IF;
    DBMS_OUTPUT.PUT_LINE('Rebuilding ' || l_user || '@' || l_service_name);
END;
/

@@00_drop_forklift.sql
@@01_schema_forklift.sql
@@02_domain_logic_forklift.sql

DECLARE
    l_invalid_count NUMBER;
    l_error_count   NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO l_invalid_count
      FROM user_objects
     WHERE status = 'INVALID';

    SELECT COUNT(*)
      INTO l_error_count
      FROM user_errors;

    IF l_invalid_count > 0 OR l_error_count > 0 THEN
        RAISE_APPLICATION_ERROR(
            -20997,
            'Rebuild produced invalid objects: invalid='
            || l_invalid_count || ', errors=' || l_error_count);
    END IF;

    DBMS_OUTPUT.PUT_LINE('Rebuild complete; invalid objects=0, compile errors=0');
END;
/

EXIT SUCCESS
