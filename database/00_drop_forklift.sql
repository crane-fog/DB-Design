-- ============================================================================
-- 工业制造物料进销存管理系统 · 本地开发对象清理
-- 仅允许：PYZ2190@FREEPDB1
-- 本脚本只删除下列明确列出的项目对象，不删除用户、不扫描删除其他对象。
-- ============================================================================

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON

DECLARE
    l_user         VARCHAR2(128) := UPPER(USER);
    l_service_name VARCHAR2(128) := UPPER(SYS_CONTEXT('USERENV', 'SERVICE_NAME'));

    PROCEDURE drop_ddl(p_ddl VARCHAR2, p_missing_code NUMBER) IS
    BEGIN
        EXECUTE IMMEDIATE p_ddl;
        DBMS_OUTPUT.PUT_LINE('dropped: ' || p_ddl);
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = p_missing_code THEN
                NULL;
            ELSE
                RAISE;
            END IF;
    END;
BEGIN
    IF l_user <> 'PYZ2190' OR l_service_name <> 'FREEPDB1' THEN
        RAISE_APPLICATION_ERROR(
            -20998,
            'Refusing rebuild: expected PYZ2190@FREEPDB1, got '
            || l_user || '@' || l_service_name);
    END IF;

    DBMS_OUTPUT.PUT_LINE('Target verified: ' || l_user || '@' || l_service_name);

    FOR object_name IN (
        SELECT column_value AS name
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'CT_BATCH_CONSUMPTION_GUARD',
            'CT_MATERIAL_VERSION_CYCLE_GUARD',
            'CT_BOM_CYCLE_GUARD'))
    ) LOOP
        drop_ddl('DROP TRIGGER ' || object_name.name, -4080);
    END LOOP;

    FOR object_name IN (
        SELECT column_value AS name
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'V_STOCK_LOCK_RECONCILIATION',
            'V_MATERIAL_BATCH_TRACE',
            'V_PRODUCT_BATCH_TRACE',
            'V_BATCH_CONSUMPTION_DETAIL',
            'V_EFFECTIVE_BOM_EDGE'))
    ) LOOP
        drop_ddl('DROP VIEW ' || object_name.name, -942);
    END LOOP;

    FOR object_name IN (
        SELECT column_value AS name
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'PKG_PRODUCTION_DOMAIN',
            'PKG_TRACE_DOMAIN',
            'PKG_BOM_DOMAIN',
            'PKG_APP_ERROR'))
    ) LOOP
        drop_ddl('DROP PACKAGE ' || object_name.name, -4043);
    END LOOP;

    FOR object_name IN (
        SELECT column_value AS name
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'FINISH_BATCH_CONSUMPTION',
            'BATCH_CONSUMPTION',
            'FINISH_INBOUND',
            'WASTE_DETECTION',
            'STOCK_LOCK',
            'STOCK_ALERT',
            'LINE_OUTPUT_RECORD',
            'LINE_STATUS',
            'FAULT_RECORD',
            'CAPACITY_BALANCE',
            'CAPACITY_DETECTION',
            'EXTERNAL_ORDER_DELIVERY',
            'EXTERNAL_ORDER_PRODUCTION',
            'EXTERNAL_ORDER',
            'PRODUCTION_ORDER',
            'PRODUCTION_CALENDAR',
            'CAPACITY_CONFIG',
            'PRODUCTION_LINE',
            'LINE_TYPE',
            'SUPPLIER_PRICE',
            'RECEIVE_RECORD',
            'OVERDUE_REMINDER',
            'PURCHASE_ORDER_ITEM',
            'PURCHASE_ORDER',
            'BOM',
            'MATERIAL_STOCK',
            'BOM_VERSION',
            'MATERIAL',
            'SUPPLIER',
            'MATERIAL_CATEGORY',
            'OPERATION_LOG',
            'LOGIN_LOG',
            'SYS_ROLE_PERMISSION',
            'SYS_USER_ROLE',
            'SYS_PERMISSION',
            'SYS_ROLE',
            'SYS_USER'))
    ) LOOP
        drop_ddl('DROP TABLE ' || object_name.name || ' CASCADE CONSTRAINTS PURGE', -942);
    END LOOP;
END;
/

PROMPT Known project objects removed.
