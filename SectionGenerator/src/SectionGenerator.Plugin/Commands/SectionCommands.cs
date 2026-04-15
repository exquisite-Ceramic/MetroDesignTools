using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CoreSectionSettings = SectionGenerator.Core.Sections.SectionSettings;
using SectionGenerator.Infrastructure.Config;

[assembly: CommandClass(typeof(SectionGenerator.Commands))]

namespace SectionGenerator
{
    public class Commands
    {
        private const string ConfigFileName = "SectionGeneratorConfig.json";
        private readonly JsonConfigManager _config = new JsonConfigManager();
        private CoreSectionSettings _settings;

        public Commands()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            string configPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), ConfigFileName);
            _settings = _config.LoadOrCreate(configPath, new CoreSectionSettings());
        }

        private void SaveSettings()
        {
            string configPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), ConfigFileName);
            _config.Save(configPath, _settings);
        }

        [CommandMethod("GenSection")]
        public void GenerateSection()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptEntityOptions peo = new PromptEntityOptions("\n选择剖切线 (直线): ");
            peo.SetRejectMessage("\n必须选择直线!");
            peo.AddAllowedClass(typeof(Line), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Line sectionLine = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Line;
                if (sectionLine == null) return;

                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n选择与剖切线相交的辅助实体 (墙线、梁线等，可直接回车跳过): ";
                PromptSelectionResult psr = ed.GetSelection(pso);
                List<Curve> auxCurves = new List<Curve>();
                if (psr.Status == PromptStatus.OK)
                {
                    foreach (ObjectId id in psr.Value.GetObjectIds())
                    {
                        Curve curve = tr.GetObject(id, OpenMode.ForRead) as Curve;
                        if (curve != null) auxCurves.Add(curve);
                    }
                }

                Point3d startPt = sectionLine.StartPoint;
                Point3d endPt = sectionLine.EndPoint;
                double length = startPt.DistanceTo(endPt);
                Vector3d direction = (endPt - startPt).GetNormal();

                List<double> paramList = new List<double>();
                foreach (Curve curve in auxCurves)
                {
                    Point3dCollection intersections = new Point3dCollection();
                    sectionLine.IntersectWith(curve, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);
                    foreach (Point3d pt in intersections)
                    {
                        double param = startPt.GetParameterTo(pt, direction);
                        if (param >= 0 && param <= length)
                            paramList.Add(param);
                    }
                }
                paramList.Sort();
                paramList = paramList.Distinct().ToList();

                if (!ModifySettings(ed)) return;

                PromptPointResult ppr = ed.GetPoint("\n指定剖面图插入点: ");
                if (ppr.Status != PromptStatus.OK) return;
                Point3d insertPt = ppr.Value;

                using (DocumentLock docLock = doc.LockDocument())
                {
                    GenerateSectionDrawing(db, insertPt, length, direction, paramList);
                }

                tr.Commit();
                ed.WriteMessage("\n剖面图生成完成!");
            }
        }

        private bool ModifySettings(Editor ed)
        {
            ed.WriteMessage($"\n当前设置: 底板厚={_settings.BottomSlabThickness}mm, 顶板厚={_settings.TopSlabThickness}mm, " +
                            $"装修厚={_settings.FinishThickness}mm, 层高={_settings.FloorHeight}mm, " +
                            $"坡度={(_settings.HasSlope ? $"是 ({_settings.SlopeValue*1000}‰)" : "否")}");

            PromptKeywordOptions pko = new PromptKeywordOptions("\n是否修改设置? [是(Y)/否(N)]: ");
            pko.Keywords.Add("Y", "Y", "是");
            pko.Keywords.Add("N", "N", "否");
            pko.Keywords.Default = "N";
            PromptResult pr = ed.GetKeywords(pko);
            if (pr.Status != PromptStatus.OK || pr.StringResult == "N")
                return true;

            try
            {
                _settings.BottomSlabThickness = GetDoubleInput(ed, $"底板厚度 (mm) <{_settings.BottomSlabThickness}>: ", _settings.BottomSlabThickness);
                _settings.TopSlabThickness = GetDoubleInput(ed, $"顶板厚度 (mm) <{_settings.TopSlabThickness}>: ", _settings.TopSlabThickness);
                _settings.FinishThickness = GetDoubleInput(ed, $"装修面层厚度 (mm) <{_settings.FinishThickness}>: ", _settings.FinishThickness);
                _settings.FloorHeight = GetDoubleInput(ed, $"层高 (mm) <{_settings.FloorHeight}>: ", _settings.FloorHeight);
                _settings.HasSlope = GetBoolInput(ed, $"楼板是否有坡度? (是/否) <{(_settings.HasSlope ? "是" : "否")}>: ", _settings.HasSlope);
                if (_settings.HasSlope)
                {
                    _settings.SlopeValue = GetDoubleInput(ed, $"坡度值 (千分之) <{_settings.SlopeValue * 1000}>: ", _settings.SlopeValue * 1000) / 1000.0;
                }
                SaveSettings();
            }
            catch { return false; }
            return true;
        }

        private double GetDoubleInput(Editor ed, string message, double defaultValue)
        {
            PromptDoubleOptions pdo = new PromptDoubleOptions(message);
            pdo.DefaultValue = defaultValue;
            pdo.AllowNegative = false;
            PromptDoubleResult pdr = ed.GetDouble(pdo);
            return pdr.Status == PromptStatus.OK ? pdr.Value : defaultValue;
        }

        private bool GetBoolInput(Editor ed, string message, bool defaultValue)
        {
            PromptKeywordOptions pko = new PromptKeywordOptions(message);
            pko.Keywords.Add("是", "是", "是");
            pko.Keywords.Add("否", "否", "否");
            pko.Keywords.Default = defaultValue ? "是" : "否";
            PromptResult pr = ed.GetKeywords(pko);
            if (pr.Status == PromptStatus.OK)
                return pr.StringResult == "是";
            return defaultValue;
        }

        private void GenerateSectionDrawing(Database db, Point3d insertPt, double length, Vector3d direction, List<double> intersectionParams)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                double slopeDelta = _settings.HasSlope ? length * _settings.SlopeValue : 0.0;
                double bottomSlabBottom = -_settings.BottomSlabThickness;
                double bottomSlabTop = 0.0;
                double topSlabBottomStart = _settings.FloorHeight;
                double topSlabTopStart = topSlabBottomStart + _settings.TopSlabThickness;
                double topSlabBottomEnd = _settings.FloorHeight + slopeDelta;
                double topSlabTopEnd = topSlabBottomEnd + _settings.TopSlabThickness;
                double finishBottomStart = topSlabTopStart;
                double finishTopStart = finishBottomStart + _settings.FinishThickness;
                double finishBottomEnd = topSlabTopEnd;
                double finishTopEnd = finishBottomEnd + _settings.FinishThickness;

                double maxY = Math.Max(finishTopStart, finishTopEnd);
                double minY = bottomSlabBottom;

                DrawHatchBetweenLines(btr, insertPt,
                    new Point2d(0, bottomSlabBottom), new Point2d(length, bottomSlabBottom),
                    new Point2d(0, bottomSlabTop), new Point2d(length, bottomSlabTop),
                    "混凝土", tr);

                DrawHatchBetweenLines(btr, insertPt,
                    new Point2d(0, topSlabBottomStart), new Point2d(length, topSlabBottomEnd),
                    new Point2d(0, topSlabTopStart), new Point2d(length, topSlabTopEnd),
                    "混凝土", tr);

                DrawHatchBetweenLines(btr, insertPt,
                    new Point2d(0, finishBottomStart), new Point2d(length, finishBottomEnd),
                    new Point2d(0, finishTopStart), new Point2d(length, finishTopEnd),
                    "装修面层", tr);

                DrawLine(btr, insertPt, new Point2d(0, bottomSlabBottom), new Point2d(length, bottomSlabBottom));
                DrawLine(btr, insertPt, new Point2d(0, bottomSlabTop), new Point2d(length, bottomSlabTop));
                DrawLine(btr, insertPt, new Point2d(0, topSlabBottomStart), new Point2d(length, topSlabBottomEnd));
                DrawLine(btr, insertPt, new Point2d(0, topSlabTopStart), new Point2d(length, topSlabTopEnd));
                DrawLine(btr, insertPt, new Point2d(0, finishBottomStart), new Point2d(length, finishBottomEnd));
                DrawLine(btr, insertPt, new Point2d(0, finishTopStart), new Point2d(length, finishTopEnd));

                DrawLine(btr, insertPt, new Point2d(0, minY), new Point2d(0, maxY));
                DrawLine(btr, insertPt, new Point2d(length, minY), new Point2d(length, maxY));

                foreach (double param in intersectionParams)
                {
                    double x = param;
                    DrawLine(btr, insertPt, new Point2d(x, minY), new Point2d(x, maxY), 2);
                }

                AddDimension(btr, insertPt, new Point2d(length * 0.1, topSlabBottomStart), new Point2d(length * 0.1, bottomSlabTop), "层高", tr, db);
                if (_settings.HasSlope)
                {
                    AddText(btr, insertPt, new Point2d(length * 0.5, maxY + 200), $"坡度 {_settings.SlopeValue * 1000}‰", 150);
                }

                AddElevationMarker(btr, insertPt, new Point2d(0, bottomSlabTop), "±0.000");
                AddElevationMarker(btr, insertPt, new Point2d(length, topSlabBottomEnd), $"{_settings.FloorHeight + slopeDelta:F0}");

                tr.Commit();
            }
        }

        private void DrawHatchBetweenLines(BlockTableRecord btr, Point3d insertPt,
            Point2d p1, Point2d p2, Point2d p3, Point2d p4, string patternName, Transaction tr)
        {
            Line line1 = new Line(new Point3d(p1.X, p1.Y, 0), new Point3d(p2.X, p2.Y, 0));
            Line line2 = new Line(new Point3d(p2.X, p2.Y, 0), new Point3d(p4.X, p4.Y, 0));
            Line line3 = new Line(new Point3d(p4.X, p4.Y, 0), new Point3d(p3.X, p3.Y, 0));
            Line line4 = new Line(new Point3d(p3.X, p3.Y, 0), new Point3d(p1.X, p1.Y, 0));

            MoveEntity(line1, insertPt);
            MoveEntity(line2, insertPt);
            MoveEntity(line3, insertPt);
            MoveEntity(line4, insertPt);

            btr.AppendEntity(line1);
            tr.AddNewlyCreatedDBObject(line1, true);
            btr.AppendEntity(line2);
            tr.AddNewlyCreatedDBObject(line2, true);
            btr.AppendEntity(line3);
            tr.AddNewlyCreatedDBObject(line3, true);
            btr.AppendEntity(line4);
            tr.AddNewlyCreatedDBObject(line4, true);

            Hatch hatch = new Hatch();
            hatch.SetDatabaseDefaults();
            hatch.PatternScale = 10;
            hatch.SetHatchPattern(HatchPatternType.PreDefined, patternName == "混凝土" ? "ANSI31" : "AR-CONC");
            hatch.Associative = false;
            btr.AppendEntity(hatch);
            tr.AddNewlyCreatedDBObject(hatch, true);

            ObjectIdCollection boundaryIds = new ObjectIdCollection { line1.ObjectId, line2.ObjectId, line3.ObjectId, line4.ObjectId };
            hatch.AppendLoop(HatchLoopTypes.External, boundaryIds);
            hatch.EvaluateHatch(true);
        }

        private void DrawLine(BlockTableRecord btr, Point3d insertPt, Point2d start, Point2d end, int linetypeIndex = 0)
        {
            Line line = new Line(new Point3d(start.X, start.Y, 0), new Point3d(end.X, end.Y, 0));
            MoveEntity(line, insertPt);
            if (linetypeIndex == 2)
                line.Linetype = "DASHED";
            btr.AppendEntity(line);
            line.Dispose();
        }

        private void MoveEntity(Entity ent, Point3d insertPt)
        {
            ent.TransformBy(Matrix3d.Displacement(insertPt - Point3d.Origin));
        }

        private void AddDimension(BlockTableRecord btr, Point3d insertPt, Point2d pt1, Point2d pt2, string text, Transaction tr, Database db)
        {
            Point3d p1 = new Point3d(pt1.X, pt1.Y, 0);
            Point3d p2 = new Point3d(pt2.X, pt2.Y, 0);
            Point3d mid = new Point3d((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, 0);
            Vector3d offset = new Vector3d(-200, 0, 0);
            Point3d dimLinePt = mid + offset;

            AlignedDimension dim = new AlignedDimension(p1, p2, dimLinePt, text, db.Dimstyle);
            MoveEntity(dim, insertPt);
            btr.AppendEntity(dim);
            tr.AddNewlyCreatedDBObject(dim, true);
        }

        private void AddText(BlockTableRecord btr, Point3d insertPt, Point2d position, string content, double height)
        {
            MText mtext = new MText();
            mtext.Location = new Point3d(position.X, position.Y, 0);
            mtext.Contents = content;
            mtext.TextHeight = height;
            MoveEntity(mtext, insertPt);
            btr.AppendEntity(mtext);
            mtext.Dispose();
        }

        private void AddElevationMarker(BlockTableRecord btr, Point3d insertPt, Point2d pos, string label)
        {
            double size = 100;
            Point2d tip = new Point2d(pos.X, pos.Y);
            Point2d left = new Point2d(pos.X - size, pos.Y + size);
            Point2d right = new Point2d(pos.X + size, pos.Y + size);
            DrawLine(btr, insertPt, tip, left);
            DrawLine(btr, insertPt, tip, right);
            DrawLine(btr, insertPt, left, right);
            AddText(btr, insertPt, new Point2d(pos.X + size + 20, pos.Y + size / 2), label, 80);
        }

        [CommandMethod("ViewLine")]
        public void ViewLineInfo()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptEntityOptions peo = new PromptEntityOptions("\n选择要查看的线: ");
            peo.SetRejectMessage("\n必须选择直线、多段线或圆弧!");
            peo.AddAllowedClass(typeof(Line), true);
            peo.AddAllowedClass(typeof(Polyline), true);
            peo.AddAllowedClass(typeof(Arc), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
                if (ent == null) return;

                ed.WriteMessage("\n========== 线实体信息 ==========");
                ed.WriteMessage($"\n实体类型: {ent.GetType().Name}");
                ed.WriteMessage($"\n图层: {ent.Layer}");
                ed.WriteMessage($"\n颜色: {ent.ColorIndex}");
                ed.WriteMessage($"\n线型: {ent.Linetype}");
                ed.WriteMessage($"\n线宽: {ent.LineWeight}");

                if (ent is Line line)
                {
                    DisplayLineInfo(ed, line);
                }
                else if (ent is Polyline pline)
                {
                    DisplayPolylineInfo(ed, pline);
                }
                else if (ent is Arc arc)
                {
                    DisplayArcInfo(ed, arc);
                }

                ed.WriteMessage("\n================================");
                tr.Commit();
            }
        }

        private void DisplayLineInfo(Editor ed, Line line)
        {
            Point3d startPt = line.StartPoint;
            Point3d endPt = line.EndPoint;
            double length = line.Length;
            Vector3d direction = (endPt - startPt).GetNormal();
            double angle = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;
            if (angle < 0) angle += 360;

            ed.WriteMessage("\n---------- 直线信息 ----------");
            ed.WriteMessage($"\n起点坐标: ({startPt.X:F4}, {startPt.Y:F4}, {startPt.Z:F4})");
            ed.WriteMessage($"\n终点坐标: ({endPt.X:F4}, {endPt.Y:F4}, {endPt.Z:F4})");
            ed.WriteMessage($"\n长度: {length:F4}");
            ed.WriteMessage($"\n角度: {angle:F2}°");
            ed.WriteMessage($"\n方向向量: ({direction.X:F4}, {direction.Y:F4}, {direction.Z:F4})");
            ed.WriteMessage($"\n中点坐标: ({(startPt.X + endPt.X) / 2:F4}, {(startPt.Y + endPt.Y) / 2:F4}, {(startPt.Z + endPt.Z) / 2:F4})");
        }

        private void DisplayPolylineInfo(Editor ed, Polyline pline)
        {
            int numVertices = pline.NumberOfVertices;
            double length = pline.Length;
            bool isClosed = pline.Closed;
            double area = isClosed ? pline.Area : 0;

            ed.WriteMessage("\n---------- 多段线信息 ----------");
            ed.WriteMessage($"\n顶点数: {numVertices}");
            ed.WriteMessage($"\n总长度: {length:F4}");
            ed.WriteMessage($"\n是否闭合: {(isClosed ? "是" : "否")}");
            if (isClosed)
            {
                ed.WriteMessage($"\n面积: {area:F4}");
            }

            ed.WriteMessage("\n顶点列表:");
            for (int i = 0; i < numVertices; i++)
            {
                Point3d pt = pline.GetPoint3dAt(i);
                double bulge = pline.GetBulgeAt(i);
                ed.WriteMessage($"\n  顶点 {i + 1}: ({pt.X:F4}, {pt.Y:F4}, {pt.Z:F4})");
                if (bulge != 0)
                {
                    double arcAngle = Math.Atan(Math.Abs(bulge)) * 4 * 180 / Math.PI;
                    ed.WriteMessage($"  凸度: {bulge:F4} (圆弧角度: {arcAngle:F2}°)");
                }
            }
        }

        private void DisplayArcInfo(Editor ed, Arc arc)
        {
            Point3d center = arc.Center;
            double radius = arc.Radius;
            double startAngle = arc.StartAngle * 180 / Math.PI;
            double endAngle = arc.EndAngle * 180 / Math.PI;
            double arcLength = arc.Length;
            double arcAngle = (endAngle - startAngle);
            if (arcAngle < 0) arcAngle += 360;

            ed.WriteMessage("\n---------- 圆弧信息 ----------");
            ed.WriteMessage($"\n圆心坐标: ({center.X:F4}, {center.Y:F4}, {center.Z:F4})");
            ed.WriteMessage($"\n半径: {radius:F4}");
            ed.WriteMessage($"\n起始角度: {startAngle:F2}°");
            ed.WriteMessage($"\n终止角度: {endAngle:F2}°");
            ed.WriteMessage($"\n圆弧角度: {arcAngle:F2}°");
            ed.WriteMessage($"\n弧长: {arcLength:F4}");
            ed.WriteMessage($"\n起点坐标: ({arc.StartPoint.X:F4}, {arc.StartPoint.Y:F4}, {arc.StartPoint.Z:F4})");
            ed.WriteMessage($"\n终点坐标: ({arc.EndPoint.X:F4}, {arc.EndPoint.Y:F4}, {arc.EndPoint.Z:F4})");
        }
    }

    public static class ExtensionMethods
    {
        public static double GetParameterTo(this Point3d basePt, Point3d target, Vector3d direction)
        {
            Vector3d diff = target - basePt;
            return diff.DotProduct(direction);
        }
    }
}

