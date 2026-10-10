using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmyForge
{
    // Original schematic, in battlefield inches. No card artwork is bundled.
    public class DeploymentPreview : VisualElement
    {
        string deployment;
        readonly VisualElement drawing;
        readonly Label dimensions;
        public DeploymentPreview(string name)
        {
            AddToClassList("deployment-preview");
            drawing = new VisualElement(); drawing.style.height = 390; drawing.style.flexShrink = 0; Add(drawing);
            dimensions = new Label(); dimensions.enableRichText = false; dimensions.AddToClassList("body"); Add(dimensions);
            drawing.generateVisualContent += Draw;
            SetLayout(name);
        }
        public void SetLayout(string name)
        {
            deployment = name;
            switch (name)
            {
                case "Dawn of War": dimensions.text = "12″ deep from opposite long edges."; break;
                case "Hammer and Anvil": dimensions.text = "18″ deep from opposite short edges."; break;
                case "Search and Destroy": dimensions.text = "Opposite quarters, excluding the central 9″ radius."; break;
                case "Tipping Point": dimensions.text = "Stepped short-edge zones: depths 20″ and 12″, changing at the board’s width midpoint."; break;
                case "Sweeping Engagement": dimensions.text = "Stepped long-edge zones: depths 14″ and 8″, changing at the board’s length midpoint."; break;
                default: dimensions.text = "Opposing triangles spanning a short edge and half the board’s length (30″)."; break;
            }
            drawing.MarkDirtyRepaint();
        }
        void Draw(MeshGenerationContext context)
        {
            float scale = Mathf.Min((drawing.contentRect.width - 24) / 44, (drawing.contentRect.height - 12) / 60);
            if (scale <= 0) return;
            var origin = new Vector2((drawing.contentRect.width - 44 * scale) / 2, 6);
            var pen = context.painter2D;
            void Polygon(List<Vector2> points, Color color)
            {
                pen.fillColor = color; pen.BeginPath(); pen.MoveTo(origin + points[0] * scale);
                for (int i = 1; i < points.Count; i++) pen.LineTo(origin + points[i] * scale);
                pen.ClosePath(); pen.Fill();
            }
            var board = new List<Vector2> { new Vector2(0, 0), new Vector2(44, 0), new Vector2(44, 60), new Vector2(0, 60) };
            Polygon(board, new Color(0.13f, 0.19f, 0.20f));
            List<Vector2> zone;
            switch (deployment)
            {
                case "Dawn of War": zone = Points(0,0, 12,0, 12,60, 0,60); break;
                case "Hammer and Anvil": zone = Points(0,0, 44,0, 44,18, 0,18); break;
                case "Tipping Point": zone = Points(0,0, 44,0, 44,12, 22,12, 22,20, 0,20); break;
                case "Sweeping Engagement": zone = Points(0,0, 14,0, 14,30, 8,30, 8,60, 0,60); break;
                case "Search and Destroy":
                    zone = Points(0,0, 22,0, 22,21);
                    for (int i = 1; i <= 32; i++) { float angle = (-90 - i * 90f / 32) * Mathf.Deg2Rad; zone.Add(new Vector2(22 + Mathf.Cos(angle) * 9, 30 + Mathf.Sin(angle) * 9)); }
                    zone.Add(new Vector2(0,30)); break;
                default: zone = Points(0,0, 44,0, 0,30); break;
            }
            Polygon(zone, new Color(0.63f, 0.24f, 0.23f));
            var opposite = new List<Vector2>(); foreach (var point in zone) opposite.Add(new Vector2(44 - point.x, 60 - point.y));
            Polygon(opposite, new Color(0.18f, 0.42f, 0.63f));
            pen.strokeColor = new Color(0.84f, 0.85f, 0.75f); pen.lineWidth = 2;
            pen.BeginPath(); pen.MoveTo(origin); foreach (var point in board) pen.LineTo(origin + point * scale); pen.ClosePath(); pen.Stroke();
            pen.lineWidth = 1; pen.strokeColor = new Color(0.84f, 0.85f, 0.75f, 0.30f);
            for (int x = 4; x < 44; x += 4) { pen.BeginPath(); pen.MoveTo(origin + new Vector2(x, 0) * scale); pen.LineTo(origin + new Vector2(x, 60) * scale); pen.Stroke(); }
            for (int y = 4; y < 60; y += 4) { pen.BeginPath(); pen.MoveTo(origin + new Vector2(0, y) * scale); pen.LineTo(origin + new Vector2(44, y) * scale); pen.Stroke(); }
        }
        static List<Vector2> Points(params float[] xy)
        { var result = new List<Vector2>(); for (int i = 0; i < xy.Length; i += 2) result.Add(new Vector2(xy[i], xy[i + 1])); return result; }
    }
}
