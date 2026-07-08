# API 文档编写规范

本文档用于统一项目 API 文档的基础约定，包括接口返回格式、业务状态码、分页结构、业务状态值和公共 Schema 设计规则。

本文档不包含具体业务接口定义。各模块负责人编写具体接口时，应在遵守本文档约定的基础上，补充本模块的请求参数、响应数据、状态流转和 Schema。

## 1. 统一接口返回格式

所有接口统一返回以下三个字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `code` | integer | 业务状态码 |
| `message` | string | 接口执行结果说明 |
| `data` | object / array / null | 实际业务数据；没有数据时返回 `null` |

基础返回结构如下：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": null
}
```

统一要求：

- 普通接口统一返回 `code`、`message`、`data`。
- 分页接口的 `data` 中统一返回 `total`、`page`、`page_size`、`records`。
- 所有组员必须使用相同的字段名称和业务状态码，不得自行修改返回结构。

## 2. 业务状态码约定

项目统一使用以下业务状态码。这里的状态码指响应体 JSON 中的 `code` 字段，不是 HTTP 状态码。

| 状态码 | 含义 |
| --- | --- |
| `200` | 请求成功 |
| `400` | 请求参数错误 |
| `401` | 未登录或登录失效 |
| `403` | 没有权限 |
| `404` | 数据不存在 |
| `409` | 当前业务状态冲突，操作不合法 |
| `500` | 服务器内部错误 |

为方便前后端统一处理，所有接口的 HTTP 状态码一律返回 `200`。接口是否真正成功，由响应体中的 `code` 判断。

例如请求参数错误时，HTTP 状态码仍然是 `200`，响应体中的 `code` 返回 `400`：

```json
{
  "code": 400,
  "message": "采购数量必须大于0",
  "data": null
}
```

### 2.1 成功返回示例

```json
{
  "code": 200,
  "message": "查询成功",
  "data": {
    "material_id": 1,
    "material_name": "钢板"
  }
}
```

### 2.2 请求参数错误示例

```json
{
  "code": 400,
  "message": "采购数量必须大于0",
  "data": null
}
```

### 2.3 未登录示例

```json
{
  "code": 401,
  "message": "未登录或登录已失效",
  "data": null
}
```

### 2.4 无权限示例

```json
{
  "code": 403,
  "message": "没有权限访问该接口",
  "data": null
}
```

### 2.5 数据不存在示例

```json
{
  "code": 404,
  "message": "物料不存在",
  "data": null
}
```

### 2.6 业务状态冲突示例

```json
{
  "code": 409,
  "message": "订单已完成，不能取消",
  "data": null
}
```

### 2.7 服务器错误示例

```json
{
  "code": 500,
  "message": "服务器内部错误",
  "data": null
}
```

## 3. 分页查询返回格式

分页查询接口的分页信息统一放在 `data` 中。

分页查询请求参数统一使用：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `page` | integer | 当前页码，从 1 开始，默认 1 |
| `page_size` | integer | 每页数据数量，默认 10 |

分页查询接口不要使用 `pageNo`、`pageSize`、`current`、`size` 等其他命名。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `total` | integer | 数据总数 |
| `page` | integer | 当前页码 |
| `page_size` | integer | 每页数据数量 |
| `records` | array | 当前页数据 |

示例：

```json
{
  "code": 200,
  "message": "查询成功",
  "data": {
    "total": 25,
    "page": 1,
    "page_size": 10,
    "records": []
  }
}
```

## 4. 业务状态值设计规范

`status` 表示一条业务数据现在处在哪个阶段。例如采购订单可能是“草稿”“已提交”“部分到货”“已完成”。

各模块负责人可以根据自己的业务流程设计状态值，不需要组长提前把所有状态都列完。但状态值一旦写进 API 文档，就要按统一规则命名和使用。

### 4.1 命名规则

状态值统一使用英文小写和下划线，不直接使用中文。例如：

```text
draft
submitted
in_progress
partial_received
completed
cancelled
```

前端展示给用户时，可以把这些英文状态翻译成中文。

### 4.2 模块状态说明要求

每个模块写接口文档时，需要把自己的状态说明清楚，至少包括：

- 状态值
- 中文含义
- 允许执行的操作
- 状态之间的变化顺序

例如采购订单可以写成：

```text
draft：草稿，可以编辑、提交
submitted：已提交，可以审核、取消
partial_received：部分到货，可以继续收货
completed：已完成，不能继续修改
cancelled：已取消，不能继续操作
```

数据库、后端代码、API 文档和前端传递的状态值必须保持一致。状态值确定后不要随意改名；确实需要修改时，要同步通知相关成员。

### 4.3 推荐记录格式

```text
字段名称：status

状态值：
- draft：草稿
- submitted：已提交
- in_progress：处理中
- completed：已完成
- cancelled：已取消
```

在 OpenAPI 中可以写成：

```yaml
status:
  type: string
  description: 业务状态
  enum:
    - draft
    - submitted
    - in_progress
    - completed
    - cancelled
```

### 4.4 项目核心状态值

以下状态值全组统一使用。各模块负责人可以补充本模块其他状态字段，但不能修改这里已经确定的名称。

| 业务对象 | 字段 | 状态值 |
| --- | --- | --- |
| 采购订单 | `status` | `draft`、`submitted`、`partial_received`、`completed`、`cancelled` |
| 生产订单 | `status` | `pending_review`、`pending_schedule`、`in_progress`、`completed`、`cancelled` |
| 外部订单 | `status` | `pending_review`、`accepted`、`rejected` |
| 库存预警 | `status` | `pending`、`handled`、`ignored` |
| 库存锁定 | `status` | `locked`、`cancelled`、`consumed` |
| 废弃物料检测 | `status` | `pending`、`handled`、`ignored` |
| 采购逾期提醒 | `status` | `pending_urge`、`urged`、`received` |
| 生产线状态 | `status` | `idle`、`running`、`fault` |
| 故障记录 | `status` | `pending_repair`、`repairing`、`recovered` |
| 用户账号 | `status` | `active`、`disabled` |
| 角色 | `status` | `active`、`disabled` |

最终原则是：**大家共用的核心状态值全组统一；各模块新增状态值时，由模块负责人提出，并确认命名风格一致。**

## 5. 公共 Schema 设计规范

### 5.1 定义边界

Schema 可以理解为“接口里某种数据长什么样”。例如物料简要信息包含哪些字段、用户详情包含哪些字段，都属于 Schema。

不需要一开始就定义完所有公共 Schema。各模块负责人维护自己模块的数据结构，组长只维护全项目都会用到的通用结构。

全局通用 Schema 例如：

```text
ApiResponse
PageResult
ErrorResponse
PageQuery
```

这些结构统一放在 `common.yaml` 或主 OpenAPI 文件中，所有模块共同引用。

### 5.2 实体 Schema 维护规则

同一个实体只保留一套统一定义，不要在不同模块里重复造名字。

例如物料的简要信息已经叫 `MaterialBrief`，其他模块需要返回物料信息时，应通过 `$ref` 引用它：

```yaml
material:
  $ref: './material.yaml#/components/schemas/MaterialBrief'
```

不要再新建 `PurchaseMaterialInfo`、`MaterialInfo` 之类含义相近但名字不同的结构。

只有确实会被多个接口复用的数据结构，才需要提取成公共 Schema。只在某个模块内部使用的结构，可以放在本模块里。

以下简要实体 Schema 名称提前统一，其他模块引用时不要重新命名：

```text
MaterialBrief
SupplierBrief
UserBrief
BomVersionBrief
ProductionOrderBrief
PurchaseOrderBrief
```

例如采购订单明细中需要返回物料信息时，应引用 `MaterialBrief`，不要新建 `PurchaseMaterialInfo`。

### 5.3 简要模型和完整模型

同一实体在不同场景下可以拆分为不同模型：

| Schema | 使用场景 |
| --- | --- |
| `MaterialBrief` | 关联查询时使用的简要信息 |
| `MaterialDetail` | 详情查询时使用的完整信息 |
| `MaterialCreateRequest` | 新增请求数据 |
| `MaterialUpdateRequest` | 修改请求数据 |

### 5.4 Schema 命名规范

推荐使用以下命名格式：

```text
实体名 + Brief
实体名 + Detail
实体名 + CreateRequest
实体名 + UpdateRequest
```

不要同时出现 `MaterialInfo`、`MaterialVO`、`MaterialDTO` 等含义不明确的命名。

### 5.5 公共 Schema 修改规则

公共 Schema 可能会被多个接口引用。修改字段名、类型或结构前，要确认不会影响其他模块，并同步通知相关成员。

总结来说：

> 自己模块的数据结构自己维护，其他模块需要时只引用；响应格式、分页格式等全项目通用结构由组长统一维护。

## 6. OpenAPI 公共 Schema 示例

```yaml
components:
  schemas:
    ApiResponse:
      type: object
      required:
        - code
        - message
        - data
      properties:
        code:
          type: integer
          description: 业务状态码，只使用 200、400、401、403、404、409、500
          enum:
            - 200
            - 400
            - 401
            - 403
            - 404
            - 409
            - 500
          example: 200
        message:
          type: string
          description: 返回结果说明
          example: 操作成功
        data:
          description: 实际业务数据，无数据时返回 null
          nullable: true

    ErrorResponse:
      type: object
      required:
        - code
        - message
        - data
      properties:
        code:
          type: integer
          description: 错误业务状态码，只使用 400、401、403、404、409、500
          enum:
            - 400
            - 401
            - 403
            - 404
            - 409
            - 500
          example: 404
        message:
          type: string
          description: 错误原因说明
          example: 物料不存在
        data:
          nullable: true
          description: 错误响应无业务数据，固定返回 null
          example: null

    PageResult:
      type: object
      required:
        - total
        - page
        - page_size
        - records
      properties:
        total:
          type: integer
          description: 数据总数
          example: 25
        page:
          type: integer
          description: 当前页码
          example: 1
        page_size:
          type: integer
          description: 每页数据数量
          example: 10
        records:
          type: array
          description: 当前页数据
          items: {}

    PageQuery:
      type: object
      properties:
        page:
          type: integer
          description: 当前页码，从 1 开始
          default: 1
          example: 1
        page_size:
          type: integer
          description: 每页数据数量
          default: 10
          example: 10
```

## 7. OpenAPI 文件组织

OpenAPI 文档按照数据库设计文档中的六个业务模块进行拆分，每个模块对应一份 YAML 文件。

推荐目录结构如下：

```text
openapi/
├── openapi.yaml              # 项目总入口，汇总所有模块接口
├── common.yaml               # 公共响应格式、分页结构和通用 Schema
│
├── material_bom.yaml         # 物料与 BOM 管理模块
├── inventory.yaml            # 库存管理模块
├── purchase.yaml             # 采购管理模块
├── production.yaml           # 生产管理模块
├── quality_traceability.yaml # 质量追溯模块
└── system.yaml               # 系统管理模块
```

## 8. 字段命名和数据类型

字段命名、数据类型和实体含义应参考语雀中的数据库设计文档，并与数据库表字段、后端模型和前端传参保持一致。

编写接口 Schema 时应注意：

- 字段名优先沿用数据库设计文档中的英文命名。
- 字段类型应与数据库字段类型和实际业务含义一致。
- 同一字段在不同接口中不应出现多种命名方式。
- 与业务状态相关的字段，应遵守本文档第 4 节的状态值设计规范。

## 9. 接口路径和请求方法规范

接口统一只使用 `GET` 和 `POST` 两种请求方法。

- `GET` 用于查询数据，参数放在 query string 中。
- `POST` 用于新增、修改、删除、审核、提交、取消等会改变数据的操作，参数放在请求体中。

接口路径使用动词式命名，让接口名直接表达要做的事情。以物料接口为例：

```text
GET  /api/getMaterialData?id=123
GET  /api/listMaterialData?page=1&page_size=10
POST /api/addMaterialData
POST /api/updateMaterialData
POST /api/deleteMaterialData
```

`POST` 请求体统一使用 JSON 格式，请求头中应声明：

```text
Content-Type: application/json
```

`POST /api/updateMaterialData` 的请求体示例：

```json
{
  "id": 456,
  "material_name": "钢板",
  "specification": "10mm"
}
```

路径和请求方法应遵守以下规则：

| 规则 | 说明 |
| --- | --- |
| 只使用 `GET` 和 `POST` | 不使用 `PUT`、`DELETE` 等方法 |
| 查询使用 `GET` | 例如 `getMaterialData`、`listMaterialData` |
| 改变数据使用 `POST` | 例如 `addMaterialData`、`updateMaterialData`、`deleteMaterialData` |
| `POST` 请求体使用 JSON | 请求头使用 `Content-Type: application/json` |
| 接口名使用动词开头 | 例如 `get`、`list`、`add`、`update`、`delete`、`submit`、`approve`、`cancel` |
| 多个单词使用驼峰命名 | 例如 `/api/getMaterialData`、`/api/updatePurchaseOrder` |
| 分页参数统一命名 | 使用 `page`、`page_size` |

如果某个业务动作不能用简单的增删改查表达，应直接使用能说明业务含义的接口名，例如：

```text
POST /api/submitPurchaseOrder
POST /api/approvePurchaseOrder
POST /api/cancelPurchaseOrder
```

## 10. 登录认证和权限说明

项目登录认证使用 JWT。登录成功后，后端返回 token；前端调用需要登录的接口时，把 token 放在请求头中。

请求头格式统一为：

```text
Authorization: Bearer <token>
```

需要登录的接口，应在 OpenAPI 中声明该请求头。登录接口和刷新 token 等接口不需要携带该请求头，具体以接口说明为准。

在 OpenAPI 文件中，应先在 `components.securitySchemes` 中定义 JWT Bearer 认证：

```yaml
components:
  securitySchemes:
    bearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT
```

需要登录的接口，再添加 `security`：

```yaml
/api/getMaterialData:
  get:
    summary: 查询物料详情
    security:
      - bearerAuth: []
```

不需要登录的接口，例如登录接口，不添加 `security`。

每个需要权限控制的接口应明确说明：

- 是否需要登录
- 哪些角色可以访问
- 外部客户是否只能访问自己的订单
- 无权限访问时的返回规则

无权限访问时，HTTP 状态码仍然返回 `200`，响应体中的业务 `code` 按第 2 节约定返回：未登录或登录失效返回 `401`，已登录但没有权限返回 `403`。

## 11. 错误信息规范

错误响应的 `message` 必须说明具体失败原因，避免只返回笼统的“操作失败”。

不推荐：

```json
{
  "code": 400,
  "message": "操作失败",
  "data": null
}
```

推荐：

```json
{
  "code": 400,
  "message": "采购数量必须大于0",
  "data": null
}
```
