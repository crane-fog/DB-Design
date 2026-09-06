-- ============================================================================
-- 稳定权限目录与内置角色默认授权
--
-- 前置条件：SYS_PERMISSION 已采用 permission_code 等新字段，七个内置角色已存在。
-- 本文件由 02_seed_data_forklift.sql 和 04_migrate_permission_rbac.sql 调用。
-- 权限关系始终通过 permission_code 解析，不依赖 permission_id 的具体数值。
-- ============================================================================

-- ---------- 1. 系统管理 ----------
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:user:view','系统管理','用户','查看','查看用户列表和用户详情',10,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:user:create','系统管理','用户','创建','创建用户',20,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:user:update','系统管理','用户','修改','修改用户资料和状态',30,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:user:delete','系统管理','用户','删除','删除用户',40,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:user:assign-role','系统管理','用户角色','配置','查看并替换用户的完整角色集合',50,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:role:view','系统管理','角色','查看','查看角色列表和角色详情',60,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:role:create','系统管理','角色','创建','创建角色',70,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:role:update','系统管理','角色','修改','修改角色资料和状态',80,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:role:delete','系统管理','角色','删除','删除角色',90,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:role:assign-permission','系统管理','角色权限','配置','查看并替换角色的完整权限集合',100,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:permission:view','系统管理','权限目录','查看','查看系统维护的权限目录',110,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:audit:login:view','系统管理','登录日志','查看','查看登录日志',120,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:audit:operation:view','系统管理','操作日志','查看','查看操作日志',130,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('system:audit:operation:create','系统管理','操作日志','记录','写入操作日志',140,'valid');

-- ---------- 2. 物料与 BOM ----------
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:item:view','物料与BOM','物料','查看','查看物料列表和详情',150,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:item:create','物料与BOM','物料','创建','创建物料',160,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:item:update','物料与BOM','物料','修改','修改物料',170,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:item:delete','物料与BOM','物料','删除','删除物料',180,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:category:view','物料与BOM','物料分类','查看','查看物料分类',190,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:category:create','物料与BOM','物料分类','创建','创建物料分类',200,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:category:update','物料与BOM','物料分类','修改','修改物料分类',210,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:category:delete','物料与BOM','物料分类','删除','删除物料分类',220,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:bom-version:view','物料与BOM','BOM版本','查看','查看BOM版本',230,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:bom-version:create','物料与BOM','BOM版本','创建','创建BOM版本',240,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:bom-version:update','物料与BOM','BOM版本','修改','修改BOM版本',250,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:bom-version:delete','物料与BOM','BOM版本','删除','删除BOM版本',260,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:bom:view','物料与BOM','BOM明细','查看','查看BOM明细',270,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:bom:create','物料与BOM','BOM明细','创建','创建BOM明细',280,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:bom:update','物料与BOM','BOM明细','修改','修改BOM明细',290,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:bom:delete','物料与BOM','BOM明细','删除','删除BOM明细',300,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:bom:check-cycle','物料与BOM','BOM工具','循环校验','检查BOM循环依赖',310,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:bom:tree:view','物料与BOM','BOM树','查看','查看BOM树',320,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:bom:reverse:view','物料与BOM','BOM反查','查看','反向查询物料用途',330,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:cost:calculate','物料与BOM','产品成本','计算','计算产品成本',340,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('material:loss:calculate','物料与BOM','损耗补偿','计算','计算BOM损耗补偿',350,'valid');

-- ---------- 3. 库存管理 ----------
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:stock:view','库存管理','库存快照','查看','查看物料库存',360,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:shortage:calculate','库存管理','物料缺口','计算','计算生产物料缺口',370,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:alert:view','库存管理','库存预警','查看','查看库存预警',380,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:alert:generate','库存管理','库存预警','生成','生成库存预警',390,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:alert:handle','库存管理','库存预警','处理','处理库存预警',400,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:lock:view','库存管理','库存锁定','查看','查看库存锁定记录',410,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:lock:create','库存管理','库存锁定','锁定','创建库存锁定',420,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:lock:release','库存管理','库存锁定','释放','释放库存锁定',430,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:obsolete:view','库存管理','废弃物料','查看','查看废弃物料检测记录',440,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:obsolete:detect','库存管理','废弃物料','检测','执行废弃物料检测',450,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:obsolete:handle','库存管理','废弃物料','处理','处理废弃物料记录',460,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:completion:view','库存管理','完工入库','查看','查看完工入库记录',470,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('inventory:completion:create','库存管理','完工入库','创建','登记完工入库',480,'valid');

-- ---------- 4. 采购管理 ----------
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:supplier:view','采购管理','供应商','查看','查看供应商目录',490,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:buyer:view','采购管理','采购员目录','查看','查看可选采购员',500,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:buyer:eligible','采购管理','采购员资格','担任','允许用户作为采购订单采购员',510,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:order:view','采购管理','采购订单','查看','查看采购订单',520,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:order:create','采购管理','采购订单','创建','创建采购订单或缺口采购草稿',530,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:order:submit','采购管理','采购订单','提交','提交采购订单',540,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:order:cancel','采购管理','采购订单','取消','取消采购订单',550,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:receipt:view','采购管理','采购收货','查看','查看采购收货记录',560,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:receipt:create','采购管理','采购收货','创建','登记采购收货',570,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:overdue:view','采购管理','逾期提醒','查看','查看采购逾期提醒',580,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:overdue:generate','采购管理','逾期提醒','生成','生成采购逾期提醒',590,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('purchase:overdue:handle','采购管理','逾期提醒','处理','处理采购逾期提醒',600,'valid');

-- ---------- 5. 生产管理 ----------
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:order:view','生产管理','生产订单','查看','查看生产订单',610,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:order:create','生产管理','生产订单','创建','创建生产订单',620,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:order:update','生产管理','生产订单','修改','修改生产订单',630,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:order:approve','生产管理','生产订单','审核','审核生产订单',640,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:order:start','生产管理','生产订单','开工','启动生产订单',650,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:order:finish','生产管理','生产订单','完工','完成生产订单',660,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:order:cancel','生产管理','生产订单','取消','取消生产订单',670,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:line:view','生产管理','生产线','查看','查看生产线',680,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:line:create','生产管理','生产线','创建','创建生产线',690,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:line:update','生产管理','生产线','修改','修改生产线',700,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:line-type:view','生产管理','生产线类型','查看','查看生产线类型',710,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:line-type:update','生产管理','生产线类型','维护','新增或修改生产线类型',720,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:capacity-config:view','生产管理','产能配置','查看','查看产能配置',730,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:capacity-config:update','生产管理','产能配置','维护','新增或修改产能配置',740,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:calendar:view','生产管理','生产日历','查看','查看生产日历',750,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:calendar:update','生产管理','生产日历','维护','新增或修改生产日历',760,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:calendar:delete','生产管理','生产日历','删除','删除生产日历',770,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:capacity:estimate','生产管理','产能评估','计算','评估生产产能',780,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:capacity:detect','生产管理','产能检测','执行','执行产能检测',790,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:capacity:balance','生产管理','产能平衡','保存','保存产能平衡方案',800,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:fault:view','生产管理','生产线故障','查看','查看生产线故障',810,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:fault:report','生产管理','生产线故障','上报','上报生产线故障',820,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:fault:claim','生产管理','生产线故障','认领','认领待维修故障',830,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:fault:update-assigned','生产管理','生产线故障','处理本人任务','处理分配给当前用户的故障',840,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:fault:update-any','生产管理','生产线故障','处理任意任务','分配或处理任意故障',850,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('production:line-status:update','生产管理','生产线状态','修改','修改生产线运行状态',860,'valid');

-- ---------- 6. 外部订单 ----------
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('external-order:view-own','外部订单','外部订单','查看本人','仅查看当前用户提交的外部订单',870,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('external-order:view-all','外部订单','外部订单','查看全部','查看全部外部订单',880,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('external-order:create-own','外部订单','外部订单','本人提交','以当前用户身份提交外部订单',890,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('external-order:create-for-customer','外部订单','外部订单','代客户提交','为指定客户提交外部订单',900,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('external-order:review','外部订单','外部订单','审核','审核外部订单',910,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('external-order:convert','外部订单','外部订单','转生产订单','将已接受外部订单转换为生产订单',920,'valid');

-- ---------- 7. 质量追溯 ----------
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('trace:consumption:view','质量追溯','批次消耗','查看','查看批次消耗记录',930,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('trace:consumption:create','质量追溯','批次消耗','创建','创建批次消耗记录',940,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('trace:consumption:update','质量追溯','批次消耗','修改','修改批次消耗记录',950,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('trace:consumption:delete','质量追溯','批次消耗','删除','删除批次消耗记录',960,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('trace:product:view','质量追溯','成品追溯','查看','按成品批次追溯原材料',970,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('trace:material:view','质量追溯','原材料追溯','查看','按原材料批次追溯成品',980,'valid');
INSERT INTO sys_permission (permission_code,module_name,resource_name,action_name,description,sort_order,status) VALUES ('trace:impact:analyze','质量追溯','质量影响','分析','分析问题批次影响范围',990,'valid');

-- ---------- 8. 内置角色默认授权 ----------
-- 系统管理员在当前版本中显式关联本文件定义的完整权限目录。
INSERT INTO sys_role_permission (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM sys_role r
CROSS JOIN sys_permission p
WHERE r.role_name = '系统管理员';

-- 生产管理员：物料、库存、生产、外部订单和质量追溯全流程。
INSERT INTO sys_role_permission (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM sys_role r
CROSS JOIN sys_permission p
WHERE r.role_name = '生产管理员'
  AND p.permission_code IN (
    'material:item:view','material:item:create','material:item:update','material:item:delete',
    'material:category:view','material:category:create','material:category:update','material:category:delete',
    'material:bom-version:view','material:bom-version:create','material:bom-version:update','material:bom-version:delete',
    'material:bom:view','material:bom:create','material:bom:update','material:bom:delete',
    'material:bom:check-cycle','material:bom:tree:view','material:bom:reverse:view',
    'material:cost:calculate','material:loss:calculate',
    'inventory:stock:view','inventory:shortage:calculate',
    'inventory:alert:view','inventory:alert:generate','inventory:alert:handle',
    'inventory:lock:view','inventory:lock:create','inventory:lock:release',
    'inventory:obsolete:view','inventory:obsolete:detect','inventory:obsolete:handle',
    'inventory:completion:view','inventory:completion:create',
    'production:order:view','production:order:create','production:order:update','production:order:approve',
    'production:order:start','production:order:finish','production:order:cancel',
    'production:line:view','production:line:create','production:line:update',
    'production:line-type:view','production:line-type:update',
    'production:capacity-config:view','production:capacity-config:update',
    'production:calendar:view','production:calendar:update','production:calendar:delete',
    'production:capacity:estimate','production:capacity:detect','production:capacity:balance',
    'production:fault:view','production:fault:report','production:fault:claim',
    'production:fault:update-assigned','production:fault:update-any','production:line-status:update',
    'external-order:view-own','external-order:view-all','external-order:create-own',
    'external-order:create-for-customer','external-order:review','external-order:convert',
    'trace:consumption:view','trace:consumption:create','trace:consumption:update','trace:consumption:delete',
    'trace:product:view','trace:material:view','trace:impact:analyze'
  );

-- 采购员：采购全流程、采购员资格及业务所需物料/BOM和库存只读能力。
INSERT INTO sys_role_permission (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM sys_role r
CROSS JOIN sys_permission p
WHERE r.role_name = '采购员'
  AND p.permission_code IN (
    'material:item:view','material:category:view','material:bom-version:view','material:bom:view',
    'inventory:stock:view','inventory:shortage:calculate',
    'purchase:supplier:view','purchase:buyer:view','purchase:buyer:eligible',
    'purchase:order:view','purchase:order:create','purchase:order:submit','purchase:order:cancel',
    'purchase:receipt:view','purchase:receipt:create',
    'purchase:overdue:view','purchase:overdue:generate','purchase:overdue:handle'
  );

-- 库存管理员：库存全流程、物料/BOM只读及缺口转采购草稿所需权限。
INSERT INTO sys_role_permission (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM sys_role r
CROSS JOIN sys_permission p
WHERE r.role_name = '库存管理员'
  AND p.permission_code IN (
    'material:item:view','material:category:view','material:bom-version:view','material:bom:view',
    'inventory:stock:view','inventory:shortage:calculate',
    'inventory:alert:view','inventory:alert:generate','inventory:alert:handle',
    'inventory:lock:view','inventory:lock:create','inventory:lock:release',
    'inventory:obsolete:view','inventory:obsolete:detect','inventory:obsolete:handle',
    'inventory:completion:view','inventory:completion:create',
    'purchase:supplier:view','purchase:buyer:view','purchase:order:create',
    'production:order:view'
  );

-- 质量管理员：质量追溯全流程及追溯页面依赖的跨模块只读权限。
INSERT INTO sys_role_permission (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM sys_role r
CROSS JOIN sys_permission p
WHERE r.role_name = '质量管理员'
  AND p.permission_code IN (
    'material:item:view','material:category:view','material:bom-version:view','material:bom:view',
    'inventory:stock:view','inventory:completion:view',
    'purchase:supplier:view','purchase:order:view','purchase:receipt:view',
    'production:order:view','production:line:view',
    'trace:consumption:view','trace:consumption:create','trace:consumption:update','trace:consumption:delete',
    'trace:product:view','trace:material:view','trace:impact:analyze'
  );

-- 设备管理员：生产线只读、故障处理和产线状态维护。
INSERT INTO sys_role_permission (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM sys_role r
CROSS JOIN sys_permission p
WHERE r.role_name = '设备管理员'
  AND p.permission_code IN (
    'production:line:view','production:line-type:view',
    'production:fault:view','production:fault:report','production:fault:claim',
    'production:fault:update-assigned','production:fault:update-any',
    'production:line-status:update'
  );

-- 外部客户：只能提交和查看自己的外部订单。
INSERT INTO sys_role_permission (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM sys_role r
CROSS JOIN sys_permission p
WHERE r.role_name = '外部客户'
  AND p.permission_code IN ('external-order:view-own','external-order:create-own');
