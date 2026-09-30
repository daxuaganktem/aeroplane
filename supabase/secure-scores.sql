-- Skyline Glide: lock down the scores table.
-- Run this once in Supabase → SQL Editor, after the table from the README exists.
-- It is safe to run again.
--
-- What it does:
--   • Nobody can write to public.scores directly any more; scores only arrive through submit_score().
--   • Each run gets a single-use token from start_run(); the server remembers when the run began.
--   • submit_score() rejects reused or expired tokens and scores faster than the game allows
--     (top speed caps scoring at about 21 points per second).
--   • Per-IP rate limits: 20 run starts and 5 score submissions per minute.
--   • The board keeps the best 1,000 scores; old run tokens are cleared after a day.

-- 1. Readers keep read access; direct writes are closed.
drop policy if exists "anyone can add a score" on public.scores;
revoke insert, update, delete, truncate on public.scores from anon, authenticated;
grant select on public.scores to anon, authenticated;

-- 2. Run tokens. Row-level security with no policies: only the functions below can touch this table.
create table if not exists public.runs (
  id uuid primary key default gen_random_uuid(),
  ip text not null,
  started_at timestamptz not null default now(),
  submitted_at timestamptz,
  score integer
);
alter table public.runs enable row level security;
revoke all on public.runs from anon, authenticated;
create index if not exists runs_ip_started_idx on public.runs (ip, started_at);
create index if not exists runs_ip_submitted_idx on public.runs (ip, submitted_at);
create index if not exists scores_score_idx on public.scores (score desc, created_at);

-- 3. The caller's IP address, taken from the headers Supabase's gateway forwards.
create or replace function public.client_ip() returns text
language sql stable
set search_path = public
as $$
  select coalesce(
    nullif(btrim(split_part(current_setting('request.headers', true)::json ->> 'x-forwarded-for', ',', 1)), ''),
    nullif(current_setting('request.headers', true)::json ->> 'cf-connecting-ip', ''),
    'unknown');
$$;

-- 4. Start a run: returns a single-use token.
create or replace function public.start_run() returns uuid
language plpgsql volatile security definer
set search_path = public
as $$
declare
  v_ip text := public.client_ip();
  v_id uuid;
begin
  if (select count(*) from public.runs
      where ip = v_ip and started_at > now() - interval '1 minute') >= 20 then
    raise exception 'rate_limited' using errcode = 'P0001';
  end if;
  insert into public.runs (ip) values (v_ip) returning id into v_id;
  return v_id;
end;
$$;

-- 5. Keep the board and the token table small.
create or replace function public.prune_scores() returns void
language sql volatile security definer
set search_path = public
as $$
  delete from public.scores
  where id not in (select id from public.scores order by score desc, created_at asc limit 1000);
  delete from public.runs where started_at < now() - interval '1 day';
$$;

-- 6. Submit a score for a run.
create or replace function public.submit_score(p_run uuid, p_name text, p_score integer) returns void
language plpgsql volatile security definer
set search_path = public
as $$
declare
  v_ip text := public.client_ip();
  v_run public.runs%rowtype;
  v_name text;
  v_secs double precision;
begin
  -- same clean-up the game applies: A–Z, 0–9, space . _ -, at most 12 characters
  v_name := upper(regexp_replace(coalesce(p_name, ''), '[^A-Za-z0-9 ._-]', '', 'g'));
  v_name := left(btrim(regexp_replace(v_name, '\s+', ' ', 'g')), 12);
  if length(v_name) = 0 then
    raise exception 'bad_name' using errcode = 'P0001';
  end if;
  if p_score is null or p_score < 1 or p_score > 99999 then
    raise exception 'bad_score' using errcode = 'P0001';
  end if;

  if (select count(*) from public.runs
      where ip = v_ip and submitted_at > now() - interval '1 minute') >= 5 then
    raise exception 'rate_limited' using errcode = 'P0001';
  end if;

  select * into v_run from public.runs where id = p_run for update;
  if not found or v_run.submitted_at is not null then
    raise exception 'bad_run' using errcode = 'P0001';
  end if;
  if v_run.started_at < now() - interval '2 hours' then
    raise exception 'run_expired' using errcode = 'P0001';
  end if;

  -- At top speed the game awards about 21 points a second; allow a little slack for network delay.
  v_secs := extract(epoch from (now() - v_run.started_at));
  if p_score > 22 * v_secs + 20 then
    raise exception 'implausible' using errcode = 'P0001';
  end if;

  update public.runs set submitted_at = now(), score = p_score where id = p_run;
  insert into public.scores (name, score) values (v_name, p_score);

  -- tidy up now and then instead of on a schedule
  if random() < 0.02 then
    perform public.prune_scores();
  end if;
end;
$$;

-- 7. Only the two game endpoints are callable from the browser.
revoke all on function public.client_ip() from public, anon, authenticated;
revoke all on function public.prune_scores() from public, anon, authenticated;
revoke all on function public.start_run() from public;
revoke all on function public.submit_score(uuid, text, integer) from public;
grant execute on function public.start_run() to anon, authenticated;
grant execute on function public.submit_score(uuid, text, integer) to anon, authenticated;
