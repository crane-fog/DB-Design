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
    l_message     VARCHAR2(4000);
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
                l_message := SQLERRM;
            ELSE
                RAISE;
            END IF;
    END;
    IF NOT l_failed THEN
        RAISE_APPLICATION_ERROR(-20995, '库存不足的订单被审核通过');
    END IF;
    -- 缺料提示必须是完整清单（物料名 + 缺口 + 单位），而不是只报第一个物料编号。
    IF INSTR(l_message, '库存不足：') = 0 OR INSTR(l_message, '缺少') = 0 THEN
        RAISE_APPLICATION_ERROR(-20995, '库存不足提示未返回完整缺料清单：' || l_message);
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

-- ============================================================================
-- 追溯口径回归：batch_consumption 是订单级累计消耗，含在制、未报工订单。
-- ============================================================================
SAVEPOINT regression_order_trace;
DECLARE
    l_order_rows    NUMBER;
    l_snapshot_rows NUMBER;
    l_batch_no      VARCHAR2(30 CHAR);
    l_wip_rows      NUMBER;
BEGIN
    -- 把订单 6 退回「已录消耗但尚未报工」的状态。
    DELETE FROM finish_batch_consumption WHERE inbound_id = 6;
    DELETE FROM finish_inbound WHERE inbound_id = 6;

    SELECT COUNT(*) INTO l_order_rows
      FROM v_order_material_trace WHERE order_id = 6;
    SELECT COUNT(*) INTO l_snapshot_rows
      FROM v_product_batch_trace WHERE order_id = 6;

    -- 订单级视图必须仍然给出全部累计消耗；报工增量快照视图则理应为空。
    IF l_order_rows <> 3 THEN
        RAISE_APPLICATION_ERROR(-20980, '未报工订单的订单级追溯行数不正确：' || l_order_rows);
    END IF;
    IF l_snapshot_rows <> 0 THEN
        RAISE_APPLICATION_ERROR(-20980, '报工增量快照视图不应包含未报工订单');
    END IF;

    SELECT MAX(batch_no) INTO l_batch_no
      FROM v_order_material_trace WHERE order_id = 6;
    IF l_batch_no IS NOT NULL THEN
        RAISE_APPLICATION_ERROR(-20980, '未报工订单的成品批次号应为 NULL');
    END IF;

    -- 质量影响分析口径：问题批次 item 9 必须能牵出在制订单 6。
    SELECT COUNT(*) INTO l_wip_rows
      FROM v_order_material_trace
     WHERE item_id = 9
       AND order_id = 6
       AND TRIM(production_status) = '生产中';
    IF l_wip_rows <> 1 THEN
        RAISE_APPLICATION_ERROR(-20980, '在制订单未进入原料反查结果');
    END IF;

    DBMS_OUTPUT.PUT_LINE('PASS order-level trace covers work-in-progress orders');
END;
/
ROLLBACK TO regression_order_trace;

DECLARE
    l_rows        SYS_REFCURSOR;
    l_material_id NUMBER;
    l_name        VARCHAR2(200);
    l_type        VARCHAR2(50);
    l_parent      NUMBER;
    l_depth       NUMBER;
    l_net         NUMBER;
    l_gross       NUMBER;
    l_loss        NUMBER;
    l_path        VARCHAR2(4000);
    l_is_leaf     NUMBER;
    l_count       NUMBER := 0;
    l_leaf_count  NUMBER := 0;
    l_net_501     NUMBER;
    l_gross_501   NUMBER;
BEGIN
    pkg_bom_domain.open_demand(101, 2, 1, l_rows);
    LOOP
        FETCH l_rows INTO l_material_id, l_name, l_type, l_parent, l_depth,
            l_net, l_gross, l_loss, l_path, l_is_leaf;
        EXIT WHEN l_rows%NOTFOUND;
        l_count := l_count + 1;
        IF l_is_leaf = 1 THEN
            l_leaf_count := l_leaf_count + 1;
        END IF;
        IF l_material_id = 501 THEN
            l_net_501 := l_net;
            l_gross_501 := l_gross;
        END IF;
    END LOOP;
    CLOSE l_rows;

    -- 101/V2 至少有 18 个直接子项，聚合后不可能少于这个数。
    IF l_count < 18 OR l_leaf_count = 0 THEN
        RAISE_APPLICATION_ERROR(-20981, '需求展开结果不完整：rows=' || l_count);
    END IF;
    -- 501 用量 32、损耗 3%：净需求 32，毛需求 CEIL(32 / 0.97) = 33。
    IF l_net_501 <> 32 OR l_gross_501 <> 33 THEN
        RAISE_APPLICATION_ERROR(
            -20981,
            '损耗补偿计算错误：net=' || l_net_501 || ', gross=' || l_gross_501);
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS demand expansion with loss compensation rows=' || l_count);
END;
/

DECLARE
    l_rows        SYS_REFCURSOR;
    l_product_id  NUMBER;
    l_name        VARCHAR2(200);
    l_version_id  NUMBER;
    l_version_no  VARCHAR2(20);
    l_status      VARCHAR2(20);
    l_quantity    NUMBER;
    l_accumulated NUMBER;
    l_depth       NUMBER;
    l_path        VARCHAR2(4000);
    l_count       NUMBER := 0;
    l_self_rows   NUMBER := 0;
    l_min_depth   NUMBER := 99;
BEGIN
    -- 203 只被 202 使用，202 又被 101/102/103 使用：有效版本口径下应为 4 行。
    pkg_bom_domain.open_reverse_usage(203, 7, 0, l_rows);
    LOOP
        FETCH l_rows INTO l_product_id, l_name, l_version_id, l_version_no,
            l_status, l_quantity, l_accumulated, l_depth, l_path;
        EXIT WHEN l_rows%NOTFOUND;
        l_count := l_count + 1;
        l_min_depth := LEAST(l_min_depth, l_depth);
        IF l_product_id = 203 THEN
            l_self_rows := l_self_rows + 1;
        END IF;
    END LOOP;
    CLOSE l_rows;

    IF l_count <> 4 THEN
        RAISE_APPLICATION_ERROR(-20982, '反向使用关系行数不正确：' || l_count);
    END IF;
    IF l_min_depth <> 1 THEN
        RAISE_APPLICATION_ERROR(-20982, '反向使用关系层级应从 1 开始');
    END IF;
    -- 起点物料自身不得作为「上层成品」出现。
    IF l_self_rows <> 0 THEN
        RAISE_APPLICATION_ERROR(-20982, '反向使用关系把起点物料当成了上层成品');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS reverse usage rows=' || l_count);
END;
/

DECLARE
    l_rows    SYS_REFCURSOR;
    l_failed  BOOLEAN := FALSE;
    l_code    NUMBER;
BEGIN
    -- 物料不存在必须是明确的 404（-20101），不能被版本归属检查掩盖成 400。
    BEGIN
        pkg_bom_domain.open_tree(999999, 2, 1, l_rows);
    EXCEPTION
        WHEN OTHERS THEN
            l_code := SQLCODE;
            l_failed := TRUE;
    END;
    IF NOT l_failed OR l_code <> -20101 THEN
        RAISE_APPLICATION_ERROR(-20983, '物料不存在未返回 not_found：' || l_code);
    END IF;

    -- 版本不属于该物料仍然是入参错误（-20001）。
    l_failed := FALSE;
    BEGIN
        pkg_bom_domain.open_tree(101, 3, 1, l_rows);
    EXCEPTION
        WHEN OTHERS THEN
            l_code := SQLCODE;
            l_failed := TRUE;
    END;
    IF NOT l_failed OR l_code <> -20001 THEN
        RAISE_APPLICATION_ERROR(-20983, '版本归属错误未返回 invalid_argument：' || l_code);
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS BOM query error classification');
END;
/

SAVEPOINT regression_deep_cycle;
DECLARE
    l_failed BOOLEAN := FALSE;
BEGIN
    -- 环不经过被校验版本的根物料的直接子项，而是深一层才闭合：
    -- 206(V10) -> 202(V6) -> 203(V7) -> 206，仍必须被拦截。
    BEGIN
        INSERT INTO bom (parent_material_id, child_material_id, version_id, quantity, loss_rate)
        VALUES (206, 202, 10, 1, 0);
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -20202 THEN
                l_failed := TRUE;
            ELSE
                RAISE;
            END IF;
    END;
    IF NOT l_failed THEN
        RAISE_APPLICATION_ERROR(-20984, '深层循环 BOM 未被阻止');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS deep cycle guard');
END;
/
ROLLBACK TO regression_deep_cycle;

SAVEPOINT regression_consumption_conflict;
DECLARE
    l_item_id        NUMBER;
    l_consumption_id NUMBER := NULL;
    l_failed         BOOLEAN := FALSE;
    l_remaining      NUMBER;
BEGIN
    INSERT INTO purchase_order_item (
        order_id, material_id, quantity, received_qty, unit_price)
    VALUES (5, 402, 5, 5, 27500)
    RETURNING item_id INTO l_item_id;

    pkg_trace_domain.save_consumption(l_consumption_id, 6, l_item_id, 1);
    IF l_consumption_id IS NULL THEN
        RAISE_APPLICATION_ERROR(-20987, '新增消耗记录未返回主键');
    END IF;

    -- 改挂到订单 6 下已存在消耗的采购明细（item 9），必须是领域冲突而不是裸 ORA-00001。
    BEGIN
        pkg_trace_domain.save_consumption(l_consumption_id, 6, 9, 1);
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -20201 THEN
                l_failed := TRUE;
            ELSE
                RAISE;
            END IF;
    END;
    IF NOT l_failed THEN
        RAISE_APPLICATION_ERROR(-20987, '重复的订单+采购明细消耗未被拦截');
    END IF;

    -- 未分配到成品批次的消耗可以删除。
    pkg_trace_domain.delete_consumption(l_consumption_id);
    l_remaining := pkg_trace_domain.remaining_received_qty(l_item_id);
    IF l_remaining <> 5 THEN
        RAISE_APPLICATION_ERROR(-20987, '删除消耗后可用收货量未回滚：' || l_remaining);
    END IF;

    -- 已分配到成品批次的消耗不可删除。
    l_failed := FALSE;
    BEGIN
        pkg_trace_domain.delete_consumption(13);
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -20204 THEN
                l_failed := TRUE;
            ELSE
                RAISE;
            END IF;
    END;
    IF NOT l_failed THEN
        RAISE_APPLICATION_ERROR(-20987, '已分配到成品批次的消耗被删除');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS consumption conflict and delete guards');
END;
/
ROLLBACK TO regression_consumption_conflict;

SAVEPOINT regression_order_lifecycle;
DECLARE
    l_status      production_order.status%TYPE;
    l_finished    NUMBER;
    l_locked      NUMBER;
    l_active      NUMBER;
    l_inbound_id  NUMBER;
    l_completed   NUMBER;
    l_failed      BOOLEAN := FALSE;
BEGIN
    -- 先补足订单 7 的库存缺口，再跑一遍「审核 -> 开工 -> 报工至完工」。
    UPDATE material_stock
       SET available_qty = available_qty + 1000000
     WHERE material_id IN (
         SELECT child_material_id FROM bom
          WHERE version_id = 2 AND parent_material_id = 101);

    pkg_production_domain.approve_order(7, 1, 5);
    SELECT status INTO l_status FROM production_order WHERE order_id = 7;
    SELECT COUNT(*) INTO l_locked
      FROM stock_lock WHERE order_id = 7 AND status = '已锁定';
    IF TRIM(l_status) <> '待排产' OR l_locked <> 18 THEN
        RAISE_APPLICATION_ERROR(
            -20988, '审核通过后状态或锁定不正确：' || l_status || '/' || l_locked);
    END IF;

    pkg_production_domain.start_order(7);
    SELECT status INTO l_status FROM production_order WHERE order_id = 7;
    IF TRIM(l_status) <> '生产中' THEN
        RAISE_APPLICATION_ERROR(-20988, '开工后状态不正确：' || l_status);
    END IF;

    -- 一次报满计划量：订单转「已完工」，剩余锁定必须全部结算成「已消耗」。
    pkg_production_domain.report_completion(
        7, 10, 10, 'REGRESSION-ORDER7-B01', 5, l_inbound_id, l_completed);
    SELECT status, finished_qty INTO l_status, l_finished
      FROM production_order WHERE order_id = 7;
    SELECT COUNT(*) INTO l_active
      FROM stock_lock WHERE order_id = 7 AND status = '已锁定';
    IF l_completed <> 1 OR TRIM(l_status) <> '已完工' OR l_finished <> 10 THEN
        RAISE_APPLICATION_ERROR(
            -20988, '完工报工未结束订单：' || l_status || '/' || l_finished);
    END IF;
    IF l_active <> 0 THEN
        RAISE_APPLICATION_ERROR(-20988, '完工后仍有未结算的库存锁定：' || l_active);
    END IF;

    -- 已完工订单不可再取消。
    BEGIN
        pkg_production_domain.cancel_order(7);
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -20201 THEN
                l_failed := TRUE;
            ELSE
                RAISE;
            END IF;
    END;
    IF NOT l_failed THEN
        RAISE_APPLICATION_ERROR(-20988, '已完工订单被取消');
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS order lifecycle approve/start/report/settle');
END;
/
ROLLBACK TO regression_order_lifecycle;

SAVEPOINT regression_cancel_order;
DECLARE
    l_status production_order.status%TYPE;
    l_failed BOOLEAN := FALSE;
BEGIN
    -- 已报工的在制订单不可取消。
    BEGIN
        pkg_production_domain.cancel_order(6);
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -20201 THEN
                l_failed := TRUE;
            ELSE
                RAISE;
            END IF;
    END;
    IF NOT l_failed THEN
        RAISE_APPLICATION_ERROR(-20989, '已报工订单被取消');
    END IF;

    -- 待审核、无锁定、无报工的订单可以取消。
    pkg_production_domain.cancel_order(7);
    SELECT status INTO l_status FROM production_order WHERE order_id = 7;
    IF TRIM(l_status) <> '已取消' THEN
        RAISE_APPLICATION_ERROR(-20989, '待审核订单取消失败：' || l_status);
    END IF;
    DBMS_OUTPUT.PUT_LINE('PASS cancel order guards');
END;
/
ROLLBACK TO regression_cancel_order;

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
