# Unlockable player planes: image prompt

Use this with an image generator, the same way as the opponent prompt (`docs/opponent-image-prompt.md`). Generate **one image per plane**: paste the shared style block, then add that plane's line. These are the planes players fly and pick in the hangar, so they're drawn in a little more detail than the opponents.

**Shared with the opponents:** five planes appear in both sets (light plane, biplane, regional jet, supersonic jet and fighter jet). If you've already made those opponent images and like them, you can reuse the files. The game can mirror and recolour one image for both uses.

## Shared style (paste this every time)

```
Flat 2D vector illustration of a single aircraft for a mobile game, drawn as the player's hero plane.
Exact side view (profile), nose pointing to the RIGHT, perfectly level, no perspective, no tilt.
Style reference: a clean commercial-airliner illustration made of flat colour blocks. No gradients,
no texture, no black outlines; at most a thin soft grey edge line. Each surface has at most one lighter
or darker shade. Bright white fuselage with a light grey lower third; a crisp row of small dark grey
rectangular passenger windows; outlined passenger doors; a dark navy slanted cockpit window at a
rounded nose; light grey wings with a white leading edge; a light grey horizontal tailplane.
Livery: every livery-coloured part (tail fin, the darker stripe along the back edge of the fin, belly
stripe, engine nacelles, door outlines and any accent stripe) must be painted in pure flat magenta
#FF00FF, with the fin's back-edge stripe in a darker magenta #B000B0, so the game can recolour it.
Background: fully transparent (PNG with an alpha channel). No ground, sky, clouds, shadow,
reflection, text, logo, registration number or watermark.
Framing: the aircraft is centred and fills about 90% of the image width, with even empty
margins. Image size 1024 × 512 pixels.
Landing gear retracted (not visible), unless the description below says otherwise.
```

## One line per plane (in unlock order)

1. **Airliner (from the start):** `A single-aisle twin-jet airliner like an Airbus A320 or Boeing 737: a tall swept tail fin, a row of about fifteen windows between two outlined doors, a swept low wing with one engine nacelle hanging under it, a small far-side wingtip just visible above the fuselage, and a tiny antenna on the roof.`
2. **Light plane (100 points):** `A four-seat high-wing light aircraft like a Cessna 172: wing on top of the cabin with one diagonal strut, large dark cabin windows, a pointed magenta spinner with the propeller shown as a translucent grey disc, and a small swept tail fin.`
3. **Turboprop (200 points):** `A 70-seat regional turboprop like an ATR 72: slim fuselage, a high wing on top of the fuselage, an engine nacelle under the wing with a large translucent grey propeller disc, a T-tail with the tailplane on top of the fin, and a small landing-gear fairing under the belly.`
4. **Biplane (300 points):** `A 1920s open-cockpit biplane: white fuselage with a magenta belly, two stacked straight wings in magenta joined by struts, a silver engine cowling with a translucent grey propeller disc at the nose, a curved tail fin, and fixed landing gear with one visible wheel (this plane always shows its wheels).`
5. **Business jet (450 points):** `A small business jet like a Learjet or Cessna Citation: short slim fuselage with five oval windows, one door just behind the cockpit, a T-tail with the tailplane on top of the fin, two engines mounted on the rear fuselage below the tail, and a small swept wing with an upturned winglet.`
6. **Seaplane (600 points):** `A four-seat high-wing floatplane: the same light aircraft as a Cessna 172 but standing on two long white floats connected by thin struts, with a magenta stripe along each float. No wheels.`
7. **Regional jet (800 points):** `A 50-seat regional jet like a Bombardier CRJ: long slim fuselage with a row of small windows, two doors outlined, a T-tail, two engines mounted on the rear fuselage below the tail, and a small swept low wing.`
8. **Jumbo jet (1,000 points):** `A four-engine jumbo jet like a Boeing 747: a raised upper deck hump over the front of the fuselage with its own short row of windows, a long main-deck window row between outlined doors, a very tall swept fin, a large swept wing with two engine nacelles visible under it (one further back and lower), and a far-side wingtip showing above the fuselage.`
9. **Supersonic (1,300 points):** `A supersonic passenger jet like Concorde: very long slender fuselage with a pointed needle nose, a tiny row of small windows, a large delta wing low on the fuselage, engines in one long box nacelle under the wing, and a tall swept fin with no tailplane.`
10. **Fighter jet (1,600 points):** `A modern single-seat jet fighter like an F-16: silver-grey fuselage with a darker grey lower half, pointed nose, dark tinted bubble canopy with a small white glint, cropped delta wing, a single swept magenta fin with a darker magenta back edge, a magenta stripe along the fuselage, a small tailplane, and a short orange-and-yellow afterburner flame at the exhaust.`

## Optional: hangar versions with wheels down

The hangar shows each plane parked with its wheels down. If you'd like that too, make a second image of each plane with the same prompt, replacing the last line of the style block with:

```
Landing gear extended: short dark grey gear legs with round black tyres, positioned where this
aircraft's real landing gear would be (main gear under the wing, nose gear under the cockpit).
Keep the aircraft in exactly the same position and size as the gear-up version.
```

Name them with `-gear`, for example `airliner-gear.png`. Skip this and the game draws the wheels itself.

## Tips for consistent results

- Make the **airliner first**, then say "same style, same size and same colours as the airliner" for each one after it. The airliner sets the look of the whole hangar.
- If the tool won't make a transparent background, ask for a **pure white background**. The game can cut it out.
- If the magenta comes out pink or purple, that's fine. Anything clearly pink-magenta gets recoloured, and the darker fin stripe becomes a darker shade of the player's colour.
- Name the files `airliner.png`, `cessna.png`, `turboprop.png`, `biplane.png`, `bizjet.png`, `seaplane.png`, `regional.png`, `jumbo.png`, `concorde.png` and `fighter.png`.

When you send them back, I'll trace each outline for crash detection, map the magenta to the six liveries in the hangar, keep the grey silhouette look for locked planes, and use the gear-down versions in the hangar if you made them.
