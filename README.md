# Skyline Glide

A one-button, mobile-first browser game: fly a little airliner over city tower clusters and dodge oncoming traffic.

- **Hangar:** pixel-art planes you unlock with your best score: Airliner (from the start), Turboprop (150), Business jet (400), Jumbo jet (800) and Fighter jet (1500), each in six liveries. Locked planes show as silhouettes. The crash screen has **Fly again** and **Home**.
- **Tap** anywhere to climb, **hold** to keep climbing (Space / ↑ on desktop).
- **Fuel:** taps sip fuel, holding burns it fast, and it refills while you're off the throttle. Run dry and the engine cuts out until the tank is back to a quarter.
- Towers rise from the ground in clusters of 2–4 buildings of equal height, with varied rooftops.
- Oncoming traffic gets more aggressive with your score: biplanes (0), propliners (200), jets that steer toward you (450), then fighter jets that chase your altitude (800). A red chevron on the right edge warns you before fast ones arrive.
- Day and night swap every 500 points.

`src/game.html` is the source. Run `./build.sh` to regenerate the standalone `index.html`, which you can open directly or serve from any static host.

## Top pilots board

The board shows the all-time top 10 pilots, one row per pilot (their best run). When you set a new personal best, the game asks for your name once, then saves future bests under it automatically. It picks a backend in this order:

1. **claude.ai artifact.** A board shared by everyone who opens the page. Anyone signed in can read it. Posting needs the owner, an invited Editor, or a Contributor on a team plan.
2. **Supabase (optional, free).** Makes the board global anywhere you host `index.html`, like GitHub Pages. See below.
3. **This device.** Otherwise the board lives in the browser's local storage.

### Turning on the Supabase board

1. Create a free project at [supabase.com](https://supabase.com).
2. In **SQL Editor**, run:

   ```sql
   create table public.scores (
     id bigint generated always as identity primary key,
     name text not null check (char_length(name) between 1 and 12),
     score integer not null check (score between 0 and 99999),
     created_at timestamptz not null default now()
   );
   alter table public.scores enable row level security;
   create policy "anyone can read scores" on public.scores for select using (true);
   create policy "anyone can add a score" on public.scores for insert with check (true);
   grant select, insert on public.scores to anon;
   ```

3. In **Project Settings → API**, copy the Project URL and the `anon` public key into `SUPABASE_URL` and `SUPABASE_ANON_KEY` near the top of the script in `src/game.html`, then run `./build.sh`.

The anon key is meant to be public. The policies above allow reading and adding scores but not editing or deleting them. Scores are submitted by the browser, so a determined player could post a fake one. That's fine for a casual game, but it isn't cheat-proof.
