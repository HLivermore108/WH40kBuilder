# Army Forge — Warhammer 40,000 companion

A Unity 6 application for building army rosters, reading datasheets, and tracking a tabletop battle. Windows first, with a responsive UI Toolkit layout. No accounts or book unlocks.

## Run

1. Open this folder in **Unity 6000.3.10f1** (the existing project version).
2. Open `Assets/Scenes/SampleScene.unity` and press **Play**. The application creates its interface automatically; no scene wiring is required.
3. Choose **New army**, select a faction and battle size, then add and configure units.
4. Choose detachments, a Warlord, enhancements, bodyguard attachments and paid wargear. Record free weapon choices in each unit's notes.
5. Choose **Start battle with this army**. For mixed-profile units, use **Model profiles** to assign the correct profile to each model before tracking damage.

Build a Windows executable with **Army Forge → Build Windows app**. Output: `Builds/Windows/ArmyForge.exe`. Keep the executable, its `_Data` folder and adjacent Unity runtime files together.

## Included

- 1,501 datasheets across 24 populated factions from the bundled Wahapedia 11th-edition export; 1,499 have model prices. Space Marine chapter units appear under Space Marines. The provider also lists an empty Unbound Adversaries category.
- Offline unit library with profiles, weapons, abilities, composition, wargear, transport and leader rules, plus source links.
- Multiple saved armies; duplicate, delete, JSON import and JSON/plain-text export.
- Unit sizes, repeated-unit price bands, paid wargear quantities, enhancements and basic army checks.
- Detachment selection and rules, including the basic Incursion exception for a single 3 DP detachment.
- Independent battle snapshots, per-model wound controls, destroyed models, battle-shock, rounds, active player and phases.
- Both players' primary VP, secondary VP and command points. Values are manual and never awarded automatically.
- Undo for battle changes during the current session. Battles and rosters survive app restarts.
- Larger, higher-contrast text, separated weapon stat cells, and an extra-large-text preference under Data.
- Main-page and unit-catalogue scroll positions survive selections, unit additions, wounds and score updates.
- The 18-card Chapter Approved 2026–27 tactical secondary deck, separately shuffled and saved for both players. Use **Secondaries** or **Battle → Draw secondaries**.
- On-demand catalogue updates with timeouts, source-version checks, validation and retention of the previous catalogue on failure.

## Data

Source: [Wahapedia data export](https://wahapedia.ru/wh40k11ed/the-rules/data-export/), [export specification](https://wahapedia.ru/wh40k11ed/Export%20Data%20Specs.xlsx), and [11th-edition core rules](https://wahapedia.ru/wh40k11ed/the-rules/core-rules/).

The original UTF-8 pipe-delimited exports are stored as `.txt` assets in `Assets/Resources/ArmyData` so Unity imports them as TextAssets. `Last_update.txt` records the source's export time in UTC+3. IDs remain strings. HTML descriptions are converted to plain text; no remote HTML or scripts are executed.

**Data → Update from Wahapedia** downloads the supported tables over HTTPS. It builds and validates a new catalogue before replacing the cached version. Existing rosters retain their own profiles, prices and rules, so an update cannot alter an ongoing battle. Create a new roster for new prices. The provider may include older source books, Legends and virtual units; source names and editions appear on sheets. Virtual units and units without a parsed model price/profile cannot be added directly.

Data powered by Wahapedia. This is an unofficial fan project, not affiliated with Games Workshop. Warhammer 40,000 and associated names belong to their respective owners.

## Validation boundaries

Secondary cards use short reminders with a link to [current mission rules](https://wahapedia.ru/wh40k11ed/the-rules/warhammer-event-companion/#Secondary-Mission-deck). The deck draws two cards once per player's Command phase per round, retaining any existing active cards. Discarded cards are not recycled automatically. The paid replacement spends 1 CP once per player per battle during that player's Command phase. Card-specific redraw conditions, scoring timing, VP caps and CP earned from discarding remain manual. The discard reshuffle is an explicit correction tool. Fixed missions are not implemented.

The app is a roster and table companion, **not a complete tournament legality engine**. It checks points, basic duplicate limits, a Character Warlord, detachment budgets, enhancement counts and basic attachment constraints. Review chapter restrictions, mandatory units/Warlords, allies, wargear quantities, model composition, faction exceptions and chapter-specific DP against current source rules. Allied-faction selection is not automated. Multiple source pricing rows may require review where the export lacks qualifying context. Free loadout choices are notes, rather than a weapon-allocation solver.

Mixed units initially use their first profile for every model. The battle screen exposes per-model profile assignment; it does not infer mixed composition. Opponent tracking covers scores and CP, not an opponent roster. There is no cloud sync, purchase verification, mission scoring automation or mobile packaging yet.

## Storage

Windows saves normally live in `%USERPROFILE%/AppData/LocalLow/ArmyForge/Army Forge`. **Data → Open save folder** opens the actual location reported by Unity.

- `armies.json`: rosters and current battle, with a `.bak` backup on replacement.
- `catalog.json`: the last successfully downloaded catalogue, when present.
- `Exports/`: roster JSON and readable text exports.

Writes use a temporary file and replacement; malformed JSON falls back to the backup. Unreadable primary and backup files are preserved and reported. JSON imports create a new army ID rather than overwriting an existing army. Only import roster files you intend to use.

## Implementation and verification

- `Assets/Scripts/Core`: serializable models, basic validation and pipe-delimited import; no scene dependencies.
- `Assets/Scripts/Runtime`: UI Toolkit interface, local storage and catalogue updates.
- `Assets/Resources/ArmyForge.uss`: responsive dark/gold interface styling.
- `Assets/Tests/Editor`: import coverage, parser edge cases, real repeated-unit pricing, wound bounds, phase transitions, save recovery, deck integrity, serialization, scroll retention and the army-to-battle workflow.

Run the tests in Unity's **Window → General → Test Runner → EditMode**. Do not start a second Unity editor against the same project folder while it is already open; use an isolated copy for command-line verification.
