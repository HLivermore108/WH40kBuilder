using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace ArmyForge
{
    [RequireComponent(typeof(UIDocument))]
    public partial class ArmyForgeApp : MonoBehaviour
    {
        Catalog catalog;
        SaveData data;
        Army army;
        VisualElement root, content, overlay;
        Label status;
        string page = "Armies", search = "", factionId;
        bool syncing;
        string saveError;
        string renderedPage;
        int secondaryPlayer;
        readonly Dictionary<string, Vector2> scrollPositions = new Dictionary<string, Vector2>();
        ScrollView catalogueScroll;
        Stack<string> battleUndo = new Stack<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<ArmyForgeApp>() != null) return;
            var host = new GameObject("Army Forge");
            DontDestroyOnLoad(host);
            var document = host.AddComponent<UIDocument>();
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ArmyForgeTheme");
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.sortingOrder = 10;
            document.panelSettings = settings;
            host.AddComponent<ArmyForgeApp>();
        }
        void Start()
        {
            Application.targetFrameRate = 60;
            root = GetComponent<UIDocument>().rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("ArmyForge"));
            root.AddToClassList("app");
            root.RegisterCallback<GeometryChangedEvent>(e => root.EnableInClassList("compact", e.newRect.width < 900));
            try
            {
                catalog = LocalStore.Load<Catalog>("catalog.json") ?? WahapediaImport.Build(name => {
                    var asset = Resources.Load<TextAsset>("ArmyData/" + name);
                    if (asset == null) throw new FileNotFoundException("Missing bundled table: " + name);
                    return asset.text;
                });
                data = LocalStore.Load<SaveData>("armies.json") ?? new SaveData();
                if (data.version != 1 || data.armies == null) throw new FormatException("Unsupported save version. Your files have been preserved.");
                factionId = catalog.factions.First().id;
                army = data.armies.FirstOrDefault();
                Shell();
            }
            catch (Exception ex)
            {
                Text(root, "Could not open Army Forge", "title");
                Text(root, ex.Message, "warning");
                Button(root, "Open save folder", OpenSaveFolder);
                Debug.LogException(ex);
            }
        }
        void Shell()
        {
            if (renderedPage != null)
                foreach (var scroll in root.Query<ScrollView>().ToList().Where(x => !string.IsNullOrEmpty(x.name)))
                    scrollPositions[renderedPage + "/" + scroll.name] = scroll.scrollOffset;
            root.Clear();
            overlay = null;
            root.EnableInClassList("extra-large", data.extraLargeText);
            var header = Box(root, "header");
            Text(header, "AF", "brand-mark");
            var brand = Box(header, "brand");
            Text(brand, "ARMY FORGE", "brand-title");
            Text(brand, "WARHAMMER 40,000  /  FIELD COMPANION", "eyebrow");
            Text(header, "11TH EDITION", "edition");
            var nav = Box(root, "nav");
            foreach (var name in new[] { "Armies", "Datasheets", "Battle", "Secondaries", "Data" })
            {
                var tab = Button(nav, name, () => { page = name; Shell(); });
                tab.EnableInClassList("selected", page == name);
            }
            // Keep the actual scroller alive across refreshes, not just its last pixel offset.
            // Replacing it resets its scroll range to zero and causes a visible jump.
            var mainScroll = content as ScrollView ?? new ScrollView(ScrollViewMode.Vertical) { name = "page-scroll" };
            mainScroll.Clear(); content = mainScroll; content.AddToClassList("content"); root.Add(content);
            status = Text(root, saveError ?? "OFFLINE READY  •  Powered by Wahapedia  •  Export " + catalog.updated + " (UTC+3)", "status");
            switch (page) { case "Armies": Armies(); break; case "Datasheets": Library(); break; case "Battle": BattleView(); break; case "Secondaries": SecondaryView(); break; default: DataView(); break; }
            renderedPage = page;
            foreach (var scroll in root.Query<ScrollView>().ToList().Where(x => !string.IsNullOrEmpty(x.name)))
            {
                var offset = scrollPositions.TryGetValue(page + "/" + scroll.name, out var savedOffset) ? savedOffset : Vector2.zero;
                if (scroll.verticalScroller.highValue >= offset.y)
                { scroll.scrollOffset = offset; continue; }
                // Wait for layout before restoring: a new ScrollView initially has a zero scroll range.
                EventCallback<GeometryChangedEvent> restore = null;
                restore = _ => {
                    scroll.contentContainer.UnregisterCallback(restore);
                    scroll.schedule.Execute(() => scroll.scrollOffset = offset);
                };
                scroll.contentContainer.RegisterCallback(restore);
            }
        }
        void Persist() { try { LocalStore.Save("armies.json", data); saveError = null; } catch (Exception e) { saveError = "Save failed: " + e.Message; Notify(saveError); } }
        void Notify(string message) { if (status != null) status.text = message; }
        static VisualElement Box(VisualElement parent, string css = "card") { var box = new VisualElement(); box.AddToClassList(css); parent.Add(box); return box; }
        static Label Text(VisualElement parent, string value, string css = "body") { var label = new Label(value ?? ""); label.enableRichText = false; label.AddToClassList(css); parent.Add(label); return label; }
        static Button Button(VisualElement parent, string label, Action action, string css = null) { var b = new Button(action) { text = label }; if (css != null) b.AddToClassList(css); parent.Add(b); return b; }
        static void Input(VisualElement parent, string label, string value, Action<string> changed, bool multiline = false)
        {
            var f = new TextField(label) { value = value ?? "", multiline = multiline, isDelayed = !multiline };
            f.RegisterValueChangedCallback(e => changed(e.newValue)); parent.Add(f);
        }
        static void Integer(VisualElement parent, string label, int value, int min, int max, Action<int> changed)
        {
            var f = new IntegerField(label) { value = value, isDelayed = true };
            f.RegisterValueChangedCallback(e => { int n = Mathf.Clamp(e.newValue, min, max); f.SetValueWithoutNotify(n); changed(n); }); parent.Add(f);
        }
        static DropdownField Choose(VisualElement parent, string label, List<string> choices, int index, Action<int> changed)
        {
            var f = new DropdownField(label, choices, Mathf.Clamp(index, 0, Math.Max(0, choices.Count - 1)));
            f.RegisterValueChangedCallback(e => { if (f.index >= 0) changed(f.index); }); parent.Add(f); return f;
        }
        static void OpenUrl(string url) { if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https") Application.OpenURL(url); }
        static void OpenSaveFolder() { Directory.CreateDirectory(LocalStore.Folder); Application.OpenURL(new Uri(LocalStore.Folder).AbsoluteUri); }
        void Title(string kicker, string title, string subtitle) { Text(content, kicker, "eyebrow"); Text(content, title, "title"); Text(content, subtitle, "subtitle"); }
        void Armies()
        {
            Title("MUSTER YOUR FORCES", "Your armies", "Build a roster. Know your units. Bring everything to the table.");
            var toolbar = Box(content, "toolbar");
            Button(toolbar, "+ New army", NewArmy, "primary");
            if (data.armies.Count > 0)
                Choose(toolbar, "Army", data.armies.Select(x => x.name).ToList(), Math.Max(0, data.armies.IndexOf(army)), i => { army = data.armies[i]; Shell(); });
            if (army == null) { Text(content, "Your next campaign starts here. Create an army to choose a faction and add units.", "empty"); return; }
            var summary = Box(content, "card");
            var summaryTop = Box(summary, "row");
            Text(summaryTop, army.factionName.ToUpperInvariant(), "eyebrow");
            Text(summaryTop, $"{army.Points} / {army.limit} PTS", army.Points > army.limit ? "warning" : "points");
            var bar = new ProgressBar { lowValue = 0, highValue = army.limit, value = army.Points, title = $"{Math.Max(0, army.limit - army.Points)} points remaining" }; summary.Add(bar);
            Input(summary, "Army name", army.name, value => { army.name = string.IsNullOrWhiteSpace(value) ? "Untitled army" : value; Persist(); });
            Choose(summary, "Battle size", new List<string> { "Incursion · 1,000 pts", "Strike Force · 2,000 pts" }, army.limit <= 1000 ? 0 : 1, i => { army.limit = i == 0 ? 1000 : 2000; Persist(); Shell(); });
            var actions = Box(summary, "toolbar");
            Button(actions, "Start battle with this army", StartBattle, "primary");
            Button(actions, "Detachments", Detachments);
            Button(actions, "Export roster", () => { try { Notify("Exported JSON and text: " + LocalStore.ExportArmy(army)); } catch (Exception e) { Notify(e.Message); } });
            Button(actions, "Duplicate", () => { var copy = LocalStore.Copy(army); copy.id = Guid.NewGuid().ToString("N"); copy.name += " (copy)"; data.armies.Add(copy); army = copy; Persist(); Shell(); });
            Button(actions, "Delete", () => Confirm("Delete " + army.name + "?", "This removes this saved roster. An existing battle remains available.", () => { data.armies.Remove(army); army = data.armies.FirstOrDefault(); Persist(); Shell(); }), "danger");
            Text(summary, string.Join("  /  ", army.detachments.Select(x => x.name + " · " + x.points + " DP")), "muted");
            var issues = ArmyRules.Validate(army);
            Text(summary, issues.Count == 0 ? "Basic checks passed" : string.Join("\n", issues), issues.Count == 0 ? "success" : "warning");
            Text(summary, "Basic checks only. Review chapter restrictions, allies, wargear quantities, special pricing and faction exceptions against the source rules.", "muted");
            var columns = Box(content, "columns");
            var roster = Box(columns, "column");
            Text(roster, $"ROSTER  /  {army.units.Count} UNITS", "section-title");
            if (army.units.Count == 0) Text(roster, "Add units from the catalogue to assemble your army.", "empty");
            foreach (var entry in army.units)
            {
                var card = Box(roster);
                var row = Box(card, "row"); Text(row, entry.sheet.name, "card-title"); Text(row, army.Cost(entry) + " pts", "points");
                Text(card, $"{entry.Size.models} models" + (entry.warlord ? "  •  WARLORD" : "") + (entry.enhancement != null ? "  •  " + entry.enhancement.name : ""), "muted");
                var buttons = Box(card, "toolbar");
                Button(buttons, "Configure", () => Configure(entry));
                Button(buttons, "Datasheet", () => Sheet(entry.sheet, entry.loadouts));
                Button(buttons, "Remove", () => { army.units.Remove(entry); foreach (var u in army.units.Where(x => x.attachedTo == entry.id)) u.attachedTo = ""; Persist(); Shell(); });
            }
            var catalogPanel = Box(columns, "column");
            Text(catalogPanel, "ADD TO YOUR ARMY", "section-title");
            var filter = new TextField { value = search }; filter.AddToClassList("search"); catalogPanel.Add(filter);
            filter.tooltip = "Search units by name or keyword";
            if (catalogueScroll == null) catalogueScroll = new ScrollView(ScrollViewMode.Vertical) { name = "unit-catalogue" };
            var results = catalogueScroll; results.RemoveFromHierarchy(); results.AddToClassList("catalog-results"); catalogPanel.Add(results);
            void RefreshResults()
            {
                results.Clear();
                var matches = catalog.units.Where(x => x.factionId == army.factionId && !x.virtualUnit && x.sizes.Count > 0 && x.profiles.Count > 0 && Matches(x, search)).ToList();
                Text(results, matches.Count + " available units", "muted");
                foreach (var unit in matches.Take(60))
                {
                    var card = Box(results); Text(card, unit.name, "card-title");
                    Text(card, $"From {unit.sizes.Min(x => x.points)} pts  •  {unit.source}", "muted");
                    var row = Box(card, "toolbar"); Button(row, "+ Add", () => AddUnit(unit), "primary"); Button(row, "View sheet", () => Sheet(unit));
                }
                if (matches.Count > 60) Text(results, "Showing the first 60. Search to narrow your selection.", "muted");
            }
            filter.RegisterValueChangedCallback(e => { search = e.newValue; RefreshResults(); }); RefreshResults();
        }
        bool Matches(UnitDefinition unit, string query) => string.IsNullOrWhiteSpace(query) || unit.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || unit.keywords.Any(x => x.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        void AddUnit(UnitDefinition sheet)
        {
            var copy = LocalStore.Copy(sheet);
            army.units.Add(new RosterUnit { sheet = copy, paidOptions = copy.paidOptions.Select(x => new PaidOption { name = x.name, points = x.points }).ToList() });
            Persist(); Shell();
        }
        void NewArmy()
        {
            var body = Modal("Create an army"); string name = "New army"; int faction = 0, limit = 2000;
            Input(body, "Name", name, x => name = x);
            Choose(body, "Faction", catalog.factions.Select(x => x.name).ToList(), faction, i => faction = i);
            Choose(body, "Battle size", new List<string> { "1,000 points", "2,000 points" }, 1, i => limit = i == 0 ? 1000 : 2000);
            Button(body, "Create army", () => { var f = catalog.factions[faction]; army = new Army { name = string.IsNullOrWhiteSpace(name) ? f.name + " army" : name, factionId = f.id, factionName = f.name, limit = limit, dataVersion = catalog.updated }; data.armies.Add(army); Persist(); Shell(); }, "primary");
        }
        void Configure(RosterUnit unit)
        {
            var body = Modal(unit.sheet.name);
            var sizes = unit.sheet.sizes.Select((size, i) => new { size, i }).GroupBy(x => x.size.models).Select(x => x.First()).ToList();
            VisualElement loadoutEditor = null;
            Choose(body, "Unit size", sizes.Select(x => x.size.label).ToList(), sizes.FindIndex(x => x.size.models == unit.Size.models), i => { unit.sizeIndex = sizes[i].i; LoadoutRules.Resize(unit); if (loadoutEditor != null) DrawLoadoutEditor(loadoutEditor, unit); Persist(); });
            var warlord = new Toggle("Warlord") { value = unit.warlord }; warlord.SetEnabled(unit.sheet.Has("Character")); body.Add(warlord);
            warlord.RegisterValueChangedCallback(e => { if (e.newValue) foreach (var u in army.units) u.warlord = false; unit.warlord = e.newValue; Persist(); });
            var enhancements = catalog.enhancements.Where(x => army.detachments.Any(d => d.id == x.detachmentId) && unit.sheet.enhancementIds.Contains(x.id)).ToList();
            var enhancementNames = new List<string> { "None" }; enhancementNames.AddRange(enhancements.Select(x => x.name + " · " + x.points + " pts"));
            var selectedEnhancement = unit.enhancement;
            if (selectedEnhancement != null && enhancements.All(x => x.id != selectedEnhancement.id)) { enhancements.Add(selectedEnhancement); enhancementNames.Add(selectedEnhancement.name + " (review detachment)"); }
            var enhancementRule = new Label(selectedEnhancement?.text ?? "") { enableRichText = false }; enhancementRule.AddToClassList("muted");
            Choose(body, "Enhancement", enhancementNames, selectedEnhancement == null ? 0 : enhancements.FindIndex(x => x.id == selectedEnhancement.id) + 1, i => { unit.enhancement = i == 0 ? null : LocalStore.Copy(enhancements[i - 1]); enhancementRule.text = unit.enhancement?.text ?? ""; Persist(); });
            body.Add(enhancementRule);
            var targets = army.units.Where(x => x.id != unit.id && unit.sheet.bodyguards.Contains(x.sheet.id)).ToList();
            if (targets.Count > 0 || unit.sheet.bodyguards.Count > 0)
            {
                var names = new List<string> { "Not attached" }; names.AddRange(targets.Select((x, i) => (i + 1) + ". " + x.sheet.name));
                Choose(body, "Bodyguard", names, targets.FindIndex(x => x.id == unit.attachedTo) + 1, i => { unit.attachedTo = i == 0 ? "" : targets[i - 1].id; Persist(); });
                Text(body, unit.sheet.leaderNotes, "muted");
            }
            Text(body, "Wargear", "section-title"); Text(body, unit.sheet.loadout); Text(body, unit.sheet.options);
            foreach (var paid in unit.paidOptions)
                Integer(body, paid.name + " · " + paid.points + " pts each", paid.quantity, 0, unit.Size.models * 10, n => { paid.quantity = n; Persist(); });
            loadoutEditor = Box(body, "loadout-editor"); DrawLoadoutEditor(loadoutEditor, unit);
            Input(body, "Unit notes", unit.notes, value => { unit.notes = value; Persist(); }, true);
            Button(body, "Done", () => { CloseModal(); Shell(); }, "primary");
        }
        void Detachments()
        {
            var body = Modal("Choose detachments");
            Text(body, "Select detachments for your army. DP budgets are checked on the roster. Chapter-specific DP overrides require source review.", "muted");
            foreach (var d in catalog.detachments.Where(x => x.factionId == army.factionId && string.IsNullOrEmpty(x.type)))
            {
                var card = Box(body); var toggle = new Toggle(d.name + " · " + d.points + " DP") { value = army.detachments.Any(x => x.id == d.id) }; card.Add(toggle);
                toggle.RegisterValueChangedCallback(e => { army.detachments.RemoveAll(x => x.id == d.id); if (e.newValue) army.detachments.Add(LocalStore.Copy(d)); Persist(); });
                Text(card, d.disposition, "eyebrow"); foreach (var rule in d.rules) { Text(card, rule.name, "card-title"); Text(card, rule.text); }
            }
            Button(body, "Done", () => { CloseModal(); Shell(); }, "primary");
        }
        void Library()
        {
            Title("THE REFERENCE LIBRARY", "Datasheets", "Every imported faction, ready offline. Open a unit to see its profiles, weapons and abilities.");
            Choose(content, "Faction", catalog.factions.Select(x => x.name).ToList(), catalog.factions.FindIndex(x => x.id == factionId), i => { factionId = catalog.factions[i].id; Shell(); });
            var filter = new TextField("Search units") { value = search }; content.Add(filter);
            var results = Box(content, "grid");
            void Refresh()
            {
                results.Clear(); var matches = catalog.units.Where(x => x.factionId == factionId && Matches(x, search)).ToList();
                foreach (var unit in matches.Take(90)) { var card = Box(results); Text(card, unit.name, "card-title"); Text(card, unit.source, "muted"); Button(card, "Open datasheet", () => Sheet(unit)); }
                if (matches.Count == 0) Text(results, "No units match your search.", "empty");
                if (matches.Count > 90) Text(results, "Showing 90 results. Refine your search to see more.", "muted");
            }
            filter.RegisterValueChangedCallback(e => { search = e.newValue; Refresh(); }); Refresh();
        }
        void Sheet(UnitDefinition unit, List<ModelLoadout> loadouts = null)
        {
            var body = Modal(unit.name);
            Text(body, unit.source + " · source edition " + unit.sourceEdition, "muted");
            Text(body, string.Join("  •  ", unit.keywords.Distinct()), "eyebrow");
            foreach (var p in unit.profiles)
            {
                Text(body, p.name, "section-title"); var stats = Box(body, "stats");
                foreach (var pair in new[] { ("M", p.movement), ("T", p.toughness), ("SV", p.save), ("INV", p.invulnerable), ("W", p.wounds.ToString()), ("LD", p.leadership), ("OC", p.control) })
                { var stat = Box(stats, "stat"); Text(stat, pair.Item1, "eyebrow"); Text(stat, string.IsNullOrEmpty(pair.Item2) ? "—" : pair.Item2, "stat-value"); }
            }
            DrawEquippedWeapons(body, unit, loadouts);
            foreach (var rule in unit.abilities) { Text(body, rule.name, "section-title"); Text(body, rule.text); }
            foreach (var pair in new[] { ("Composition", unit.composition), ("Default loadout", unit.loadout), ("Wargear options", unit.options), ("Transport", unit.transport), ("Damaged", unit.damaged), ("Leader / support", unit.leaderNotes) })
                if (!string.IsNullOrWhiteSpace(pair.Item2)) { Text(body, pair.Item1, "section-title"); Text(body, pair.Item2); }
            Text(body, "Points", "section-title");
            foreach (var size in unit.sizes.GroupBy(x => $"{x.label} · {x.points} pts · copies {x.from}–{(x.to == 999 ? "onward" : x.to.ToString())}").Select(x => x.Key)) Text(body, size);
            foreach (var option in unit.paidOptions) Text(body, option.name + " · " + option.points + " pts each");
            Button(body, "View source on Wahapedia", () => OpenUrl(unit.link));
        }
        VisualElement Modal(string title)
        {
            CloseModal(); overlay = Box(root, "overlay"); var panel = Box(overlay, "modal"); var head = Box(panel, "row");
            Text(head, title, "modal-title"); Button(head, "Close", () => { CloseModal(); Shell(); });
            var scroll = new ScrollView(); scroll.AddToClassList("modal-body"); panel.Add(scroll); return scroll;
        }
        void CloseModal() { overlay?.RemoveFromHierarchy(); overlay = null; }
        void Confirm(string title, string description, Action action)
        { var body = Modal(title); Text(body, description); Button(body, "Confirm", () => { CloseModal(); action(); }, "primary"); Button(body, "Cancel", CloseModal); }
        void StartBattle()
        {
            if (army == null || army.units.Count == 0) { Notify("Add units to an army before starting a battle."); return; }
            BattleSetupDialog(true);
        }
        void ChangeBattle(Action action)
        { battleUndo.Push(JsonUtility.ToJson(data.battle)); action(); Persist(); Shell(); }
        void BattleView()
        {
            Title("AT THE TABLE", "Battle tracker", "Track wounds per model, command points and victory points. Changes save automatically.");
            var battle = data.battle;
            if (battle == null || !battle.active)
            {
                Text(content, "Select a saved army in Armies, then start your battle.", "empty");
                Button(content, "Go to armies", () => { page = "Armies"; Shell(); }); return;
            }
            var top = Box(content); Text(top, battle.armyName, "card-title");
            if (battle.setup != null) Text(top, "Your primary: " + battle.setup.playerPrimary + "\nOpponent primary: " + battle.setup.opponentPrimary + "\nDeployment: " + battle.setup.deployment, "muted");
            Text(top, $"ROUND {battle.round}  /  {(battle.turn == 0 ? battle.player.name : battle.opponent.name).ToUpperInvariant()}  /  {Battle.Phases[battle.phase].ToUpperInvariant()}", "points");
            var actions = Box(top, "toolbar"); Button(actions, "Next phase", () => ChangeBattle(battle.NextPhase), "primary");
            Button(actions, "Draw secondaries", () => { secondaryPlayer = battle.turn; page = "Secondaries"; Shell(); });
            Button(actions, "Mission & deployment", () => BattleSetupDialog(false));
            var undo = Button(actions, "Undo", () => { if (battleUndo.Count == 0) return; data.battle = JsonUtility.FromJson<Battle>(battleUndo.Pop()); Persist(); Shell(); }); undo.SetEnabled(battleUndo.Count > 0);
            Button(actions, "End battle", () => Confirm("End battle?", "The army list is kept. Start a new battle to reset wounds and scores.", () => ChangeBattle(() => battle.active = false)), "danger");
            Integer(top, "Battle round", battle.round, 1, 99, value => ChangeBattle(() => battle.round = value));
            Choose(top, "Active player", new List<string> { "You", "Opponent" }, battle.turn, value => ChangeBattle(() => battle.turn = value));
            Choose(top, "Phase", Battle.Phases.ToList(), battle.phase, value => ChangeBattle(() => battle.phase = value));
            Text(top, "CP and mission scores are manual. Advancing a phase never awards points automatically.", "muted");
            var scores = Box(content, "columns"); ScorePanel(scores, battle.player); ScorePanel(scores, battle.opponent);
            foreach (var unit in battle.units)
            {
                var card = Box(content); var row = Box(card, "row"); Text(row, unit.name, "card-title");
                Text(row, $"{unit.models.Count(x => x.wounds > 0)} / {unit.models.Count} models  •  {unit.models.Sum(x => x.wounds)} W", "points");
                var controls = Box(card, "toolbar"); Button(controls, "Datasheet", () => Sheet(unit.sheet, unit.loadouts));
                Button(controls, "Model profiles", () => ModelProfiles(unit));
                var shock = new Toggle("Battle-shocked") { value = unit.battleShocked }; controls.Add(shock); shock.RegisterValueChangedCallback(e => ChangeBattle(() => unit.battleShocked = e.newValue));
                if (!string.IsNullOrEmpty(unit.notes)) Text(card, unit.notes, "muted");
                var models = Box(card, "model-grid");
                foreach (var model in unit.models)
                {
                    var box = Box(models, "model"); box.EnableInClassList("destroyed", model.wounds == 0); Text(box, model.name, "muted");
                    var counter = Box(box, "counter");
                    Button(counter, "−", () => ChangeBattle(() => model.Change(-1)));
                    Text(counter, model.wounds + " / " + model.maxWounds, "stat-value");
                    Button(counter, "+", () => ChangeBattle(() => model.Change(1)));
                    Text(box, model.wounds == 0 ? "DESTROYED" : "WOUNDS", "eyebrow");
                }
            }
        }
        void ScorePanel(VisualElement parent, Score score)
        {
            var card = Box(parent, "score-card"); Input(card, "Player", score.name, x => { score.name = x; Persist(); }); Text(card, score.Total + " VP", "title");
            foreach (var kind in new[] { "Primary VP", "Secondary VP", "Command points" })
            {
                int Value() => kind == "Primary VP" ? score.primary : kind == "Secondary VP" ? score.secondary : score.command;
                void Set(int n) { if (kind == "Primary VP") score.primary = n; else if (kind == "Secondary VP") score.secondary = n; else score.command = n; }
                Integer(card, kind, Value(), 0, 999, n => ChangeBattle(() => Set(n)));
                var row = Box(card, "toolbar"); foreach (int delta in new[] { -1, 1, 5 }) Button(row, delta > 0 ? "+" + delta : delta.ToString(), () => ChangeBattle(() => Set(Mathf.Clamp(Value() + delta, 0, 999))));
            }
        }
        void ModelProfiles(BattleUnit unit)
        {
            var body = Modal("Model profiles · " + unit.name);
            Text(body, "For mixed units, assign each model its profile. Changing a profile resets that model to full wounds. Check the datasheet's composition.", "warning"); Text(body, unit.sheet.composition);
            foreach (var model in unit.models)
            {
                var names = unit.sheet.profiles.Select(x => x.name + " · " + x.wounds + " W").ToList();
                Choose(body, model.name, names, Math.Max(0, unit.sheet.profiles.FindIndex(x => model.name.StartsWith(x.name, StringComparison.Ordinal))), i => {
                    battleUndo.Push(JsonUtility.ToJson(data.battle)); var p = unit.sheet.profiles[i]; model.name = p.name + " " + (unit.models.IndexOf(model) + 1); model.maxWounds = p.wounds; model.wounds = p.wounds; Persist();
                });
            }
        }
        void DataView()
        {
            Title("YOUR FIELD LIBRARY", "Data & storage", "A bundled catalogue for offline play, with on-demand updates from Wahapedia.");
            var reading = Box(content); Text(reading, "Reading preferences", "card-title");
            var textSize = new Toggle("Extra-large text") { value = data.extraLargeText }; reading.Add(textSize);
            textSize.RegisterValueChangedCallback(e => { data.extraLargeText = e.newValue; Persist(); Shell(); });
            var card = Box(content); Text(card, $"{catalog.factions.Count} factions  /  {catalog.units.Count} datasheets", "card-title");
            Text(card, "11th edition · source export " + catalog.updated + " (UTC+3)");
            Text(card, "Updates replace the catalogue after every table validates. Saved armies and battles keep their existing datasheet and points snapshots; create a new roster to use updated data.", "muted");
            var update = Button(card, syncing ? "Updating…" : "Update from Wahapedia", () => { if (!syncing) StartCoroutine(Sync()); }, "primary"); update.SetEnabled(!syncing);
            Button(card, "Export documentation", () => OpenUrl(WahapediaImport.BaseUrl + "the-rules/data-export/"));
            Button(card, "Core rules", () => OpenUrl(WahapediaImport.BaseUrl + "the-rules/core-rules/"));
            var storage = Box(content); Text(storage, "Local saves & exports", "card-title"); Text(storage, LocalStore.Folder, "muted");
            Button(storage, "Open save folder", OpenSaveFolder);
            string path = ""; Input(storage, "Import roster JSON path", path, x => path = x);
            Button(storage, "Import army", () => { try { army = LocalStore.ImportArmy(path.Trim().Trim('"')); data.armies.Add(army); Persist(); page = "Armies"; Shell(); } catch (Exception e) { Notify("Import failed: " + e.Message); } });
            Text(content, "Army Forge is an unofficial fan project. Warhammer 40,000 belongs to Games Workshop. Data powered by Wahapedia. No purchased-book unlocks or account required.", "muted");
            Text(content, "Current scope: army lists, datasheets and manual battle tracking. Chapter-specific detachment prices, allied-faction exceptions and full wargear legality are not automated. Source exports may include legacy entries; check the source book shown on each sheet.", "warning");
        }
        void SecondaryView()
        {
            Title("TACTICAL OBJECTIVES", "Secondary missions", SecondaryMissions.DeckName);
            var battle = data.battle;
            if (battle == null || !battle.active) { Text(content, "Start a battle from your army to draw secondary missions.", "empty"); return; }
            if (battle.EnsureDecks()) Persist();
            Choose(content, "Player", new List<string> { "You · " + battle.player.name, "Opponent · " + battle.opponent.name }, secondaryPlayer, i => { secondaryPlayer = i; Shell(); });
            bool forOpponent = secondaryPlayer == 1;
            var deck = forOpponent ? battle.opponentDeck : battle.playerDeck;
            var score = forOpponent ? battle.opponent : battle.player;
            bool command = battle.turn == secondaryPlayer && battle.phase == 0;
            var summary = Box(content);
            Text(summary, $"{deck.drawPile.Count} in deck  /  {deck.active.Count} active  /  {deck.discarded.Count} discarded", "points");
            Text(summary, $"Round {battle.round} · {Battle.Phases[battle.phase]} · {score.secondary} secondary VP", "body");
            Text(summary, "Draw two at the start of your Command phase. Cards already in your hand remain active. Draws never recycle discarded cards automatically.", "muted");
            var actions = Box(summary, "toolbar");
            var draw = Button(actions, "Draw 2 secondaries", () => ChangeBattle(() => deck.DrawCommandCards(battle.round)), "primary");
            draw.SetEnabled(command && deck.lastDrawRound < battle.round && deck.drawPile.Count > 0);
            if (!command) Text(summary, "Drawing is available in this player's Command phase. Set the turn and phase on Battle.", "muted");
            else if (deck.lastDrawRound >= battle.round) Text(summary, "Command-phase cards already drawn this round.", "success");
            if (deck.drawPile.Count == 0) Text(summary, "Your deck is exhausted. There are no cards left to draw.", "warning");
            var undo = Button(actions, "Undo last battle change", () => { if (battleUndo.Count == 0) return; data.battle = JsonUtility.FromJson<Battle>(battleUndo.Pop()); Persist(); Shell(); }); undo.SetEnabled(battleUndo.Count > 0);
            Button(actions, "Back to battle", () => { page = "Battle"; Shell(); });
            Button(actions, "Full mission rules", () => OpenUrl(SecondaryMissions.SourceUrl));
            Text(summary, "Card reminders below are abbreviated. Check the full card for timing, exclusions, actions and redraw conditions. Enter earned VP manually; check mission scoring caps. Discards do not award CP automatically.", "muted");
            if (deck.active.Count == 0) Text(content, "No active cards. Draw your hand during your Command phase.", "empty");
            foreach (var id in deck.active.ToList())
            {
                var mission = SecondaryMissions.Find(id);
                var card = Box(content, "mission-card");
                Text(card, mission.name, "card-title"); Text(card, mission.cue, "body");
                var points = 5; Integer(card, "Earned VP", points, 0, 5, n => points = n);
                var buttons = Box(card, "toolbar");
                Button(buttons, "Score & complete", () => ChangeBattle(() => { if (deck.Discard(id)) score.secondary = Mathf.Clamp(score.secondary + points, 0, 999); }), "primary");
                Button(buttons, "Discard", () => ChangeBattle(() => deck.Discard(id)));
                var replace = Button(buttons, "Replace · 1 CP", () => ChangeBattle(() => battle.ReplaceSecondary(forOpponent, id)));
                replace.SetEnabled(command && !deck.commandRedrawUsed && score.command > 0 && deck.drawPile.Count > 0);
                replace.tooltip = "Once per battle, at the end of your Command phase.";
                if (!string.IsNullOrEmpty(mission.redraw))
                    Button(buttons, "Card-rule redraw…", () => Confirm("Use this card's redraw rule?", "Check that the printed When Drawn condition applies. This draws a replacement first, then " + (mission.redraw == "shuffle" ? "shuffles the current card back into the draw pile." : "discards the current card.") + " It does not cost CP.", () => ChangeBattle(() => deck.Redraw(id, mission.redraw == "shuffle")))).SetEnabled(deck.drawPile.Count > 0);
            }
            var discards = new Foldout { text = "Discard pile (" + deck.discarded.Count + ")", value = false }; content.Add(discards);
            foreach (var id in deck.discarded) Text(discards, SecondaryMissions.Find(id).name);
            var reshuffle = Button(discards, "Reshuffle discarded cards…", () => Confirm("Reshuffle the discard pile?", "This is a manual deck correction, not a standard tactical draw. Active cards stay in your hand. The discarded cards return to the draw pile.", () => ChangeBattle(() => deck.ReshuffleDiscarded())));
            reshuffle.SetEnabled(deck.discarded.Count > 0);
        }
        IEnumerator Sync()
        {
            syncing = true; var tables = new Dictionary<string, string>(); string error = null;
            // Read the export marker before and after to reject a mid-download source update.
            foreach (var name in new[] { "Last_update" }.Concat(WahapediaImport.Files).Concat(new[] { "Last_update" }))
            {
                Notify("Downloading " + name.Replace('_', ' ') + "…");
                using (var request = UnityWebRequest.Get(WahapediaImport.BaseUrl + name + ".csv"))
                {
                    request.timeout = 35;
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success) { error = request.error; break; }
                    if (name == "Last_update" && tables.TryGetValue(name, out var before) && before != request.downloadHandler.text) { error = "The source changed during download. Try again."; break; }
                    tables[name] = request.downloadHandler.text;
                }
            }
            if (error == null)
            {
                try { var next = WahapediaImport.Build(name => tables[name]); LocalStore.Save("catalog.json", next); catalog = next; }
                catch (Exception e) { error = e.Message; }
            }
            syncing = false; Shell(); Notify(error == null ? "Catalogue updated. Existing armies kept their snapshots." : "Update failed; previous catalogue kept. " + error);
        }
    }
}
