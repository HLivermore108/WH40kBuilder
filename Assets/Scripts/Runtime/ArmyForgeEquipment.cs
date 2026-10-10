using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmyForge
{
    public partial class ArmyForgeApp
    {
        void DrawLoadoutEditor(VisualElement parent, RosterUnit unit)
        {
            parent.Clear(); LoadoutRules.Resize(unit);
            Text(parent, "Model loadouts", "section-title");
            Text(parent, "Choose each model’s weapons and profile. Check the wargear options above for allowed replacements and quantities. Paid upgrades use the point controls above.", "muted");
            int selected = 0;
            var modelPicker = Choose(parent, "Model", Enumerable.Range(1, unit.Size.models).Select(x => "Model " + x).ToList(), 0, i => { selected = i; });
            var detail = Box(parent, "loadout-detail");
            void Render()
            {
                detail.Clear(); var model = unit.loadouts[selected];
                Choose(detail, "Model profile", unit.sheet.profiles.Select(x => x.name).ToList(), model.profileIndex, i => { model.profileIndex = i; Persist(); });
                var custom = new Toggle("Choose weapons for this model") { value = model.customised }; detail.Add(custom);
                custom.RegisterValueChangedCallback(e => { model.customised = e.newValue; Persist(); Render(); });
                if (!model.customised) Text(detail, "Using the datasheet’s default equipment. Enable the switch above to record a complete replacement loadout.", "muted");
                else
                {
                    Text(detail, "Set the number of each weapon this model carries. Alternative firing modes share one quantity.", "muted");
                    var selection = Text(detail, "", "success");
                    void Summary() => selection.text = "Selected: " + (model.weapons.Any(x => x.quantity > 0) ? string.Join(", ", model.weapons.Where(x => x.quantity > 0).Select(x => x.quantity + " × " + x.name)) : "No weapons yet");
                    Summary();
                    foreach (var group in unit.sheet.weapons.GroupBy(LoadoutRules.WeaponName))
                    {
                        string weaponName = group.Key;
                        var row = Box(detail); Text(row, weaponName, "card-title");
                        Integer(row, "Quantity · " + weaponName, model.weapons.FirstOrDefault(x => x.name == weaponName)?.quantity ?? 0, 0, 10, n => {
                            model.weapons.RemoveAll(x => x.name == weaponName);
                            if (n > 0) model.weapons.Add(new EquippedWeapon { name = weaponName, quantity = n });
                            Summary(); Persist();
                        });
                        foreach (var weapon in group)
                        {
                            if (group.Count() > 1) Text(row, weapon.name, "muted");
                            AbilityButtons(row, weapon, unit.sheet);
                        }
                    }
                }
                Input(detail, "Other equipment", model.equipment, value => { model.equipment = value; Persist(); });
                var copy = Button(detail, "Copy to matching models", () => {
                    for (int i = 0; i < unit.loadouts.Count; i++)
                        if (i != selected && unit.loadouts[i].profileIndex == model.profileIndex) unit.loadouts[i] = model.Copy();
                    Persist(); Notify("Loadout copied to models with the same profile.");
                });
                copy.SetEnabled(unit.loadouts.Count(x => x.profileIndex == model.profileIndex) > 1);
                Text(detail, "Copy replaces weapons and other equipment only on models with this profile.", "muted");
            }
            modelPicker.RegisterValueChangedCallback(_ => Render()); Render();
        }

        void DrawEquippedWeapons(VisualElement body, UnitDefinition sheet, List<ModelLoadout> loadouts)
        {
            if (loadouts != null && loadouts.Count > 0)
            {
                Text(body, "Equipped loadout", "section-title"); Text(body, LoadoutRules.Summary(sheet, loadouts));
            }
            Text(body, "Weapons · click an ability for help", "section-title");
            bool filter = loadouts != null && loadouts.Count > 0 && loadouts.All(x => x.customised);
            var list = new VisualElement();
            if (filter)
            {
                var all = new Toggle("Show all available weapons") { value = false }; body.Add(all);
                all.RegisterValueChangedCallback(e => Render(e.newValue));
            }
            else if (loadouts != null && loadouts.Count > 0) Text(body, "All profiles are shown because some models use their datasheet default equipment.", "muted");
            body.Add(list);
            void Render(bool all)
            {
                list.Clear();
                foreach (var weapon in sheet.weapons)
                {
                    string key = LoadoutRules.WeaponName(weapon);
                    int count = loadouts?.Where(x => x.customised).Sum(x => x.weapons.Where(w => w.name == key).Sum(w => w.quantity)) ?? 0;
                    if (filter && !all && count == 0) continue;
                    var card = Box(list); Text(card, weapon.name + (count > 0 ? " · " + count + " equipped" : ""), "card-title");
                    var stats = Box(card, "stats");
                    foreach (var pair in new[] { ("RANGE", weapon.range), ("A", weapon.attacks), ("BS/WS", weapon.skill), ("S", weapon.strength), ("AP", weapon.ap), ("D", weapon.damage) })
                    { var stat = Box(stats, "stat"); Text(stat, pair.Item1, "eyebrow"); Text(stat, pair.Item2, "stat-value"); }
                    AbilityButtons(card, weapon, sheet);
                }
                if (list.childCount == 0) Text(list, "No weapons selected. Enable ‘Show all available weapons’ to browse the datasheet.", "muted");
            }
            Render(false);
        }
        void AbilityButtons(VisualElement parent, Weapon weapon, UnitDefinition sheet)
        {
            var tags = Box(parent, "toolbar");
            foreach (string ability in WeaponAbilityHelp.Split(weapon.rules))
            {
                Button trigger = null;
                trigger = Button(tags, ability, () => {
                    var help = Box(root, "overlay"); help.name = "ability-popup";
                    var panel = Box(help, "modal"); panel.AddToClassList("ability-dialog");
                    Text(panel, ability, "modal-title");
                    var scroll = new ScrollView(); panel.Add(scroll);
                    Text(scroll, WeaponAbilityHelp.Explain(ability, sheet));
                    Text(scroll, "11th-edition reminder. Use the full rule for timing, restrictions and interactions.", "muted");
                    Button(scroll, "Full core rule", () => OpenUrl(WeaponAbilityHelp.Source));
                    Button(scroll, "Unit source", () => OpenUrl(sheet.link));
                    var close = Button(panel, "Back to weapons", () => { help.RemoveFromHierarchy(); trigger.Focus(); }, "primary");
                    close.Focus();
                    help.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) { help.RemoveFromHierarchy(); trigger.Focus(); e.StopPropagation(); } });
                }, "ability-tag");
            }
        }

        void BattleSetupDialog(bool starting)
        {
            var selection = starting || data.battle.setup == null ? new BattleSetup() : LocalStore.Copy(data.battle.setup);
            var body = Modal(starting ? "Set up battle" : "Mission & deployment");
            Text(body, "Chapter Approved 2026–27 · Choose missions for both players and a deployment zone layout.", "muted");
            Text(body, "Choose missions agreed with your opponent. Shuffle makes a casual random selection; it does not apply Force Disposition pairings.", "muted");
            var p = Choose(body, "Your primary", MissionChoices.Primaries.ToList(), Array.IndexOf(MissionChoices.Primaries, selection.playerPrimary), i => selection.playerPrimary = MissionChoices.Primaries[i]);
            Button(body, "Shuffle your primary", () => p.value = MissionChoices.Shuffle(MissionChoices.Primaries, p.value));
            var o = Choose(body, "Opponent primary", MissionChoices.Primaries.ToList(), Array.IndexOf(MissionChoices.Primaries, selection.opponentPrimary), i => selection.opponentPrimary = MissionChoices.Primaries[i]);
            Button(body, "Shuffle opponent primary", () => o.value = MissionChoices.Shuffle(MissionChoices.Primaries, o.value));
            var d = Choose(body, "Deployment zones", MissionChoices.Deployments.ToList(), Array.IndexOf(MissionChoices.Deployments, selection.deployment), i => selection.deployment = MissionChoices.Deployments[i]);
            var shuffle = Box(body, "toolbar");
            Button(shuffle, "Shuffle deployment", () => d.value = MissionChoices.Shuffle(MissionChoices.Deployments, d.value));
            Button(shuffle, "Shuffle all", () => {
                p.value = MissionChoices.Shuffle(MissionChoices.Primaries, p.value);
                o.value = MissionChoices.Shuffle(MissionChoices.Primaries, o.value);
                d.value = MissionChoices.Shuffle(MissionChoices.Deployments, d.value);
            });
            var board = new DeploymentPreview(selection.deployment); body.Add(board);
            d.RegisterValueChangedCallback(_ => board.SetLayout(selection.deployment));
            Text(body, "44″ × 60″ · Red: attacker · Blue: defender. Deployment zones only; terrain and objective placement use the source card.", "muted");
            Button(body, "Full primary and deployment rules", () => OpenUrl(MissionChoices.Source + "#Primary-Mission-deck"));
            if (starting)
            {
                if (data.battle?.active == true) Text(body, "Starting replaces your current battle.", "warning");
                var issues = ArmyRules.Validate(army);
                if (issues.Count > 0) Text(body, string.Join("\n", issues), "warning");
                Button(body, "Confirm", () => {
                    data.battle = Battle.Start(LocalStore.Copy(army)); data.battle.setup = selection;
                    battleUndo.Clear(); Persist(); CloseModal(); page = "Battle"; Shell();
                }, "primary");
            }
            else Button(body, "Save setup", () => { CloseModal(); ChangeBattle(() => data.battle.setup = selection); }, "primary");
            Button(body, "Cancel", CloseModal);
        }
    }
}
