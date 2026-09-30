-- Skyline Glide: player accounts.
-- Run this in Supabase → SQL Editor after secure-scores.sql. It is safe to run again.
--
-- Signed-in players get one profile row holding their name and saved progress
-- (achievements, stats, best score, unlocks, today's challenges). Each player can
-- read and write only their own row; nobody else can see it.

create table if not exists public.profiles (
  user_id uuid primary key references auth.users (id) on delete cascade,
  name text check (name is null or char_length(name) between 1 and 12),
  progress jsonb not null default '{}'::jsonb check (pg_column_size(progress) < 32000),
  updated_at timestamptz not null default now()
);

alter table public.profiles enable row level security;

drop policy if exists "players read their own profile" on public.profiles;
drop policy if exists "players create their own profile" on public.profiles;
drop policy if exists "players update their own profile" on public.profiles;
create policy "players read their own profile" on public.profiles
  for select to authenticated using ((select auth.uid()) = user_id);
create policy "players create their own profile" on public.profiles
  for insert to authenticated with check ((select auth.uid()) = user_id);
create policy "players update their own profile" on public.profiles
  for update to authenticated using ((select auth.uid()) = user_id) with check ((select auth.uid()) = user_id);

revoke all on public.profiles from anon, authenticated;
grant select, insert, update on public.profiles to authenticated;
