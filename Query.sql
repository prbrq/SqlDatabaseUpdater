BEGIN; -- 2024-05-20 1
TRUNCATE TABLE users;
INSERT INTO users (id, "name") values (1, 'Ivan');
-- Q @EXEC(.\InsertCommand.sql);
INSERT INTO users (id, "name") values (12, 'TemplateName');
-- Q UPDATE users SET "name" = '@READ(.\UpdateData.txt)' WHERE id = 12;
END;

BEGIN; -- 2023-02-25 1
INSERT INTO users (id, "name") values (100, 'SomeName');
INSERT INTO users (id, "name") values (1001, 'QQQ');
END;

BEGIN; -- 2024-02-11 1
-- Q insert into users (id, "name") values (0, '@READ(./UpdateData.txt)');
END;