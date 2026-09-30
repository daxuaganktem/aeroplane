# Opponent aircraft: image prompt

Use this with an image generator (ChatGPT, Midjourney, Gemini, and similar tools). Generate **one image per aircraft**: paste the shared style block, then add that aircraft's line from the list below. When you're done, send the ten PNGs back and the game will use them for the oncoming traffic.

## Shared style (paste this every time)

```
Flat 2D vector illustration of a single aircraft in a clean, simple mobile-game style.
Exact side view (profile), nose pointing to the RIGHT, perfectly level, no perspective, no tilt.
Style: flat solid colours with no gradients, no texture and no black outlines. Shapes are defined by
colour blocks only, with at most one lighter or darker shade per surface. White fuselage with a
light grey lower half, a row of small dark grey rectangular passenger windows where the aircraft has
them, a dark navy cockpit window, and light grey wings and tailplane.
Livery: every livery-coloured part (tail fin, belly stripe, engine nacelles, door outlines and any
accent stripe) must be painted in pure flat magenta #FF00FF, so the game can recolour it.
Background: fully transparent (PNG with an alpha channel). No ground, sky, clouds, shadow,
reflection, text, logo, registration number or watermark.
Framing: the aircraft is centred and fills about 90% of the image width, with even empty
margins. Image size 1024 × 512 pixels.
Wheels retracted (not visible), unless the description below says otherwise.
```

## One line per aircraft

1. **Biplane:** `A 1920s open-cockpit biplane: two stacked straight wings joined by struts, a round engine cowling with a spinning propeller shown as a translucent grey disc at the nose, a small tail fin, and fixed landing gear with two spoked wheels. Wings and tail fin in magenta.`
2. **Blimp:** `A small advertising airship: a long smooth white envelope with rounded ends, four tail fins in magenta, a wide magenta stripe along the middle of the envelope, and a small white gondola with three windows hanging underneath.`
3. **Light plane:** `A four-seat high-wing light aircraft like a Cessna 172: wing mounted on top of the cabin with one diagonal strut, large cabin side windows, a single propeller shown as a translucent grey disc at the nose, and a small swept tail.`
4. **Helicopter:** `A light civilian helicopter: a rounded bubble cabin with a large dark tinted windscreen, a slim tail boom ending in a small fin and a tail rotor disc, landing skids, and a main rotor drawn as a thin blurred horizontal bar across the top.`
5. **Vintage propliner:** `A 1940s twin-engine propeller airliner like a Douglas DC-3: rounded nose, tall curved tail fin, square passenger windows, an engine nacelle on the wing with a translucent grey propeller disc, and a small tail wheel.`
6. **WWII fighter:** `A 1940s single-seat propeller fighter like a Spitfire: slim fuselage painted entirely in magenta, a light grey belly, elliptical wings, a bubble cockpit canopy, a propeller disc at the nose with a pointed spinner, a round white roundel on the side, and a small tail wheel.`
7. **Regional jet:** `A 50-seat regional jet like a Bombardier CRJ: slim fuselage, T-tail with the tailplane on top of the fin, two engines mounted on the rear fuselage below the tail, and a small swept low wing.`
8. **Widebody airliner:** `A large twin-engine widebody airliner like a Boeing 777: long fuselage with two doors outlined, a tall swept tail fin, a large swept wing, and one very large engine nacelle under the wing.`
9. **Supersonic jet:** `A supersonic passenger jet like Concorde: very long slender fuselage with a pointed, slightly drooped needle nose, a large delta wing low on the fuselage, engines in a long box nacelle under the wing, a tall swept fin and no tailplane.`
10. **Fighter jet:** `A modern single-seat jet fighter like an F-16: pointed nose, bubble canopy, cropped delta wing, a single swept vertical fin, a small tailplane, and an orange afterburner flame at the exhaust. Fuselage in mid grey with magenta tail fin and stripes.`

## Tips for consistent results

- Keep one chat or session and say "same style as before" for each new aircraft, so the set matches.
- If the tool won't make a transparent background, ask for a **pure white background** instead. The game can cut it out.
- If the magenta comes out as pink or purple, that's fine. Anything clearly pink-magenta gets recoloured.
- Name the files `biplane.png`, `blimp.png`, `cessna.png`, `helicopter.png`, `propliner.png`, `spitfire.png`, `regional.png`, `widebody.png`, `concorde.png` and `fighter.png`.

When you send them back, I'll trace each image's outline for accurate crash detection, recolour the magenta to each plane's livery, and mirror them so they fly towards you.
