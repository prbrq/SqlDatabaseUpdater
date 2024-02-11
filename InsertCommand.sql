CREATE OR REPLACE FUNCTION public.errorlogf()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
IF TG_OP = 'INSERT' then
PERFORM pg_notify('notifyerrorlogs', format('INSERT %s', NEW."Id"));
ELSIF TG_OP = 'UPDATE' then
PERFORM pg_notify('notifyerrorlogs', format('UPDATE %s %s', OLD."Id"));
ELSIF TG_OP = 'DELETE' then
PERFORM pg_notify('notifyerrorlogs', format('DELETE %s %s', OLD."Id"));
END IF;
RETURN NULL;
END;
$function$
;