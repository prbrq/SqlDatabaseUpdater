BEGIN;
TRUNCATE TABLE users;
INSERT INTO users (id, "name") values (1, 'Ivan');
-- Q @EXEC(.\InsertCommand.sql);
-- Q UPDATE users SET "name" = '@READ(.\UpdateData.txt)' WHERE id = 12;
END;
