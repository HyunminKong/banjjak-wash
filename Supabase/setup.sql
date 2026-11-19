-- 반짝 워시: 계정별 진행도 저장
-- Supabase 대시보드 → SQL Editor → New query 에 전부 붙여넣고 Run 을 누르세요.
-- 각 플레이어는 자기 데이터만 읽고 쓸 수 있어요 (Row Level Security).

-- 돈, 산 장비, 완료한 현장 같은 프로필 (계정당 1줄)
create table if not exists public.saves (
  user_id uuid primary key references auth.users (id) on delete cascade,
  data jsonb not null default '{}'::jsonb,
  updated_at timestamptz not null default now()
);

-- 현장별 진행도 (계정 × 현장마다 1줄, 4비트로 압축한 때 정보)
create table if not exists public.job_saves (
  user_id uuid not null references auth.users (id) on delete cascade,
  job_id text not null,
  data text not null,
  updated_at timestamptz not null default now(),
  primary key (user_id, job_id)
);

alter table public.saves enable row level security;
alter table public.job_saves enable row level security;

drop policy if exists "own save read" on public.saves;
drop policy if exists "own save insert" on public.saves;
drop policy if exists "own save update" on public.saves;
create policy "own save read" on public.saves for select to authenticated using (auth.uid() = user_id);
create policy "own save insert" on public.saves for insert to authenticated with check (auth.uid() = user_id);
create policy "own save update" on public.saves for update to authenticated using (auth.uid() = user_id) with check (auth.uid() = user_id);

drop policy if exists "own job read" on public.job_saves;
drop policy if exists "own job insert" on public.job_saves;
drop policy if exists "own job update" on public.job_saves;
drop policy if exists "own job delete" on public.job_saves;
create policy "own job read" on public.job_saves for select to authenticated using (auth.uid() = user_id);
create policy "own job insert" on public.job_saves for insert to authenticated with check (auth.uid() = user_id);
create policy "own job update" on public.job_saves for update to authenticated using (auth.uid() = user_id) with check (auth.uid() = user_id);
create policy "own job delete" on public.job_saves for delete to authenticated using (auth.uid() = user_id);

grant select, insert, update on public.saves to authenticated;
grant select, insert, update, delete on public.job_saves to authenticated;
