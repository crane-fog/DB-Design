-- ============================================================================
-- 为 batch_consumption 添加 (order_id, item_id) 唯一约束
-- 原因：同一生产订单不应对同一采购批次插入多条消耗记录。
-- 如需追加消耗量，应使用 updateBatchConsumption 接口。
-- ============================================================================

-- 先清理测试过程中产生的重复数据（保留 consumption_id 最小的记录）
DELETE FROM batch_consumption
WHERE consumption_id NOT IN (
    SELECT MIN(consumption_id)
    FROM batch_consumption
    GROUP BY order_id, item_id
);

-- 添加唯一约束
ALTER TABLE batch_consumption ADD CONSTRAINT uq_bc_order_item UNIQUE (order_id, item_id);

COMMIT;

-- ============================================================================
-- 为 external_order.status 添加 "已转换" 状态
-- 原因：外部订单审核接受后转为生产订单时，需要标记为已转换以区分"待转换"和"已转换"。
-- ============================================================================

DECLARE
  v_constraint_name user_constraints.constraint_name%TYPE;
  v_cnt             PLS_INTEGER;
BEGIN
  -- 精确匹配 external_order 的 STATUS CHECK 约束：
  -- 1. 优先按约束名模式 CHK_EXTERNAL_ORDER_STATUS% 匹配（新建表时统一命名）
  -- 2. 兜底按 search_condition_vc 匹配 'STATUS IN'（兼容旧自动命名约束）
  SELECT COUNT(*)
    INTO v_cnt
    FROM user_constraints
   WHERE table_name      = 'EXTERNAL_ORDER'
     AND constraint_type = 'C'
     AND (   constraint_name LIKE 'CK_EXTERNAL_ORDER_STATUS%'
          OR search_condition_vc LIKE '%STATUS IN%');

  IF v_cnt = 0 THEN
    NULL; -- 无匹配约束，跳过
  ELSIF v_cnt = 1 THEN
    SELECT constraint_name
      INTO v_constraint_name
      FROM user_constraints
     WHERE table_name      = 'EXTERNAL_ORDER'
       AND constraint_type = 'C'
       AND (   constraint_name LIKE 'CK_EXTERNAL_ORDER_STATUS%'
            OR search_condition_vc LIKE '%STATUS IN%');
    EXECUTE IMMEDIATE 'ALTER TABLE external_order DROP CONSTRAINT ' || v_constraint_name;
  ELSE
    RAISE_APPLICATION_ERROR(
      -20001,
      'Multiple STATUS check constraints found on EXTERNAL_ORDER; review and drop manually.');
  END IF;
END;
/

ALTER TABLE external_order ADD CONSTRAINT ck_external_order_status
  CHECK (status IN ('待审核','已接受','已拒绝','已转换'));

COMMIT;
