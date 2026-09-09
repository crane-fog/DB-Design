-- ============================================================================
-- 工业制造物料进销存管理系统 · Oracle 领域逻辑
-- 目标版本：Oracle 23ai Free / FREEPDB1
-- 前置脚本：01_schema_forklift.sql
-- ============================================================================

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON

-- ---------- 1. 统一领域错误 ----------

CREATE OR REPLACE PACKAGE pkg_app_error AUTHID DEFINER AS
    c_invalid_argument CONSTANT PLS_INTEGER := -20001;
    c_not_found        CONSTANT PLS_INTEGER := -20101;
    c_state_conflict   CONSTANT PLS_INTEGER := -20201;
    c_bom_cycle        CONSTANT PLS_INTEGER := -20202;
    c_stock_conflict   CONSTANT PLS_INTEGER := -20203;
    c_trace_immutable  CONSTANT PLS_INTEGER := -20204;
    c_over_consumption CONSTANT PLS_INTEGER := -20205;

    PROCEDURE fail(p_code IN PLS_INTEGER, p_message IN VARCHAR2);
END pkg_app_error;
/

CREATE OR REPLACE PACKAGE BODY pkg_app_error AS
    PROCEDURE fail(p_code IN PLS_INTEGER, p_message IN VARCHAR2) IS
    BEGIN
        IF p_code < -20999 OR p_code > -20000 THEN
            RAISE_APPLICATION_ERROR(-20001, '非法的领域错误编号');
        END IF;
        RAISE_APPLICATION_ERROR(p_code, SUBSTR(p_message, 1, 1800));
    END fail;
END pkg_app_error;
/

-- ---------- 2. 当前 BOM 与追溯只读视图 ----------
--
-- 追溯口径（定稿）：
--   * batch_consumption 是「订单级累计消耗」，是包含在制订单的权威事实；
--     所有对外追溯（正向、原料反查、质量影响分析）都必须以它为数据源。
--   * finish_batch_consumption 只表示「报工时新增登记的消耗增量」，
--     不能视为某个成品批次的真实用料分摊，因此仅供内部快照视图使用。
--   * 若以后要做真正精确的成品批次追溯，需要在报工请求中显式提交批次用料
--     分配，或引入独立的领料/投料事件，不能按报工数量反推。

CREATE OR REPLACE VIEW v_effective_bom_edge AS
SELECT b.bom_id,
       b.version_id,
       bv.version_no,
       b.parent_material_id,
       parent_material.material_name AS parent_material_name,
       b.child_material_id,
       child_material.material_name AS child_material_name,
       b.quantity,
       b.loss_rate
  FROM material parent_material
  JOIN bom_version bv
    ON bv.version_id = parent_material.current_version_id
   AND bv.material_id = parent_material.material_id
   AND bv.effective_date <= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
   AND (bv.expire_date IS NULL
        OR bv.expire_date >= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)))
  JOIN bom b
    ON b.version_id = bv.version_id
   AND b.parent_material_id = parent_material.material_id
  JOIN material child_material
    ON child_material.material_id = b.child_material_id;

-- 订单级追溯视图（对外权威口径）。以 batch_consumption 为事实来源，
-- 因此在制、未报工的生产订单同样有数据；batch_no 取该订单最新完工批次作为
-- 展示用代表值，未报工时为 NULL，与历史 API 语义一致。
CREATE OR REPLACE VIEW v_order_material_trace AS
SELECT bc.consumption_id,
       bc.order_id,
       bc.item_id,
       bc.consume_qty,
       po.material_id AS product_material_id,
       product_material.material_name AS product_material_name,
       po.plan_qty,
       po.finished_qty,
       po.status AS production_status,
       (SELECT MAX(inbound.batch_no)
          FROM finish_inbound inbound
         WHERE inbound.order_id = bc.order_id) AS batch_no,
       poi.order_id AS purchase_order_id,
       poi.material_id AS input_material_id,
       input_material.material_name AS input_material_name,
       poi.quantity AS purchase_quantity,
       poi.received_qty,
       poi.unit_price,
       purchase.supplier_id,
       supplier.supplier_name,
       (SELECT MIN(receive_record.receive_date)
          FROM receive_record
         WHERE receive_record.order_id = poi.order_id
           AND receive_record.material_id = poi.material_id) AS first_receive_date
  FROM batch_consumption bc
  JOIN production_order po
    ON po.order_id = bc.order_id
  JOIN material product_material
    ON product_material.material_id = po.material_id
  JOIN purchase_order_item poi
    ON poi.item_id = bc.item_id
  JOIN material input_material
    ON input_material.material_id = poi.material_id
  JOIN purchase_order purchase
    ON purchase.order_id = poi.order_id
  JOIN supplier
    ON supplier.supplier_id = purchase.supplier_id;

-- 消耗记录列表/详情视图，等价于订单级追溯视图去掉成品批次号。
CREATE OR REPLACE VIEW v_batch_consumption_detail AS
SELECT consumption_id,
       order_id,
       item_id,
       consume_qty,
       product_material_id,
       product_material_name,
       plan_qty,
       finished_qty,
       production_status,
       purchase_order_id,
       input_material_id,
       input_material_name,
       purchase_quantity,
       received_qty,
       unit_price,
       supplier_id,
       supplier_name,
       first_receive_date
  FROM v_order_material_trace;

-- 内部快照视图：报工增量登记明细，仅用于核对「哪一次报工登记了哪些增量」。
-- 粒度与订单级累计消耗不同，不得作为对外追溯或质量影响分析的数据源。
CREATE OR REPLACE VIEW v_product_batch_trace AS
SELECT inbound.inbound_id,
       inbound.batch_no,
       inbound.order_id,
       inbound.material_id AS product_material_id,
       product_material.material_name AS product_material_name,
       production.status AS production_status,
       snapshot.item_id,
       purchase_item.material_id AS input_material_id,
       input_material.material_name AS input_material_name,
       snapshot.consume_qty,
       purchase_item.order_id AS purchase_order_id,
       purchase.supplier_id,
       supplier.supplier_name,
       (SELECT MIN(receive_record.receive_date)
          FROM receive_record
         WHERE receive_record.order_id = purchase_item.order_id
           AND receive_record.material_id = purchase_item.material_id) AS first_receive_date
  FROM finish_inbound inbound
  JOIN production_order production
    ON production.order_id = inbound.order_id
  JOIN material product_material
    ON product_material.material_id = inbound.material_id
  JOIN finish_batch_consumption snapshot
    ON snapshot.inbound_id = inbound.inbound_id
  JOIN purchase_order_item purchase_item
    ON purchase_item.item_id = snapshot.item_id
  JOIN material input_material
    ON input_material.material_id = purchase_item.material_id
  JOIN purchase_order purchase
    ON purchase.order_id = purchase_item.order_id
  JOIN supplier
    ON supplier.supplier_id = purchase.supplier_id;

-- 内部快照视图：v_product_batch_trace 的原材料视角投影，同样仅供内部核对。
CREATE OR REPLACE VIEW v_material_batch_trace AS
SELECT item_id,
       input_material_id,
       input_material_name,
       supplier_id,
       supplier_name,
       purchase_order_id,
       first_receive_date,
       order_id,
       batch_no,
       product_material_id,
       product_material_name,
       production_status,
       consume_qty
  FROM v_product_batch_trace;

CREATE OR REPLACE VIEW v_stock_lock_reconciliation AS
WITH active_lock AS (
    SELECT material_id, SUM(lock_qty) AS active_locked_qty
      FROM stock_lock
     WHERE status = '已锁定'
     GROUP BY material_id
)
SELECT stock.material_id,
       material.material_name,
       stock.locked_qty,
       NVL(active_lock.active_locked_qty, 0) AS active_locked_qty,
       stock.locked_qty - NVL(active_lock.active_locked_qty, 0) AS difference_qty
  FROM material_stock stock
  JOIN material
    ON material.material_id = stock.material_id
  LEFT JOIN active_lock
    ON active_lock.material_id = stock.material_id;

-- ---------- 3. BOM 递归、反向使用关系和需求展开 ----------

CREATE OR REPLACE PACKAGE pkg_bom_domain AUTHID DEFINER AS
    c_max_depth CONSTANT PLS_INTEGER := 50;

    PROCEDURE assert_version_acyclic(
        p_material_id IN NUMBER,
        p_version_id  IN NUMBER);

    PROCEDURE check_edge_cycle(
        p_parent_material_id IN NUMBER,
        p_child_material_id  IN NUMBER,
        p_version_id         IN NUMBER,
        p_has_cycle          OUT NUMBER,
        p_cycle_path         OUT VARCHAR2);

    PROCEDURE open_tree(
        p_material_id IN NUMBER,
        p_version_id  IN NUMBER,
        p_root_qty    IN NUMBER DEFAULT 1,
        p_rows        OUT SYS_REFCURSOR);

    PROCEDURE open_demand(
        p_material_id IN NUMBER,
        p_version_id  IN NUMBER,
        p_quantity    IN NUMBER,
        p_rows        OUT SYS_REFCURSOR);

    PROCEDURE open_reverse_usage(
        p_material_id     IN NUMBER,
        p_version_id      IN NUMBER,
        p_include_history IN NUMBER,
        p_rows            OUT SYS_REFCURSOR);

    FUNCTION is_order_leaf_material(
        p_order_id    IN NUMBER,
        p_material_id IN NUMBER)
    RETURN NUMBER;
END pkg_bom_domain;
/


-- ---------- 4. 批次消耗写入与分批完工快照 ----------

CREATE OR REPLACE PACKAGE pkg_trace_domain AUTHID DEFINER AS
    PROCEDURE save_consumption(
        p_consumption_id IN OUT NUMBER,
        p_order_id       IN NUMBER,
        p_item_id        IN NUMBER,
        p_consume_qty    IN NUMBER);

    PROCEDURE delete_consumption(p_consumption_id IN NUMBER);

    PROCEDURE allocate_to_finish_batch(
        p_order_id   IN NUMBER,
        p_inbound_id IN NUMBER);

    FUNCTION remaining_received_qty(
        p_item_id                IN NUMBER,
        p_exclude_consumption_id IN NUMBER DEFAULT NULL)
    RETURN NUMBER;
END pkg_trace_domain;
/

CREATE OR REPLACE PACKAGE BODY pkg_trace_domain AS
    PROCEDURE load_order_for_consumption(
        p_order_id       IN NUMBER,
        p_status         OUT production_order.status%TYPE,
        p_material_id    OUT production_order.material_id%TYPE,
        p_version_id     OUT production_order.version_id%TYPE) IS
    BEGIN
        BEGIN
            SELECT status, material_id, version_id
              INTO p_status, p_material_id, p_version_id
              FROM production_order
             WHERE order_id = p_order_id;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                -- 与历史 API 语义一致：消耗记录里的订单/明细引用不合法算入参错误（400），
                -- 只有「消耗记录本身不存在」才是 404。
                pkg_app_error.fail(pkg_app_error.c_invalid_argument, '生产订单不存在');
        END;

        -- 收紧点：旧实现只要求 actual_start 非空（已完工订单也能改），
        -- 现在必须处于「生产中」，避免终态订单的追溯链被改动。
        IF TRIM(p_status) <> '生产中' THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '仅生产中订单可维护消耗记录');
        END IF;
    END load_order_for_consumption;

    FUNCTION allocated_qty(
        p_order_id IN NUMBER,
        p_item_id  IN NUMBER)
    RETURN NUMBER IS
        l_quantity NUMBER;
    BEGIN
        SELECT NVL(SUM(snapshot.consume_qty), 0)
          INTO l_quantity
          FROM finish_batch_consumption snapshot
          JOIN finish_inbound inbound
            ON inbound.inbound_id = snapshot.inbound_id
         WHERE inbound.order_id = p_order_id
           AND snapshot.item_id = p_item_id;
        RETURN l_quantity;
    END allocated_qty;

    FUNCTION remaining_received_qty(
        p_item_id                IN NUMBER,
        p_exclude_consumption_id IN NUMBER DEFAULT NULL)
    RETURN NUMBER IS
        l_received purchase_order_item.received_qty%TYPE;
        l_consumed NUMBER;
    BEGIN
        BEGIN
            SELECT received_qty
              INTO l_received
              FROM purchase_order_item
             WHERE item_id = p_item_id;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                pkg_app_error.fail(pkg_app_error.c_not_found, '采购订单明细不存在');
        END;

        SELECT NVL(SUM(consume_qty), 0)
          INTO l_consumed
          FROM batch_consumption
         WHERE item_id = p_item_id
           AND (p_exclude_consumption_id IS NULL
                OR consumption_id <> p_exclude_consumption_id);

        RETURN l_received - l_consumed;
    END remaining_received_qty;

    PROCEDURE save_consumption(
        p_consumption_id IN OUT NUMBER,
        p_order_id       IN NUMBER,
        p_item_id        IN NUMBER,
        p_consume_qty    IN NUMBER) IS
        l_status              production_order.status%TYPE;
        l_product_material_id production_order.material_id%TYPE;
        l_version_id          production_order.version_id%TYPE;
        l_input_material_id   purchase_order_item.material_id%TYPE;
        l_existing_order_id   batch_consumption.order_id%TYPE;
        l_existing_item_id    batch_consumption.item_id%TYPE;
        l_existing_count      NUMBER;
        l_allocated_qty       NUMBER;
        l_remaining_qty       NUMBER;
    BEGIN
        IF p_order_id IS NULL OR p_order_id <= 0
           OR p_item_id IS NULL OR p_item_id <= 0
           OR p_consume_qty IS NULL OR p_consume_qty <= 0 THEN
            pkg_app_error.fail(pkg_app_error.c_invalid_argument, '订单、采购明细和消耗数量必须大于 0');
        END IF;

        load_order_for_consumption(
            p_order_id,
            l_status,
            l_product_material_id,
            l_version_id);

        BEGIN
            SELECT material_id
              INTO l_input_material_id
              FROM purchase_order_item
             WHERE item_id = p_item_id;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                pkg_app_error.fail(pkg_app_error.c_invalid_argument, '采购订单明细不存在');
        END;

        IF pkg_bom_domain.is_order_leaf_material(p_order_id, l_input_material_id) <> 1 THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '采购物料不是订单 BOM 的末级物料');
        END IF;

        IF p_consumption_id IS NOT NULL THEN
            BEGIN
                SELECT order_id, item_id
                  INTO l_existing_order_id, l_existing_item_id
                  FROM batch_consumption
                 WHERE consumption_id = p_consumption_id
                 FOR UPDATE;
            EXCEPTION
                WHEN NO_DATA_FOUND THEN
                    pkg_app_error.fail(pkg_app_error.c_not_found, '批次消耗关系不存在');
            END;

            IF l_existing_order_id <> p_order_id THEN
                pkg_app_error.fail(pkg_app_error.c_state_conflict, '消耗记录所属生产订单不可变更');
            END IF;

            l_allocated_qty := allocated_qty(l_existing_order_id, l_existing_item_id);
            IF l_allocated_qty > 0 AND l_existing_item_id <> p_item_id THEN
                pkg_app_error.fail(pkg_app_error.c_trace_immutable, '已分配到成品批次的采购明细不可变更');
            END IF;

            -- 改挂到同订单下已有消耗的采购明细，会撞 uq_bc_order_item；
            -- 提前判成领域冲突（409），不要落到未映射的 ORA-00001（500）。
            SELECT COUNT(*)
              INTO l_existing_count
              FROM batch_consumption
             WHERE order_id = p_order_id
               AND item_id = p_item_id
               AND consumption_id <> p_consumption_id;
            IF l_existing_count > 0 THEN
                pkg_app_error.fail(
                    pkg_app_error.c_state_conflict,
                    '目标采购明细在该生产订单下已存在消耗记录');
            END IF;
        ELSE
            SELECT COUNT(*)
              INTO l_existing_count
              FROM batch_consumption
             WHERE order_id = p_order_id
               AND item_id = p_item_id;
            IF l_existing_count > 0 THEN
                pkg_app_error.fail(pkg_app_error.c_state_conflict, '该原材料已存在消耗记录，请使用更新接口');
            END IF;
        END IF;

        l_allocated_qty := allocated_qty(p_order_id, p_item_id);
        IF p_consume_qty < l_allocated_qty THEN
            pkg_app_error.fail(
                pkg_app_error.c_trace_immutable,
                '累计消耗不得小于已分配到成品批次的数量');
        END IF;

        l_remaining_qty := remaining_received_qty(p_item_id, p_consumption_id);
        IF p_consume_qty > l_remaining_qty THEN
            pkg_app_error.fail(
                pkg_app_error.c_over_consumption,
                '采购明细累计消耗超过已收数量');
        END IF;

        BEGIN
            IF p_consumption_id IS NULL THEN
                INSERT INTO batch_consumption (order_id, item_id, consume_qty)
                VALUES (p_order_id, p_item_id, p_consume_qty)
                RETURNING consumption_id INTO p_consumption_id;
            ELSE
                UPDATE batch_consumption
                   SET item_id = p_item_id,
                       consume_qty = p_consume_qty
                 WHERE consumption_id = p_consumption_id;
            END IF;
        EXCEPTION
            WHEN DUP_VAL_ON_INDEX THEN
                pkg_app_error.fail(
                    pkg_app_error.c_state_conflict,
                    '目标采购明细在该生产订单下已存在消耗记录');
        END;
    END save_consumption;

    PROCEDURE delete_consumption(p_consumption_id IN NUMBER) IS
        l_order_id     batch_consumption.order_id%TYPE;
        l_item_id      batch_consumption.item_id%TYPE;
        l_status       production_order.status%TYPE;
        l_allocated    NUMBER;
    BEGIN
        IF p_consumption_id IS NULL OR p_consumption_id <= 0 THEN
            pkg_app_error.fail(pkg_app_error.c_invalid_argument, '消耗记录编号必须大于 0');
        END IF;

        BEGIN
            SELECT consumption.order_id, consumption.item_id, production.status
              INTO l_order_id, l_item_id, l_status
              FROM batch_consumption consumption
              JOIN production_order production
                ON production.order_id = consumption.order_id
             WHERE consumption.consumption_id = p_consumption_id
             FOR UPDATE OF consumption.consume_qty;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                pkg_app_error.fail(pkg_app_error.c_not_found, '批次消耗关系不存在');
        END;

        IF TRIM(l_status) <> '生产中' THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '仅生产中订单可删除消耗记录');
        END IF;

        l_allocated := allocated_qty(l_order_id, l_item_id);
        IF l_allocated > 0 THEN
            pkg_app_error.fail(pkg_app_error.c_trace_immutable, '已分配到成品批次的消耗不可删除');
        END IF;

        DELETE FROM batch_consumption
         WHERE consumption_id = p_consumption_id;
    END delete_consumption;

    PROCEDURE allocate_to_finish_batch(
        p_order_id   IN NUMBER,
        p_inbound_id IN NUMBER) IS
        l_inbound_order_id finish_inbound.order_id%TYPE;
        l_allocated_qty    NUMBER;
        l_delta_qty        NUMBER;
    BEGIN
        BEGIN
            SELECT order_id
              INTO l_inbound_order_id
              FROM finish_inbound
             WHERE inbound_id = p_inbound_id;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                pkg_app_error.fail(pkg_app_error.c_not_found, '完工入库批次不存在');
        END;

        IF l_inbound_order_id <> p_order_id THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '完工批次不属于指定生产订单');
        END IF;

        FOR consumption IN (
            SELECT consumption_id, item_id, consume_qty
              FROM batch_consumption
             WHERE order_id = p_order_id
             ORDER BY item_id
        ) LOOP
            l_allocated_qty := allocated_qty(p_order_id, consumption.item_id);
            l_delta_qty := consumption.consume_qty - l_allocated_qty;

            IF l_delta_qty < 0 THEN
                pkg_app_error.fail(
                    pkg_app_error.c_trace_immutable,
                    '累计消耗小于历史完工批次快照');
            ELSIF l_delta_qty > 0 THEN
                INSERT INTO finish_batch_consumption (
                    inbound_id,
                    item_id,
                    consume_qty,
                    allocated_time)
                VALUES (
                    p_inbound_id,
                    consumption.item_id,
                    l_delta_qty,
                    SYS_EXTRACT_UTC(SYSTIMESTAMP));
            END IF;
        END LOOP;
    END allocate_to_finish_batch;
END pkg_trace_domain;
/

CREATE OR REPLACE TRIGGER ct_batch_consumption_guard
FOR INSERT OR UPDATE OR DELETE ON batch_consumption
COMPOUND TRIGGER
    TYPE item_set_type IS TABLE OF BOOLEAN INDEX BY PLS_INTEGER;
    g_items item_set_type;

    PROCEDURE remember_item(p_item_id NUMBER) IS
    BEGIN
        IF p_item_id IS NOT NULL THEN
            g_items(TRUNC(p_item_id)) := TRUE;
        END IF;
    END remember_item;

    PROCEDURE assert_mutable(
        p_order_id      NUMBER,
        p_item_id       NUMBER,
        p_consume_qty   NUMBER,
        p_is_delete     BOOLEAN,
        p_old_order_id  NUMBER DEFAULT NULL,
        p_old_item_id   NUMBER DEFAULT NULL) IS
        l_status            production_order.status%TYPE;
        l_input_material_id purchase_order_item.material_id%TYPE;
        l_allocated_qty     NUMBER;
    BEGIN
        BEGIN
            SELECT status
              INTO l_status
              FROM production_order
             WHERE order_id = p_order_id;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                pkg_app_error.fail(pkg_app_error.c_invalid_argument, '生产订单不存在');
        END;

        IF TRIM(l_status) <> '生产中' THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '仅生产中订单可维护消耗记录');
        END IF;

        IF p_is_delete THEN
            SELECT NVL(SUM(snapshot.consume_qty), 0)
              INTO l_allocated_qty
              FROM finish_batch_consumption snapshot
              JOIN finish_inbound inbound
                ON inbound.inbound_id = snapshot.inbound_id
             WHERE inbound.order_id = p_order_id
               AND snapshot.item_id = p_item_id;
            IF l_allocated_qty > 0 THEN
                pkg_app_error.fail(pkg_app_error.c_trace_immutable, '已分配到成品批次的消耗不可删除');
            END IF;
            RETURN;
        END IF;

        BEGIN
            SELECT material_id
              INTO l_input_material_id
              FROM purchase_order_item
             WHERE item_id = p_item_id;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                pkg_app_error.fail(pkg_app_error.c_invalid_argument, '采购订单明细不存在');
        END;

        IF pkg_bom_domain.is_order_leaf_material(p_order_id, l_input_material_id) <> 1 THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '采购物料不是订单 BOM 的末级物料');
        END IF;

        SELECT NVL(SUM(snapshot.consume_qty), 0)
          INTO l_allocated_qty
          FROM finish_batch_consumption snapshot
          JOIN finish_inbound inbound
            ON inbound.inbound_id = snapshot.inbound_id
         WHERE inbound.order_id = NVL(p_old_order_id, p_order_id)
           AND snapshot.item_id = NVL(p_old_item_id, p_item_id);

        IF l_allocated_qty > 0
           AND (NVL(p_old_order_id, p_order_id) <> p_order_id
                OR NVL(p_old_item_id, p_item_id) <> p_item_id) THEN
            pkg_app_error.fail(pkg_app_error.c_trace_immutable, '已分配到成品批次的消耗归属不可变更');
        END IF;

        IF p_consume_qty < l_allocated_qty THEN
            pkg_app_error.fail(pkg_app_error.c_trace_immutable, '累计消耗不得小于已分配数量');
        END IF;
    END assert_mutable;

    BEFORE EACH ROW IS
    BEGIN
        IF INSERTING THEN
            assert_mutable(:NEW.order_id, :NEW.item_id, :NEW.consume_qty, FALSE);
            remember_item(:NEW.item_id);
        ELSIF UPDATING THEN
            assert_mutable(
                :NEW.order_id,
                :NEW.item_id,
                :NEW.consume_qty,
                FALSE,
                :OLD.order_id,
                :OLD.item_id);
            remember_item(:OLD.item_id);
            remember_item(:NEW.item_id);
        ELSE
            assert_mutable(:OLD.order_id, :OLD.item_id, :OLD.consume_qty, TRUE);
            remember_item(:OLD.item_id);
        END IF;
    END BEFORE EACH ROW;

    AFTER STATEMENT IS
        l_item_id      PLS_INTEGER;
        l_received_qty NUMBER;
        l_consumed_qty NUMBER;
    BEGIN
        l_item_id := g_items.FIRST;
        WHILE l_item_id IS NOT NULL LOOP
            SELECT received_qty
              INTO l_received_qty
              FROM purchase_order_item
             WHERE item_id = l_item_id;

            SELECT NVL(SUM(consume_qty), 0)
              INTO l_consumed_qty
              FROM batch_consumption
             WHERE item_id = l_item_id;

            IF l_consumed_qty > l_received_qty THEN
                pkg_app_error.fail(
                    pkg_app_error.c_over_consumption,
                    '采购明细累计消耗超过已收数量');
            END IF;
            l_item_id := g_items.NEXT(l_item_id);
        END LOOP;
    END AFTER STATEMENT;
END ct_batch_consumption_guard;
/

-- ---------- 5. 生产订单、库存锁定与完工报工事务 ----------

CREATE OR REPLACE PACKAGE pkg_production_domain AUTHID DEFINER AS
    PROCEDURE open_lock_preview(
        p_order_id IN NUMBER,
        p_rows     OUT SYS_REFCURSOR);

    PROCEDURE approve_order(
        p_order_id    IN NUMBER,
        p_approved    IN NUMBER,
        p_operator_id IN NUMBER);

    PROCEDURE start_order(p_order_id IN NUMBER);

    PROCEDURE cancel_order(p_order_id IN NUMBER);

    PROCEDURE report_completion(
        p_order_id        IN NUMBER,
        p_finish_qty      IN NUMBER,
        p_qualified_qty   IN NUMBER,
        p_batch_no        IN VARCHAR2,
        p_operator_id     IN NUMBER,
        p_inbound_id      OUT NUMBER,
        p_order_completed OUT NUMBER);
END pkg_production_domain;
/

CREATE OR REPLACE PACKAGE BODY pkg_production_domain AS
    PROCEDURE load_order(
        p_order_id    IN NUMBER,
        p_for_update  IN BOOLEAN,
        p_material_id OUT production_order.material_id%TYPE,
        p_version_id  OUT production_order.version_id%TYPE,
        p_plan_qty    OUT production_order.plan_qty%TYPE,
        p_finished_qty OUT production_order.finished_qty%TYPE,
        p_status      OUT production_order.status%TYPE) IS
    BEGIN
        IF p_order_id IS NULL OR p_order_id <= 0 THEN
            pkg_app_error.fail(pkg_app_error.c_invalid_argument, '生产订单编号必须大于 0');
        END IF;

        BEGIN
            IF p_for_update THEN
                SELECT material_id, version_id, plan_qty, finished_qty, status
                  INTO p_material_id, p_version_id, p_plan_qty, p_finished_qty, p_status
                  FROM production_order
                 WHERE order_id = p_order_id
                 FOR UPDATE;
            ELSE
                SELECT material_id, version_id, plan_qty, finished_qty, status
                  INTO p_material_id, p_version_id, p_plan_qty, p_finished_qty, p_status
                  FROM production_order
                 WHERE order_id = p_order_id;
            END IF;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                pkg_app_error.fail(pkg_app_error.c_not_found, '生产订单不存在');
        END;
    END load_order;

    PROCEDURE assert_direct_bom(
        p_material_id IN NUMBER,
        p_version_id  IN NUMBER) IS
        l_count NUMBER;
    BEGIN
        SELECT COUNT(*)
          INTO l_count
          FROM bom
         WHERE version_id = p_version_id
           AND parent_material_id = p_material_id;
        IF l_count = 0 THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '生产订单所选 BOM 没有直接子项');
        END IF;
    END assert_direct_bom;

    PROCEDURE assert_existing_locks_valid(
        p_order_id    IN NUMBER,
        p_material_id IN NUMBER,
        p_version_id  IN NUMBER,
        p_plan_qty    IN NUMBER) IS
        l_unrelated_material_id stock_lock.material_id%TYPE;
        l_excess_material_id    stock_lock.material_id%TYPE;
    BEGIN
        BEGIN
            SELECT material_id
              INTO l_unrelated_material_id
              FROM (
                  SELECT active_lock.material_id
                    FROM stock_lock active_lock
                   WHERE active_lock.order_id = p_order_id
                     AND active_lock.status = '已锁定'
                     AND NOT EXISTS (
                         SELECT 1
                           FROM bom edge
                          WHERE edge.version_id = p_version_id
                            AND edge.parent_material_id = p_material_id
                            AND edge.child_material_id = active_lock.material_id)
                   ORDER BY active_lock.material_id)
             WHERE ROWNUM = 1;
            pkg_app_error.fail(
                pkg_app_error.c_stock_conflict,
                '订单存在非当前 BOM 物料 ' || l_unrelated_material_id || ' 的有效锁定');
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                NULL;
        END;

        BEGIN
            SELECT material_id
              INTO l_excess_material_id
              FROM (
                  SELECT edge.child_material_id AS material_id,
                         SUM(active_lock.lock_qty) AS locked_qty,
                         CEIL(p_plan_qty * edge.quantity / (1 - edge.loss_rate) * 100) / 100
                             AS required_qty
                    FROM bom edge
                    JOIN stock_lock active_lock
                      ON active_lock.order_id = p_order_id
                     AND active_lock.material_id = edge.child_material_id
                     AND active_lock.status = '已锁定'
                   WHERE edge.version_id = p_version_id
                     AND edge.parent_material_id = p_material_id
                   GROUP BY edge.child_material_id, edge.quantity, edge.loss_rate
                  HAVING SUM(active_lock.lock_qty) >
                         CEIL(p_plan_qty * edge.quantity / (1 - edge.loss_rate) * 100) / 100
                   ORDER BY edge.child_material_id)
             WHERE ROWNUM = 1;
            pkg_app_error.fail(
                pkg_app_error.c_stock_conflict,
                '物料 ' || l_excess_material_id || ' 的有效锁定数量超过订单需求');
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                NULL;
        END;
    END assert_existing_locks_valid;

    PROCEDURE open_lock_preview(
        p_order_id IN NUMBER,
        p_rows     OUT SYS_REFCURSOR) IS
        l_material_id production_order.material_id%TYPE;
        l_version_id  production_order.version_id%TYPE;
        l_plan_qty    production_order.plan_qty%TYPE;
        l_finished_qty production_order.finished_qty%TYPE;
        l_status      production_order.status%TYPE;
    BEGIN
        load_order(
            p_order_id,
            FALSE,
            l_material_id,
            l_version_id,
            l_plan_qty,
            l_finished_qty,
            l_status);
        IF TRIM(l_status) <> '待审核' THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '仅待审核订单可预览物料锁定');
        END IF;

        assert_direct_bom(l_material_id, l_version_id);
        assert_existing_locks_valid(p_order_id, l_material_id, l_version_id, l_plan_qty);

        OPEN p_rows FOR
            WITH active_lock AS (
                SELECT material_id, SUM(lock_qty) AS locked_qty
                  FROM stock_lock
                 WHERE order_id = p_order_id
                   AND status = '已锁定'
                 GROUP BY material_id
            ),
            requirement AS (
                SELECT edge.child_material_id AS material_id,
                       edge.quantity AS bom_quantity,
                       edge.loss_rate,
                       CEIL(l_plan_qty * edge.quantity / (1 - edge.loss_rate) * 100) / 100
                           AS required_qty
                  FROM bom edge
                 WHERE edge.version_id = l_version_id
                   AND edge.parent_material_id = l_material_id
            )
            SELECT requirement.material_id,
                   material.material_name,
                   material.unit,
                   requirement.bom_quantity,
                   requirement.loss_rate,
                   requirement.required_qty,
                   NVL(active_lock.locked_qty, 0) AS locked_qty,
                   requirement.required_qty - NVL(active_lock.locked_qty, 0) AS pending_lock_qty,
                   NVL(stock.available_qty, 0) AS available_qty,
                   GREATEST(
                       requirement.required_qty
                       - NVL(active_lock.locked_qty, 0)
                       - NVL(stock.available_qty, 0),
                       0) AS shortage_qty
              FROM requirement
              JOIN material
                ON material.material_id = requirement.material_id
              LEFT JOIN active_lock
                ON active_lock.material_id = requirement.material_id
              LEFT JOIN material_stock stock
                ON stock.material_id = requirement.material_id
             ORDER BY requirement.material_id;
    END open_lock_preview;

    PROCEDURE approve_order(
        p_order_id    IN NUMBER,
        p_approved    IN NUMBER,
        p_operator_id IN NUMBER) IS
        l_material_id    production_order.material_id%TYPE;
        l_version_id     production_order.version_id%TYPE;
        l_plan_qty       production_order.plan_qty%TYPE;
        l_finished_qty   production_order.finished_qty%TYPE;
        l_status         production_order.status%TYPE;
        l_available_qty  material_stock.available_qty%TYPE;
        l_locked_qty     NUMBER;
        l_pending_qty    NUMBER;
        l_lock_id        stock_lock.lock_id%TYPE;
        l_shortage_text  VARCHAR2(4000);
    BEGIN
        IF p_approved IS NULL OR p_approved NOT IN (0, 1) THEN
            pkg_app_error.fail(pkg_app_error.c_invalid_argument, '审核结果必须为 0 或 1');
        END IF;

        load_order(
            p_order_id,
            TRUE,
            l_material_id,
            l_version_id,
            l_plan_qty,
            l_finished_qty,
            l_status);
        IF TRIM(l_status) <> '待审核' THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '仅待审核订单可审核');
        END IF;

        IF p_approved = 0 THEN
            UPDATE production_order
               SET status = '已取消'
             WHERE order_id = p_order_id
               AND status = '待审核';
            RETURN;
        END IF;

        assert_direct_bom(l_material_id, l_version_id);
        assert_existing_locks_valid(p_order_id, l_material_id, l_version_id, l_plan_qty);

        -- 第一遍只锁行，不做判定；若任何一项不足，不执行领域 DML。
        FOR requirement IN (
            SELECT edge.child_material_id AS material_id
              FROM bom edge
             WHERE edge.version_id = l_version_id
               AND edge.parent_material_id = l_material_id
             ORDER BY edge.child_material_id
        ) LOOP
            BEGIN
                -- 只为拿行锁，l_available_qty 本身不参与判定；
                -- 没有库存台账行的物料没什么可锁，忽略即可。
                SELECT available_qty
                  INTO l_available_qty
                  FROM material_stock
                 WHERE material_id = requirement.material_id
                 FOR UPDATE;
            EXCEPTION
                WHEN NO_DATA_FOUND THEN
                    NULL;
            END;
        END LOOP;

        -- 行锁到手后一次性汇总全部缺口，保持与旧实现一致的完整缺料清单。
        SELECT LISTAGG(shortage.material_name || '缺少'
                       || RTRIM(TO_CHAR(shortage.shortage_qty, 'FM99999999990.99'), '.')
                       || shortage.unit, '；'
                       ON OVERFLOW TRUNCATE '...' WITH COUNT)
                   WITHIN GROUP (ORDER BY shortage.material_id)
          INTO l_shortage_text
          FROM (
              SELECT edge.child_material_id AS material_id,
                     material.material_name,
                     material.unit,
                     CEIL(l_plan_qty * edge.quantity / (1 - edge.loss_rate) * 100) / 100
                     - NVL((SELECT SUM(active_lock.lock_qty)
                              FROM stock_lock active_lock
                             WHERE active_lock.order_id = p_order_id
                               AND active_lock.material_id = edge.child_material_id
                               AND active_lock.status = '已锁定'), 0)
                     - NVL(stock.available_qty, 0) AS shortage_qty
                FROM bom edge
                JOIN material
                  ON material.material_id = edge.child_material_id
                LEFT JOIN material_stock stock
                  ON stock.material_id = edge.child_material_id
               WHERE edge.version_id = l_version_id
                 AND edge.parent_material_id = l_material_id
          ) shortage
         WHERE shortage.shortage_qty > 0;

        IF l_shortage_text IS NOT NULL THEN
            pkg_app_error.fail(
                pkg_app_error.c_stock_conflict,
                '库存不足：' || l_shortage_text);
        END IF;

        -- 第二遍执行库存和锁定写入。
        FOR requirement IN (
            SELECT edge.child_material_id AS material_id,
                   CEIL(l_plan_qty * edge.quantity / (1 - edge.loss_rate) * 100) / 100
                       AS required_qty
              FROM bom edge
             WHERE edge.version_id = l_version_id
               AND edge.parent_material_id = l_material_id
             ORDER BY edge.child_material_id
        ) LOOP
            SELECT NVL(SUM(lock_qty), 0), MIN(lock_id)
              INTO l_locked_qty, l_lock_id
              FROM stock_lock
             WHERE order_id = p_order_id
               AND material_id = requirement.material_id
               AND status = '已锁定';
            l_pending_qty := requirement.required_qty - l_locked_qty;

            IF l_pending_qty > 0 THEN
                UPDATE material_stock
                   SET available_qty = available_qty - l_pending_qty,
                       locked_qty = locked_qty + l_pending_qty,
                       last_out_date = SYS_EXTRACT_UTC(SYSTIMESTAMP)
                 WHERE material_id = requirement.material_id
                   AND available_qty >= l_pending_qty;
                IF SQL%ROWCOUNT <> 1 THEN
                    pkg_app_error.fail(
                        pkg_app_error.c_stock_conflict,
                        '物料 ' || requirement.material_id || ' 库存不足，未执行审核');
                END IF;

                IF l_lock_id IS NULL THEN
                    INSERT INTO stock_lock (
                        order_id,
                        material_id,
                        lock_qty,
                        status,
                        lock_time,
                        operator_id)
                    VALUES (
                        p_order_id,
                        requirement.material_id,
                        l_pending_qty,
                        '已锁定',
                        SYS_EXTRACT_UTC(SYSTIMESTAMP),
                        p_operator_id);
                ELSE
                    UPDATE stock_lock
                       SET lock_qty = lock_qty + l_pending_qty,
                           operator_id = p_operator_id
                     WHERE lock_id = l_lock_id;
                END IF;
            END IF;
        END LOOP;

        UPDATE production_order
           SET status = '待排产'
         WHERE order_id = p_order_id
           AND status = '待审核';
        IF SQL%ROWCOUNT <> 1 THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '订单状态已变更，请刷新后重试');
        END IF;
    END approve_order;

    PROCEDURE start_order(p_order_id IN NUMBER) IS
        l_material_id  production_order.material_id%TYPE;
        l_version_id   production_order.version_id%TYPE;
        l_plan_qty     production_order.plan_qty%TYPE;
        l_finished_qty production_order.finished_qty%TYPE;
        l_status       production_order.status%TYPE;
        l_incomplete   NUMBER;
    BEGIN
        load_order(
            p_order_id,
            TRUE,
            l_material_id,
            l_version_id,
            l_plan_qty,
            l_finished_qty,
            l_status);
        IF TRIM(l_status) <> '待排产' THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '仅待排产订单可开工');
        END IF;

        assert_direct_bom(l_material_id, l_version_id);
        assert_existing_locks_valid(p_order_id, l_material_id, l_version_id, l_plan_qty);

        SELECT COUNT(*)
          INTO l_incomplete
          FROM bom edge
         WHERE edge.version_id = l_version_id
           AND edge.parent_material_id = l_material_id
           AND NVL((
               SELECT SUM(active_lock.lock_qty)
                 FROM stock_lock active_lock
                WHERE active_lock.order_id = p_order_id
                  AND active_lock.material_id = edge.child_material_id
                  AND active_lock.status = '已锁定'), 0)
               < CEIL(l_plan_qty * edge.quantity / (1 - edge.loss_rate) * 100) / 100;

        IF l_incomplete > 0 THEN
            pkg_app_error.fail(pkg_app_error.c_stock_conflict, '订单物料锁定不完整，无法开工');
        END IF;

        UPDATE production_order
           SET status = '生产中',
               actual_start = TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
         WHERE order_id = p_order_id
           AND status = '待排产';
        IF SQL%ROWCOUNT <> 1 THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '订单状态已变更，请刷新后重试');
        END IF;
    END start_order;

    PROCEDURE cancel_order(p_order_id IN NUMBER) IS
        l_material_id  production_order.material_id%TYPE;
        l_version_id   production_order.version_id%TYPE;
        l_plan_qty     production_order.plan_qty%TYPE;
        l_finished_qty production_order.finished_qty%TYPE;
        l_status       production_order.status%TYPE;
        l_inbound_count NUMBER;
    BEGIN
        load_order(
            p_order_id,
            TRUE,
            l_material_id,
            l_version_id,
            l_plan_qty,
            l_finished_qty,
            l_status);

        IF TRIM(l_status) IN ('已完工', '已取消') THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '已完工或已取消订单不可取消');
        END IF;

        SELECT COUNT(*)
          INTO l_inbound_count
          FROM finish_inbound
         WHERE order_id = p_order_id;
        IF l_inbound_count > 0 THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '已发生完工报工的生产订单不可取消');
        END IF;

        FOR active_lock IN (
            SELECT material_id, SUM(lock_qty) AS lock_qty
              FROM stock_lock
             WHERE order_id = p_order_id
               AND status = '已锁定'
             GROUP BY material_id
             ORDER BY material_id
        ) LOOP
            UPDATE material_stock
               SET available_qty = available_qty + active_lock.lock_qty,
                   locked_qty = locked_qty - active_lock.lock_qty
             WHERE material_id = active_lock.material_id
               AND locked_qty >= active_lock.lock_qty;
            IF SQL%ROWCOUNT <> 1 THEN
                pkg_app_error.fail(
                    pkg_app_error.c_stock_conflict,
                    '物料 ' || active_lock.material_id || ' 的锁定库存数据不一致');
            END IF;
        END LOOP;

        UPDATE stock_lock
           SET status = '已取消',
               release_time = SYS_EXTRACT_UTC(SYSTIMESTAMP)
         WHERE order_id = p_order_id
           AND status = '已锁定';

        UPDATE production_order
           SET status = '已取消'
         WHERE order_id = p_order_id
           AND status NOT IN ('已完工', '已取消');
        IF SQL%ROWCOUNT <> 1 THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '订单状态已变更，请刷新后重试');
        END IF;
    END cancel_order;

    PROCEDURE report_completion(
        p_order_id        IN NUMBER,
        p_finish_qty      IN NUMBER,
        p_qualified_qty   IN NUMBER,
        p_batch_no        IN VARCHAR2,
        p_operator_id     IN NUMBER,
        p_inbound_id      OUT NUMBER,
        p_order_completed OUT NUMBER) IS
        l_material_id      production_order.material_id%TYPE;
        l_version_id       production_order.version_id%TYPE;
        l_plan_qty         production_order.plan_qty%TYPE;
        l_finished_qty     production_order.finished_qty%TYPE;
        l_status           production_order.status%TYPE;
        l_new_finished_qty production_order.finished_qty%TYPE;
        l_batch_no         VARCHAR2(4000) := TRIM(p_batch_no);
    BEGIN
        IF p_finish_qty IS NULL OR p_finish_qty <= 0 THEN
            pkg_app_error.fail(pkg_app_error.c_invalid_argument, '本批完工数量必须大于 0');
        END IF;
        IF p_qualified_qty IS NULL OR p_qualified_qty < 0
           OR p_qualified_qty > p_finish_qty THEN
            pkg_app_error.fail(pkg_app_error.c_invalid_argument, '本批合格数量必须介于 0 和完工数量之间');
        END IF;
        IF l_batch_no IS NULL OR LENGTH(l_batch_no) NOT BETWEEN 1 AND 30 THEN
            pkg_app_error.fail(pkg_app_error.c_invalid_argument, '批次号长度必须为 1 到 30 个字符');
        END IF;

        load_order(
            p_order_id,
            TRUE,
            l_material_id,
            l_version_id,
            l_plan_qty,
            l_finished_qty,
            l_status);
        IF TRIM(l_status) <> '生产中' THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '仅生产中订单可进行完工报工');
        END IF;

        BEGIN
            INSERT INTO finish_inbound (
                order_id,
                material_id,
                version_id,
                finish_qty,
                qualified_qty,
                batch_no,
                inbound_time,
                operator_id)
            VALUES (
                p_order_id,
                l_material_id,
                l_version_id,
                p_finish_qty,
                p_qualified_qty,
                l_batch_no,
                SYS_EXTRACT_UTC(SYSTIMESTAMP),
                p_operator_id)
            RETURNING inbound_id INTO p_inbound_id;
        EXCEPTION
            WHEN DUP_VAL_ON_INDEX THEN
                pkg_app_error.fail(pkg_app_error.c_state_conflict, '批次号已存在');
        END;

        pkg_trace_domain.allocate_to_finish_batch(p_order_id, p_inbound_id);

        MERGE INTO material_stock target
        USING (SELECT l_material_id AS material_id FROM dual) source
           ON (target.material_id = source.material_id)
        WHEN MATCHED THEN UPDATE SET
            target.available_qty = target.available_qty + p_qualified_qty,
            target.last_in_date = SYS_EXTRACT_UTC(SYSTIMESTAMP)
        WHEN NOT MATCHED THEN INSERT (
            material_id,
            available_qty,
            locked_qty,
            last_in_date)
        VALUES (
            l_material_id,
            p_qualified_qty,
            0,
            SYS_EXTRACT_UTC(SYSTIMESTAMP));

        l_new_finished_qty := l_finished_qty + p_qualified_qty;
        p_order_completed := CASE WHEN l_new_finished_qty >= l_plan_qty THEN 1 ELSE 0 END;

        UPDATE production_order
           SET finished_qty = l_new_finished_qty,
               status = CASE WHEN p_order_completed = 1 THEN '已完工' ELSE '生产中' END,
               actual_end = CASE
                   WHEN p_order_completed = 1
                   THEN TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
                   ELSE actual_end
               END
         WHERE order_id = p_order_id
           AND status = '生产中';
        IF SQL%ROWCOUNT <> 1 THEN
            pkg_app_error.fail(pkg_app_error.c_state_conflict, '生产订单状态已变化，请刷新后重试');
        END IF;

        IF p_order_completed = 1 THEN
            FOR active_lock IN (
                SELECT material_id, SUM(lock_qty) AS lock_qty
                  FROM stock_lock
                 WHERE order_id = p_order_id
                   AND status = '已锁定'
                 GROUP BY material_id
                 ORDER BY material_id
            ) LOOP
                UPDATE material_stock
                   SET locked_qty = locked_qty - active_lock.lock_qty
                 WHERE material_id = active_lock.material_id
                   AND locked_qty >= active_lock.lock_qty;
                IF SQL%ROWCOUNT <> 1 THEN
                    pkg_app_error.fail(
                        pkg_app_error.c_stock_conflict,
                        '物料 ' || active_lock.material_id || ' 的锁定库存数据不一致');
                END IF;
            END LOOP;

            UPDATE stock_lock
               SET status = '已消耗',
                   release_time = SYS_EXTRACT_UTC(SYSTIMESTAMP)
             WHERE order_id = p_order_id
               AND status = '已锁定';
        END IF;
    END report_completion;
END pkg_production_domain;
/



CREATE OR REPLACE PACKAGE BODY pkg_bom_domain AS
    PROCEDURE assert_positive(
        p_value IN NUMBER,
        p_name  IN VARCHAR2) IS
    BEGIN
        IF p_value IS NULL OR p_value <= 0 THEN
            pkg_app_error.fail(pkg_app_error.c_invalid_argument, p_name || '必须大于 0');
        END IF;
    END assert_positive;

    PROCEDURE assert_version_owner(
        p_material_id IN NUMBER,
        p_version_id  IN NUMBER) IS
        l_owner_id       bom_version.material_id%TYPE;
        l_material_count NUMBER;
    BEGIN
        assert_positive(p_material_id, '物料编号');
        assert_positive(p_version_id, 'BOM 版本编号');

        -- 先判物料再判版本，保证「物料不存在」返回明确的 404，
        -- 而不是被后面的版本归属检查掩盖成 400。
        SELECT COUNT(*)
          INTO l_material_count
          FROM material
         WHERE material_id = p_material_id;
        IF l_material_count = 0 THEN
            pkg_app_error.fail(pkg_app_error.c_not_found, '物料不存在');
        END IF;

        BEGIN
            SELECT material_id
              INTO l_owner_id
              FROM bom_version
             WHERE version_id = p_version_id;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                pkg_app_error.fail(pkg_app_error.c_not_found, 'BOM 版本不存在');
        END;

        IF l_owner_id <> p_material_id THEN
            pkg_app_error.fail(pkg_app_error.c_invalid_argument, 'BOM 版本不属于指定物料');
        END IF;
    END assert_version_owner;

    PROCEDURE assert_version_acyclic(
        p_material_id IN NUMBER,
        p_version_id  IN NUMBER) IS
        l_has_cycle    NUMBER;
        l_too_deep     NUMBER;
        l_cycle_path   VARCHAR2(4000);
    BEGIN
        assert_version_owner(p_material_id, p_version_id);

        -- 只从目标根物料出发展开：根物料用候选版本 p_version_id 的边，
        -- 更深层沿用各自的当前有效版本。不再以「图中每一条边」为起点，
        -- 避免整图路径枚举，也避免图中其它位置的环误伤本次校验。
        WITH graph (parent_material_id, child_material_id) AS (
            SELECT b.parent_material_id, b.child_material_id
              FROM bom b
             WHERE b.version_id = p_version_id
               AND b.parent_material_id = p_material_id
            UNION ALL
            SELECT edge.parent_material_id, edge.child_material_id
              FROM v_effective_bom_edge edge
             WHERE edge.parent_material_id <> p_material_id
        ),
        walk (current_material_id, depth, material_path) AS (
            SELECT CAST(p_material_id AS NUMBER),
                   0,
                   CAST(TO_CHAR(p_material_id) AS VARCHAR2(4000))
              FROM dual
            UNION ALL
            SELECT graph.child_material_id,
                   walk.depth + 1,
                   walk.material_path || '/' || TO_CHAR(graph.child_material_id)
              FROM walk
              JOIN graph
                ON graph.parent_material_id = walk.current_material_id
             WHERE walk.depth <= c_max_depth
        )
        SEARCH DEPTH FIRST BY current_material_id SET traversal_order
        CYCLE current_material_id SET cycle_flag TO 'Y' DEFAULT 'N'
        SELECT NVL(MAX(CASE WHEN cycle_flag = 'Y' THEN 1 ELSE 0 END), 0),
               NVL(MAX(CASE WHEN depth > c_max_depth THEN 1 ELSE 0 END), 0),
               MIN(CASE WHEN cycle_flag = 'Y' THEN material_path END)
          INTO l_has_cycle, l_too_deep, l_cycle_path
          FROM walk;

        IF l_has_cycle = 1 THEN
            pkg_app_error.fail(
                pkg_app_error.c_bom_cycle,
                'BOM 存在循环依赖：' || NVL(l_cycle_path, TO_CHAR(p_material_id)));
        END IF;

        IF l_too_deep = 1 THEN
            pkg_app_error.fail(pkg_app_error.c_bom_cycle, 'BOM 展开深度超过 50 层');
        END IF;
    END assert_version_acyclic;

    PROCEDURE check_edge_cycle(
        p_parent_material_id IN NUMBER,
        p_child_material_id  IN NUMBER,
        p_version_id         IN NUMBER,
        p_has_cycle          OUT NUMBER,
        p_cycle_path         OUT VARCHAR2) IS
    BEGIN
        p_has_cycle := 0;
        p_cycle_path := NULL;

        IF p_parent_material_id IS NULL OR p_parent_material_id <= 0
           OR p_child_material_id IS NULL OR p_child_material_id <= 0
           OR p_version_id IS NULL OR p_version_id <= 0 THEN
            RETURN;
        END IF;

        IF p_parent_material_id = p_child_material_id THEN
            p_has_cycle := 1;
            p_cycle_path := TO_CHAR(p_parent_material_id) || ',' ||
                            TO_CHAR(p_child_material_id);
            RETURN;
        END IF;

        BEGIN
            WITH walk (current_material_id, depth, material_path) AS (
                SELECT p_child_material_id,
                       0,
                       CAST(TO_CHAR(p_child_material_id) AS VARCHAR2(4000))
                  FROM dual
                UNION ALL
                SELECT edge.child_material_id,
                       walk.depth + 1,
                       walk.material_path || ',' || TO_CHAR(edge.child_material_id)
                  FROM walk
                  JOIN v_effective_bom_edge edge
                    ON edge.parent_material_id = walk.current_material_id
                 WHERE walk.depth < c_max_depth
            )
            SEARCH DEPTH FIRST BY current_material_id SET traversal_order
            CYCLE current_material_id SET cycle_flag TO 'Y' DEFAULT 'N'
            SELECT material_path
              INTO p_cycle_path
              FROM walk
             WHERE current_material_id = p_parent_material_id
               AND cycle_flag = 'N'
             ORDER BY traversal_order
             FETCH FIRST 1 ROW ONLY;

            p_has_cycle := 1;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                NULL;
        END;
    END check_edge_cycle;

    PROCEDURE open_tree(
        p_material_id IN NUMBER,
        p_version_id  IN NUMBER,
        p_root_qty    IN NUMBER DEFAULT 1,
        p_rows        OUT SYS_REFCURSOR) IS
    BEGIN
        assert_positive(p_root_qty, '根物料数量');
        assert_version_acyclic(p_material_id, p_version_id);

        OPEN p_rows FOR
            WITH tree (
                material_id,
                material_name,
                model,
                material_type,
                unit,
                parent_material_id,
                quantity,
                accumulated_quantity,
                depth,
                material_path,
                next_version_id
            ) AS (
                SELECT material.material_id,
                       material.material_name,
                       material.model,
                       material.material_type,
                       material.unit,
                       CAST(NULL AS NUMBER),
                       CAST(1 AS NUMBER),
                       p_root_qty,
                       0,
                       CAST(TO_CHAR(material.material_id) AS VARCHAR2(4000)),
                       p_version_id
                  FROM material
                 WHERE material.material_id = p_material_id
                UNION ALL
                SELECT child.material_id,
                       child.material_name,
                       child.model,
                       child.material_type,
                       child.unit,
                       tree.material_id,
                       edge.quantity,
                       tree.accumulated_quantity * edge.quantity,
                       tree.depth + 1,
                       tree.material_path || '/' || TO_CHAR(child.material_id),
                       effective_version.version_id
                  FROM tree
                  JOIN bom edge
                    ON edge.version_id = tree.next_version_id
                   AND edge.parent_material_id = tree.material_id
                  JOIN material child
                    ON child.material_id = edge.child_material_id
                  LEFT JOIN bom_version effective_version
                    ON effective_version.version_id = child.current_version_id
                   AND effective_version.material_id = child.material_id
                   AND effective_version.effective_date <=
                       TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
                   AND (effective_version.expire_date IS NULL
                        OR effective_version.expire_date >=
                           TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)))
                 WHERE tree.depth < c_max_depth
            )
            SEARCH DEPTH FIRST BY material_id SET traversal_order
            CYCLE material_id SET cycle_flag TO 'Y' DEFAULT 'N'
            SELECT material_id,
                   material_name,
                   model,
                   material_type,
                   unit,
                   quantity,
                   accumulated_quantity,
                   depth,
                   parent_material_id,
                   material_path,
                   CASE
                       WHEN next_version_id IS NULL OR NOT EXISTS (
                           SELECT 1
                             FROM bom child_edge
                            WHERE child_edge.version_id = tree.next_version_id
                              AND child_edge.parent_material_id = tree.material_id)
                       THEN 1 ELSE 0
                   END AS is_leaf
              FROM tree
             WHERE cycle_flag = 'N'
             ORDER BY traversal_order;
    END open_tree;

    PROCEDURE open_demand(
        p_material_id IN NUMBER,
        p_version_id  IN NUMBER,
        p_quantity    IN NUMBER,
        p_rows        OUT SYS_REFCURSOR) IS
    BEGIN
        assert_positive(p_quantity, '数量');
        assert_version_acyclic(p_material_id, p_version_id);

        OPEN p_rows FOR
            WITH demand (
                material_id,
                material_name,
                material_type,
                parent_material_id,
                loss_rate,
                net_quantity,
                gross_quantity,
                depth,
                material_path,
                next_version_id
            ) AS (
                SELECT child.material_id,
                       child.material_name,
                       child.material_type,
                       edge.parent_material_id,
                       edge.loss_rate,
                       p_quantity * edge.quantity,
                       CEIL(p_quantity * edge.quantity / (1 - edge.loss_rate)),
                       1,
                       CAST(TO_CHAR(p_material_id) || '/' || TO_CHAR(child.material_id)
                            AS VARCHAR2(4000)),
                       effective_version.version_id
                  FROM bom edge
                  JOIN material child
                    ON child.material_id = edge.child_material_id
                  LEFT JOIN bom_version effective_version
                    ON effective_version.version_id = child.current_version_id
                   AND effective_version.material_id = child.material_id
                   AND effective_version.effective_date <=
                       TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
                   AND (effective_version.expire_date IS NULL
                        OR effective_version.expire_date >=
                           TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)))
                 WHERE edge.version_id = p_version_id
                   AND edge.parent_material_id = p_material_id
                UNION ALL
                SELECT child.material_id,
                       child.material_name,
                       child.material_type,
                       demand.material_id,
                       edge.loss_rate,
                       demand.gross_quantity * edge.quantity,
                       CEIL(demand.gross_quantity * edge.quantity / (1 - edge.loss_rate)),
                       demand.depth + 1,
                       demand.material_path || '/' || TO_CHAR(child.material_id),
                       effective_version.version_id
                  FROM demand
                  JOIN bom edge
                    ON edge.version_id = demand.next_version_id
                   AND edge.parent_material_id = demand.material_id
                  JOIN material child
                    ON child.material_id = edge.child_material_id
                  LEFT JOIN bom_version effective_version
                    ON effective_version.version_id = child.current_version_id
                   AND effective_version.material_id = child.material_id
                   AND effective_version.effective_date <=
                       TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
                   AND (effective_version.expire_date IS NULL
                        OR effective_version.expire_date >=
                           TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)))
                 WHERE demand.depth < c_max_depth
            )
            SEARCH DEPTH FIRST BY material_id SET traversal_order
            CYCLE material_id SET cycle_flag TO 'Y' DEFAULT 'N'
            SELECT material_id,
                   MIN(material_name) KEEP (DENSE_RANK FIRST ORDER BY depth, material_path)
                       AS material_name,
                   MIN(material_type) KEEP (DENSE_RANK FIRST ORDER BY depth, material_path)
                       AS material_type,
                   CASE WHEN COUNT(DISTINCT parent_material_id) = 1
                        THEN MIN(parent_material_id) END AS parent_material_id,
                   MIN(depth) AS depth,
                   SUM(net_quantity) AS net_quantity,
                   SUM(gross_quantity) AS gross_quantity,
                   loss_rate,
                   LISTAGG(material_path, ' | '
                           ON OVERFLOW TRUNCATE '...' WITH COUNT)
                       WITHIN GROUP (ORDER BY material_path) AS material_path,
                   MIN(CASE
                       WHEN next_version_id IS NULL OR NOT EXISTS (
                           SELECT 1
                             FROM bom child_edge
                            WHERE child_edge.version_id = demand.next_version_id
                              AND child_edge.parent_material_id = demand.material_id)
                       THEN 1 ELSE 0
                   END) AS is_leaf
              FROM demand
             WHERE cycle_flag = 'N'
             GROUP BY material_id, loss_rate
             ORDER BY depth, material_id, loss_rate;
    END open_demand;

    PROCEDURE open_reverse_usage(
        p_material_id     IN NUMBER,
        p_version_id      IN NUMBER,
        p_include_history IN NUMBER,
        p_rows            OUT SYS_REFCURSOR) IS
    BEGIN
        assert_version_owner(p_material_id, p_version_id);

        OPEN p_rows FOR
            WITH reverse_edge (
                bom_id,
                version_id,
                version_no,
                parent_material_id,
                child_material_id,
                quantity
            ) AS (
                SELECT b.bom_id,
                       b.version_id,
                       version.version_no,
                       b.parent_material_id,
                       b.child_material_id,
                       b.quantity
                  FROM bom b
                  JOIN bom_version version
                    ON version.version_id = b.version_id
                  JOIN material parent
                    ON parent.material_id = b.parent_material_id
                 WHERE p_include_history = 1
                    OR parent.current_version_id = b.version_id
            ),
            -- 起点物料本身也放进 walk（depth = 0），这样 CYCLE 能覆盖起点，
            -- 避免 A 用于 B、B 又用于 A 时把起点自己当成上层成品返回；
            -- 输出时用 depth > 0 过滤掉这一行。
            usage (
                product_material_id,
                version_id,
                version_no,
                quantity,
                accumulated_quantity,
                depth,
                material_path
            ) AS (
                SELECT CAST(p_material_id AS NUMBER),
                       CAST(NULL AS NUMBER),
                       CAST(NULL AS VARCHAR2(20)),
                       CAST(NULL AS NUMBER),
                       CAST(1 AS NUMBER),
                       0,
                       CAST(TO_CHAR(p_material_id) AS VARCHAR2(4000))
                  FROM dual
                UNION ALL
                SELECT edge.parent_material_id,
                       edge.version_id,
                       edge.version_no,
                       edge.quantity,
                       usage.accumulated_quantity * edge.quantity,
                       usage.depth + 1,
                       usage.material_path || '/' || TO_CHAR(edge.parent_material_id)
                  FROM usage
                  JOIN reverse_edge edge
                    ON edge.child_material_id = usage.product_material_id
                 WHERE usage.depth < c_max_depth
            )
            SEARCH DEPTH FIRST BY product_material_id SET traversal_order
            CYCLE product_material_id SET cycle_flag TO 'Y' DEFAULT 'N'
            SELECT usage.product_material_id,
                   material.material_name AS product_material_name,
                   usage.version_id,
                   usage.version_no,
                   CASE WHEN material.current_version_id = usage.version_id
                        THEN 'effective' ELSE 'history' END AS version_status,
                   usage.quantity,
                   usage.accumulated_quantity,
                   usage.depth,
                   usage.material_path
              FROM usage
              JOIN material
                ON material.material_id = usage.product_material_id
             WHERE cycle_flag = 'N'
               AND usage.depth > 0
             ORDER BY traversal_order;
    END open_reverse_usage;

    FUNCTION is_order_leaf_material(
        p_order_id    IN NUMBER,
        p_material_id IN NUMBER)
    RETURN NUMBER IS
        l_root_material_id production_order.material_id%TYPE;
        l_version_id      production_order.version_id%TYPE;
        l_match_count     NUMBER;
    BEGIN
        BEGIN
            SELECT material_id, version_id
              INTO l_root_material_id, l_version_id
              FROM production_order
             WHERE order_id = p_order_id;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                RETURN 0;
        END;

        WITH demand (material_id, depth, next_version_id) AS (
            SELECT child.material_id,
                   1,
                   effective_version.version_id
              FROM bom edge
              JOIN material child
                ON child.material_id = edge.child_material_id
              LEFT JOIN bom_version effective_version
                ON effective_version.version_id = child.current_version_id
               AND effective_version.material_id = child.material_id
               AND effective_version.effective_date <=
                   TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
               AND (effective_version.expire_date IS NULL
                    OR effective_version.expire_date >=
                       TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)))
             WHERE edge.version_id = l_version_id
               AND edge.parent_material_id = l_root_material_id
            UNION ALL
            SELECT child.material_id,
                   demand.depth + 1,
                   effective_version.version_id
              FROM demand
              JOIN bom edge
                ON edge.version_id = demand.next_version_id
               AND edge.parent_material_id = demand.material_id
              JOIN material child
                ON child.material_id = edge.child_material_id
              LEFT JOIN bom_version effective_version
                ON effective_version.version_id = child.current_version_id
               AND effective_version.material_id = child.material_id
               AND effective_version.effective_date <=
                   TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
               AND (effective_version.expire_date IS NULL
                    OR effective_version.expire_date >=
                       TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)))
             WHERE demand.depth < c_max_depth
        )
        SEARCH DEPTH FIRST BY material_id SET traversal_order
        CYCLE material_id SET cycle_flag TO 'Y' DEFAULT 'N'
        SELECT COUNT(*)
          INTO l_match_count
          FROM demand
         WHERE cycle_flag = 'N'
           AND material_id = p_material_id
           AND (next_version_id IS NULL OR NOT EXISTS (
               SELECT 1
                 FROM bom child_edge
                WHERE child_edge.version_id = demand.next_version_id
                  AND child_edge.parent_material_id = demand.material_id));

        RETURN CASE WHEN l_match_count > 0 THEN 1 ELSE 0 END;
    END is_order_leaf_material;
END pkg_bom_domain;
/

-- ---------- 6. BOM 写入守卫 ----------

CREATE OR REPLACE TRIGGER ct_bom_cycle_guard
FOR INSERT OR UPDATE OR DELETE ON bom
COMPOUND TRIGGER
    TYPE version_set_type IS TABLE OF BOOLEAN INDEX BY PLS_INTEGER;
    g_versions version_set_type;

    PROCEDURE remember_version(p_version_id NUMBER) IS
    BEGIN
        IF p_version_id IS NOT NULL THEN
            g_versions(TRUNC(p_version_id)) := TRUE;
        END IF;
    END remember_version;

    AFTER EACH ROW IS
    BEGIN
        IF INSERTING OR UPDATING THEN
            remember_version(:NEW.version_id);
        END IF;
        IF DELETING OR UPDATING THEN
            remember_version(:OLD.version_id);
        END IF;
    END AFTER EACH ROW;

    AFTER STATEMENT IS
        l_version_id  PLS_INTEGER;
        l_material_id bom_version.material_id%TYPE;
    BEGIN
        l_version_id := g_versions.FIRST;
        WHILE l_version_id IS NOT NULL LOOP
            BEGIN
                SELECT material_id
                  INTO l_material_id
                  FROM bom_version
                 WHERE version_id = l_version_id;
                pkg_bom_domain.assert_version_acyclic(l_material_id, l_version_id);
            EXCEPTION
                WHEN NO_DATA_FOUND THEN
                    NULL;
            END;
            l_version_id := g_versions.NEXT(l_version_id);
        END LOOP;
    END AFTER STATEMENT;
END ct_bom_cycle_guard;
/

CREATE OR REPLACE TRIGGER ct_material_version_cycle_guard
FOR UPDATE OF current_version_id ON material
COMPOUND TRIGGER
    TYPE material_set_type IS TABLE OF BOOLEAN INDEX BY PLS_INTEGER;
    g_materials material_set_type;

    AFTER EACH ROW IS
    BEGIN
        IF NVL(:OLD.current_version_id, -1) <> NVL(:NEW.current_version_id, -1)
           AND :NEW.current_version_id IS NOT NULL THEN
            g_materials(TRUNC(:NEW.material_id)) := TRUE;
        END IF;
    END AFTER EACH ROW;

    AFTER STATEMENT IS
        l_material_id PLS_INTEGER;
        l_version_id  material.current_version_id%TYPE;
    BEGIN
        l_material_id := g_materials.FIRST;
        WHILE l_material_id IS NOT NULL LOOP
            SELECT current_version_id
              INTO l_version_id
              FROM material
             WHERE material_id = l_material_id;
            IF l_version_id IS NOT NULL THEN
                pkg_bom_domain.assert_version_acyclic(l_material_id, l_version_id);
            END IF;
            l_material_id := g_materials.NEXT(l_material_id);
        END LOOP;
    END AFTER STATEMENT;
END ct_material_version_cycle_guard;
/

-- ---------- 7. 编译检查 ----------

DECLARE
    l_error_count NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO l_error_count
      FROM user_errors
     WHERE name IN (
         'PKG_APP_ERROR',
         'PKG_BOM_DOMAIN',
         'PKG_TRACE_DOMAIN',
         'PKG_PRODUCTION_DOMAIN',
         'CT_BATCH_CONSUMPTION_GUARD',
         'CT_BOM_CYCLE_GUARD',
         'CT_MATERIAL_VERSION_CYCLE_GUARD');

    IF l_error_count > 0 THEN
        FOR compile_error IN (
            SELECT name, type, line, position, text
              FROM user_errors
             WHERE name IN (
                 'PKG_APP_ERROR',
                 'PKG_BOM_DOMAIN',
                 'PKG_TRACE_DOMAIN',
                 'PKG_PRODUCTION_DOMAIN',
                 'CT_BATCH_CONSUMPTION_GUARD',
                 'CT_BOM_CYCLE_GUARD',
                 'CT_MATERIAL_VERSION_CYCLE_GUARD')
             ORDER BY name, sequence
        ) LOOP
            DBMS_OUTPUT.PUT_LINE(
                compile_error.name || ' ' || compile_error.type || ' '
                || compile_error.line || ':' || compile_error.position || ' '
                || compile_error.text);
        END LOOP;
        RAISE_APPLICATION_ERROR(-20997, '领域对象存在编译错误：' || l_error_count);
    END IF;
END;
/

PROMPT Oracle domain logic compiled successfully.
