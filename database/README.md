## 数据库初始化说明

在 Oracle 数据库启动后，请依次执行以下命令（如为 Docker 容器环境，请先在设定正确字符集的情况下进入容器 `docker exec -it -e NLS_LANG="AMERICAN_AMERICA.AL32UTF8" oracle bash`）

```sh
sqlplus / as sysdba
```

```sql
ALTER SESSION SET CONTAINER = FREEPDB1;
CREATE USER "<username>" IDENTIFIED BY "<password>";
GRANT CONNECT, RESOURCE, DBA TO "<username>";
ALTER USER "<username>" QUOTA UNLIMITED ON USERS;
exit;
```

两份 .sql 脚本分别为 表结构与预置数据的初始化脚本 和 数据库层逻辑初始化脚本（包括触发器、存储过程、函数等），需按顺序执行。

```sh
sqlplus '<username>/<password>@//localhost:1521/FREEPDB1' @1_schema_data_init.sql
sqlplus '<username>/<password>@//localhost:1521/FREEPDB1' @2_logic_init.sql
```
