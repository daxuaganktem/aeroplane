# Skyline Glide

A one-button, mobile-first browser game: fly a little airliner over city tower clusters and dodge oncoming traffic.

- **Hangar:** ten planes you unlock with your best score: Airliner (from the start), Light plane (100), Turboprop (200), Biplane (300), Business jet (450), Seaplane (600), Regional jet (800), Jumbo jet (1000), Supersonic (1300) and Fighter jet (1600), each in six liveries. Locked planes show as silhouettes. The crash screen has **Fly again** and **Home**.
- **Tap** anywhere to climb, **hold** to keep climbing (Space / ↑ on desktop).
- **Fuel:** taps sip fuel, holding burns it fast, and it refills while you're off the throttle. Run dry and the engine cuts out until the tank is back to a quarter.
- Towers rise from the ground in clusters of 2–4 buildings of equal height, with varied rooftops, water tanks, billboards, lobbies and blinking aviation beacons. Windows catch a sweeping glint by day and flicker on and off at night.
- Ten kinds of oncoming traffic join as your score climbs, each faster or more aggressive: biplanes and blimps (0), light planes (100), helicopters (200), vintage propliners (300), WWII fighters (450), regional jets (600), widebody airliners (800), supersonic jets (1000) and fighter jets that chase your altitude (1200). A red chevron on the right edge warns you before fast ones arrive.
- Day and night swap every 500 points.
- **Landmarks** sometimes replace a normal cluster, each announced with a banner as it scrolls in:
  - Twin Towers
  - a stadium with floodlights and a crowd
  - a TV tower
  - a clock tower that shows the real time
  - a pyramid tower
- **Daily challenges:** three a day, the same for everyone on a given date, picked from nine kinds. Examples: "Fly past 4 helicopters", "Score 300 without running out of fuel", "Fly past the stadium". They reset at local midnight and are tracked on the **Daily** tab.
- **Achievements:** 21 of them, from *First flight* to *Grand tour* (all five landmarks), *Plane spotter* (all ten opponent types) and *Dead stick* (stay airborne 5 seconds with an empty tank). They pop up as you earn them and are listed on the **Awards** tab. Progress is saved on the device.

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

   Then run [`supabase/secure-scores.sql`](supabase/secure-scores.sql) the same way. It closes direct writes to the table so scores can only arrive through checked server functions (details below).

3. In **Project Settings → API Keys**, copy the Project URL and the publishable key (`sb_publishable_…`, or the legacy `anon` key) into `SUPABASE_URL` and `SUPABASE_ANON_KEY` near the top of the script in `src/game.html`, then run `./build.sh`. The key is sent only in the `apikey` header.

This repo is already connected to its own Supabase project, so the GitHub Pages build uses the global board.

The publishable key is meant to be public. The policies above allow reading and adding scores but not editing or deleting them. Scores are submitted by the browser, so a determined player could post a fake one. That's fine for a casual game, but it isn't cheat-proof.

### Anti-cheat and spam protection

`supabase/secure-scores.sql` moves score saving behind two database functions:

- **`start_run()`**: the game calls it when a run begins and gets a single-use token. The server records the start time and the caller's IP.
- **`submit_score(run, name, score)`**: rejects the score if any of these apply:
  - the token is unknown, already used, or older than 2 hours
  - the score is faster than the game allows: top speed caps scoring at about 21 points a second, so the server checks it against the real time since `start_run`
  - the name is empty after clean-up

**Rate limits and cleanup:**
- Each IP address can start 20 runs and submit 5 scores per minute.
- The board keeps the best 1,000 scores, and run tokens are deleted after a day.

**Limits:** this stops scripted spam and instant fake scores. Someone willing to wait out a real run's length can still post a score they didn't earn, and bots that genuinely play are not detected. For those, see replay verification and Turnstile in the project notes.

## Player accounts (optional sign-in)

On the GitHub Pages build, a **SIGN IN** button in the top-left corner lets players sign in with Google. Their achievements, stats, best score, unlocked planes, plane and livery choice, and today's challenge progress are saved to their account. These follow them to any device, merging with what's already on that device:
- counters keep the higher value
- achievements keep the earliest date earned
- today's challenges keep the furthest progress

Guests can still play and use the board without signing in. The claude.ai version never shows the button.

### Setting it up

1. **Run the SQL.** In Supabase → **SQL Editor**, run [`supabase/secure-scores.sql`](supabase/secure-scores.sql) again (it now links scores to accounts), then [`supabase/accounts.sql`](supabase/accounts.sql).
2. **Set where sign-in returns.** In Supabase → **Authentication → URL Configuration**:
   - set **Site URL** to `https://daxuaganktem.github.io/aeroplane/`
   - add that same address under **Redirect URLs**
3. **Create a Google OAuth client.** In [Google Cloud Console](https://console.cloud.google.com/) → **APIs & Services**:
   - configure the **OAuth consent screen**: choose External, then add the app name and your email
   - under **Credentials**, choose **Create credentials → OAuth client ID → Web application**
   - under **Authorized redirect URIs**, add `https://nsymcgtlbelmtshkbsok.supabase.co/auth/v1/callback`
   - copy the **Client ID** and **Client secret**
4. **Turn on Google in Supabase.** Go to **Authentication → Sign In / Providers → Google**, enable it, paste the Client ID and secret, and save.

To also offer GitHub sign-in, create an OAuth app under GitHub **Settings → Developer settings → OAuth Apps** with the same callback URL. Enable **GitHub** in Supabase, then add `{ id: 'github', label: 'CONTINUE WITH GITHUB' }` to `AUTH_PROVIDERS` in `src/game.html`.
