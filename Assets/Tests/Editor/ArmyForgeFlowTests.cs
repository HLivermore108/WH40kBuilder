using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ArmyForge.Tests
{
    public class ArmyForgeFlowTests
    {
        static void Click(VisualElement root, string label)
        {
            var button = root.Query<Button>().ToList().First(x => x.text == label);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }

        [UnityTest] public IEnumerator ArmyToBattleFlow()
        {
            // Batch-mode storage is isolated from a person's actual armies.
            if (!Application.isBatchMode) Assert.Ignore("Run this workflow test in batch mode to isolate saved data.");
            yield return new EnterPlayMode();
            yield return null;
            var app = Object.FindFirstObjectByType<ArmyForgeApp>();
            Assert.That(app, Is.Not.Null);
            var root = app.GetComponent<UIDocument>().rootVisualElement;
            root.style.width = 1440; root.style.height = 1000;
            new GameObject("Preview camera").AddComponent<Camera>();
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var layoutTarget = new RenderTexture(1440, 1000, 24); layoutTarget.Create();
                app.GetComponent<UIDocument>().panelSettings.targetTexture = layoutTarget;
            }
            Assert.That(root.Query<Button>().ToList().Any(x => x.text == "+ New army"), Is.True);
            Click(root, "+ New army");
            yield return null;
            root.Query<DropdownField>().ToList().Last(x => x.label == "Faction").value = "Orks";
            Click(root, "Create army");
            yield return null;
            Click(root, "+ Add");
            yield return null;
            Click(root, "Configure");
            yield return null;
            root.Query<Toggle>().ToList().First(x => x.label == "Choose weapons for this model").value = true;
            root.Query<IntegerField>().ToList().First(x => x.label.StartsWith("Quantity · ")).value = 1;
            for (int frame = 0; frame < 4; frame++) yield return null;
            var abilityButton = root.Query<Button>().ToList().First(x => x.ClassListContains("ability-tag"));
            Click(root, abilityButton.text);
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(root.Q("ability-popup"), Is.Not.Null);
            Assert.That(root.Query<Toggle>().ToList().Any(x => x.label == "Choose weapons for this model"), Is.True, "Popup retains the loadout editor underneath.");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "../Logs"));
                Capture(app.GetComponent<UIDocument>().panelSettings.targetTexture, Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/armyforge-ability.png")));
            }
            Click(root, "Back to weapons");
            Assert.That(root.Q("ability-popup"), Is.Null);
            Click(root, "Done");
            for (int frame = 0; frame < 4; frame++) yield return null;
            var main = root.Q<ScrollView>("page-scroll");
            var catalogue = root.Q<ScrollView>("unit-catalogue");
            main.scrollOffset = new Vector2(0, 180); catalogue.scrollOffset = new Vector2(0, 240);
            for (int frame = 0; frame < 4; frame++) yield return null;
            float mainOffset = main.scrollOffset.y, catalogueOffset = catalogue.scrollOffset.y;
            Debug.Log($"SCROLL LAYOUT: root={root.layout}, main={main.layout}, viewport={main.contentViewport.layout}, content={main.contentContainer.layout}, range={main.verticalScroller.highValue}, catalogue={catalogue.layout}, catalogueRange={catalogue.verticalScroller.highValue}, GPU={SystemInfo.graphicsDeviceType}");
            Assert.That(mainOffset, Is.GreaterThan(0), "Main page must have scrollable content."); Assert.That(catalogueOffset, Is.GreaterThan(0), "Catalogue must have scrollable content.");
            Click(root, "+ Add");
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(root.Q<ScrollView>("page-scroll").scrollOffset.y, Is.EqualTo(mainOffset).Within(2), "Adding a unit must preserve page position.");
            Assert.That(root.Q<ScrollView>("unit-catalogue").scrollOffset.y, Is.EqualTo(catalogueOffset).Within(2), "Adding a unit must preserve catalogue position.");
            Assert.That(LocalStore.Load<SaveData>("armies.json").armies.Last().units.Count, Is.EqualTo(2));
            Click(root, "Start battle with this army");
            yield return null;
            var mission = root.Query<DropdownField>().ToList().First(x => x.label == "Your primary");
            var oldPrimary = mission.value; Click(root, "Shuffle your primary");
            Assert.That(mission.value, Is.Not.EqualTo(oldPrimary));
            mission.value = "Sabotage";
            root.Query<DropdownField>().ToList().First(x => x.label == "Deployment zones").value = "Search and Destroy";
            for (int frame = 0; frame < 4; frame++) yield return null;
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(app.GetComponent<UIDocument>().panelSettings.targetTexture, Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/armyforge-setup.png")));
            Click(root, "Confirm");
            for (int frame = 0; frame < 4; frame++) yield return null;
            var initialBattle = LocalStore.Load<SaveData>("armies.json").battle;
            Assert.That(initialBattle.setup.playerPrimary, Is.EqualTo("Sabotage"));
            Assert.That(initialBattle.setup.deployment, Is.EqualTo("Search and Destroy"));
            Assert.That(initialBattle.units[0].loadouts[0].weapons.Single().quantity, Is.EqualTo(1));
            main = root.Q<ScrollView>("page-scroll"); main.scrollOffset = new Vector2(0, 300);
            for (int frame = 0; frame < 4; frame++) yield return null; mainOffset = main.scrollOffset.y;
            Assert.That(mainOffset, Is.GreaterThan(0));
            int full = LocalStore.Load<SaveData>("armies.json").battle.units[0].models[0].wounds;
            Click(root, "−");
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(root.Q<ScrollView>("page-scroll").scrollOffset.y, Is.EqualTo(mainOffset).Within(2), "Wound changes must preserve position.");
            Assert.That(LocalStore.Load<SaveData>("armies.json").battle.units[0].models[0].wounds, Is.EqualTo(full - 1));
            Click(root, "Undo");
            yield return null;
            Assert.That(LocalStore.Load<SaveData>("armies.json").battle.units[0].models[0].wounds, Is.EqualTo(full));
            Click(root, "Next phase");
            yield return null;
            Assert.That(LocalStore.Load<SaveData>("armies.json").battle.phase, Is.EqualTo(1));
            Click(root, "+5");
            yield return null;
            Assert.That(LocalStore.Load<SaveData>("armies.json").battle.player.primary, Is.EqualTo(5));
            var export = LocalStore.ExportArmy(LocalStore.Load<SaveData>("armies.json").armies.Last());
            var imported = LocalStore.ImportArmy(export);
            Assert.That(imported.units.Count, Is.EqualTo(2));
            Assert.That(imported.units[0].loadouts[0].weapons.Single().quantity, Is.EqualTo(1));
            Assert.That(File.Exists(Path.ChangeExtension(export, ".txt")), Is.True);
            root.Query<DropdownField>().ToList().First(x => x.label == "Phase").value = "Command";
            yield return new WaitForSeconds(0.15f);
            Click(root, "Draw secondaries");
            yield return new WaitForSeconds(0.15f);
            Click(root, "Draw 2 secondaries");
            yield return new WaitForSeconds(0.2f);
            Assert.That(LocalStore.Load<SaveData>("armies.json").battle.playerDeck.active.Count, Is.EqualTo(2));
            Assert.That(LocalStore.Load<SaveData>("armies.json").battle.playerDeck.drawPile.Count, Is.EqualTo(16));
            Click(root, "Score & complete");
            yield return new WaitForSeconds(0.2f);
            Assert.That(LocalStore.Load<SaveData>("armies.json").battle.playerDeck.active.Count, Is.EqualTo(1));
            Assert.That(LocalStore.Load<SaveData>("armies.json").battle.player.secondary, Is.EqualTo(5));
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                string capture = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")); Directory.CreateDirectory(capture);
                var panel = app.GetComponent<UIDocument>().panelSettings;
                var previousTarget = panel.targetTexture; if (previousTarget != null) { previousTarget.Release(); Object.Destroy(previousTarget); }
                var target = new RenderTexture(1440, 1000, 24);
                target.Create(); panel.targetTexture = target;
                for (int frame = 0; frame < 6; frame++) yield return null;
                Capture(target, Path.Combine(capture, "armyforge-secondaries.png"));
                Click(root, "Battle");
                for (int frame = 0; frame < 6; frame++) yield return null;
                Capture(target, Path.Combine(capture, "armyforge-battle.png"));
                Click(root, "Armies");
                for (int frame = 0; frame < 6; frame++) yield return null;
                Capture(target, Path.Combine(capture, "armyforge-roster.png"));
                target.Release(); Object.Destroy(target); panel.targetTexture = null;
                for (int frame = 0; frame < 6; frame++) yield return null;
            }
            yield return new ExitPlayMode();
        }
        static void Capture(RenderTexture target, string path)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG()); Object.Destroy(image); RenderTexture.active = previous;
        }
    }
}
