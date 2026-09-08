-- ============================================================================
-- Oracle 领域逻辑回归：BOM、锁料、消耗与分批追溯
-- 前置：已以 PYZ2190@FREEPDB1 执行 rebuild_local.sql
-- 所有可写用例均回滚，不改变种子数据。
-- ============================================================================

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON
SET VERIFY OFF

DECLARE
    l_invalid NUMBER;
    l_errors  NUMBER;
BEGIN
    SELECT COUNT(*) INTO l_invalid FROM user_objects WHERE status = 'INVALID';
    SELECT COUNT(*) INTO l_errors FROM user_errors;
    IF l_invalid <> 0 OR l_errors <> 0 THEN
        RAISE_APPLICATION_ERROR(-20990, '存在无效对象或编译错误');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS object compilation');
END;
/

DECLARE
    l_rows          SYS_REFCURSOR;
    l_material_id   NUMBER;
    l_name          VARCHAR2(200);
    l_model         VARCHAR2(200);
    l_type          VARCHAR2(50);
    l_unit          VARCHAR2(50);
    l_quantity      NUMBER;
    l_accumulated   NUMBER;
    l_depth         NUMBER;
    l_parent_id     NUMBER;
    l_path          VARCHAR2(4000);
    l_is_leaf       NUMBER;
    l_count         NUMBER := 0;
    l_deepest       NUMBER := 0;
BEGIN
    pkg_bom_domain.open_tree(101, 2, 1, l_rows);
    LOOP
        FETCH l_rows INTO l_material_id, l_name, l_model, l_type, l_unit,
            l_quantity, l_accumulated, l_depth, l_parent_id, l_path, l_is_leaf;
        EXIT WHEN l_rows%NOTFOUND;
        l_count := l_count + 1;
        l_deepest := GREATEST(l_deepest, l_depth);
    END LOOP;
    CLOSE l_rows;

    IF l_count < 20 OR l_deepest < 4 THEN
        RAISE_APPLICATION_ERROR(-20991, 'BOM 树展开结果不完整');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS recursive BOM tree rows=' || l_count || ', depth=' || l_deepest);
END;
/

SAVEPOINT regression_cycle;
DECLARE
    l_failed BOOLEAN := FALSE;
BEGIN
    BEGIN
        INSERT INTO bom (parent_material_id, child_material_id, version_id, quantity, loss_rate)
        VALUES (203, 101, 7, 1, 0);
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -20202 THEN
                l_failed := TRUE;
            ELSE
                RAISE;
            END IF;
    END;
    IF NOT l_failed THEN
        RAISE_APPLICATION_ERROR(-20992, '循环 BOM 未被阻止');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS cycle trigger');
END;
/
ROLLBACK TO regression_cycle;

DECLARE
    l_has_cycle NUMBER;
    l_cycle_path VARCHAR2(4000);
BEGIN
    pkg_bom_domain.check_edge_cycle(203, 101, 7, l_has_cycle, l_cycle_path);
    IF l_has_cycle <> 1 OR l_cycle_path <> '101,202,203' THEN
        RAISE_APPLICATION_ERROR(-20985, 'BOM 环路预检未返回预期路径');
    END IF;

    pkg_bom_domain.check_edge_cycle(101, 401, 2, l_has_cycle, l_cycle_path);
    IF l_has_cycle <> 0 OR l_cycle_path IS NOT NULL THEN
        RAISE_APPLICATION_ERROR(-20986, 'BOM 环路预检误报合法边');
    END IF;

    DBMS_OUTPUT.PUT_LINE('PASS cycle preflight query');
END;
/

SAVEPOINT regression_owner;
DECLARE
    l_failed BOOLEAN := FALSE;
BEGIN
    BEGIN
        UPDATE material SET current_version_id = 3 WHERE material_id = 101;
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -2291 THEN
                l_failed := TRUE;
            ELSE
                RAISE;
            END IF;
    END;
    IF NOT l_failed THEN
        RAISE_APPLICATION_ERROR(-20993, '跨物料版本引用未被阻止');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS version ownership constraints');
END;
/
ROLLBACK TO regression_owner;

SAVEPOINT regression_stock;
DECLARE
    l_rows        SYS_REFCURSOR;
    l_value       NUMBER;
    l_text        VARCHAR2(200);
    l_shortage    NUMBER;
    l_shortages   NUMBER := 0;
    l_failed      BOOLEAN := FALSE;
BEGIN
    pkg_production_domain.open_lock_preview(7, l_rows);
    LOOP
        FETCH l_rows INTO l_value, l_text, l_text, l_value, l_value,
            l_value, l_value, l_value, l_value, l_shortage;
        EXIT WHEN l_rows%NOTFOUND;
        IF l_shortage > 0 THEN
            l_shortages := l_shortages + 1;
        END IF;
    END LOOP;
    CLOSE l_rows;
    IF l_shortages = 0 THEN
        RAISE_APPLICATION_ERROR(-20994, '锁料预览未识别库存缺口');
    END IF;

    BEGIN
        pkg_production_domain.approve_order(7, 1, 5);
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -20203 THEN
                l_failed := TRUE;
            ELSE
                RAISE;
            END IF;
    END;
    IF NOT l_failed THEN
        RAISE_APPLICATION_ERROR(-20995, '库存不足的订单被审核通过');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS stock lock preview and atomic rejection');
END;
/
ROLLBACK TO regression_stock;

SAVEPOINT regression_consumption;
DECLARE
    l_consumption_id NUMBER := 13;
    l_failed         BOOLEAN := FALSE;
BEGIN
    BEGIN
        pkg_trace_domain.save_consumption(l_consumption_id, 6, 9, 16);
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -20205 THEN
                l_failed := TRUE;
            ELSE
                RAISE;
            END IF;
    END;
    IF NOT l_failed THEN
        RAISE_APPLICATION_ERROR(-20996, '超已收数量消耗未被阻止');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS over-consumption guard');
END;
/
ROLLBACK TO regression_consumption;

SAVEPOINT regression_non_leaf;
DECLARE
    l_item_id        NUMBER;
    l_consumption_id NUMBER := NULL;
    l_failed         BOOLEAN := FALSE;
BEGIN
    INSERT INTO purchase_order_item (
        order_id, material_id, quantity, received_qty, unit_price)
    VALUES (1, 202, 1, 1, 1)
    RETURNING item_id INTO l_item_id;

    BEGIN
        pkg_trace_domain.save_consumption(l_consumption_id, 6, l_item_id, 1);
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -20201 THEN
                l_failed := TRUE;
            ELSE
                RAISE;
            END IF;
    END;
    IF NOT l_failed THEN
        RAISE_APPLICATION_ERROR(-20997, '非末级 BOM 物料被允许录入消耗');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS leaf-material consumption guard');
END;
/
ROLLBACK TO regression_non_leaf;

SAVEPOINT regression_batch_snapshot;
DECLARE
    l_consumption_id NUMBER := 13;
    l_inbound_id     NUMBER;
    l_completed      NUMBER;
    l_snapshot_qty   NUMBER;
    l_view_qty       NUMBER;
BEGIN
    pkg_trace_domain.save_consumption(l_consumption_id, 6, 9, 9);
    pkg_production_domain.report_completion(
        6, 1, 1, 'REGRESSION-BATCH-001', 5, l_inbound_id, l_completed);

    SELECT consume_qty
      INTO l_snapshot_qty
      FROM finish_batch_consumption
     WHERE inbound_id = l_inbound_id
       AND item_id = 9;

    SELECT consume_qty
      INTO l_view_qty
      FROM v_product_batch_trace
     WHERE inbound_id = l_inbound_id
       AND item_id = 9;

    IF l_snapshot_qty <> 1 OR l_view_qty <> 1 OR l_completed <> 0 THEN
        RAISE_APPLICATION_ERROR(-20998, '分批完工消耗增量快照不正确');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS partial-completion consumption snapshot');
END;
/
ROLLBACK TO regression_batch_snapshot;

DECLARE
    l_difference NUMBER;
BEGIN
    SELECT COUNT(*) INTO l_difference
      FROM v_stock_lock_reconciliation
     WHERE difference_qty <> 0;
    IF l_difference <> 0 THEN
        RAISE_APPLICATION_ERROR(-20999, '库存锁定汇总与锁定明细不一致');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS stock lock reconciliation');
END;
/

ROLLBACK;
PROMPT All Oracle domain regression checks passed.
EXIT SUCCESS
