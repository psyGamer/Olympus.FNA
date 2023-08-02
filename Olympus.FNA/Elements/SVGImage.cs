using Microsoft.Xna.Framework;
using OlympUI;
using Olympus.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Olympus {
    public partial class SVGImage : Element {

        public static readonly new Style DefaultStyle = new() {
            {
                StyleKeys.Normal,
                new Style() { new Color(0xff, 0xff, 0xff, 0xff), }
            },
        };

        protected override bool IsComposited => false;
        
        public int AutoW {
            get {
                return (int) ((H / (float) data.Height) * data.Width);
            }
            set {
                W = value;
                H = AutoH;
            }
        }

        public int AutoH {
            get {
                return (int) ((W / (float) data.Width) * data.Height);
            }
            set {
                H = value;
                W = AutoW;
            }
        }

        private readonly SVGObject data;
        
        private BasicMesh Mesh;
        private Color PrevColor;
        private Point PrevWH;
        private float PrevProgress;

        public bool? Enabled;

        protected Style.Entry StyleColor = new(new ColorFader());
        private readonly Func<float> ProgressCb;
        private float Progress = 0f;

        public SVGImage(string path, Func<float> progressCb) {
            ProgressCb = progressCb;
            Cached = false;
            Mesh = new BasicMesh(UI.Game) {
                Texture = OlympUI.Assets.White,
            };
            data = new SVGObject(Encoding.Default.GetString(OlympUI.Assets.OpenData(path) 
                                                            ?? throw new FileNotFoundException($"Couldn't find asset: {path}")));
            
            WH = new Point(data.Width, data.Height);
        }

        public override void Update(float dt) {
            Style.Apply(StyleKeys.Normal);
            
            Progress = ProgressCb.Invoke();
            if (Progress > 1) 
                Progress = 1;
            if (Math.Abs(PrevProgress - Progress) > 0.005f) {
                InvalidatePaint();
            }
            
            base.Update(dt);
        }

        public override void DrawContent() {
            StyleColor.GetCurrent(out Color color);

            Vector2 xy = ScreenXY;
            Point wh = WH;
            
            if (color != PrevColor ||
                Math.Abs(Progress - PrevProgress) > 0.005) {
                PrevColor = color;
                PrevProgress = Progress;
                
                MeshShapes<MiniVertex> shapes = Mesh.Shapes;
                shapes.Clear();
                
                foreach (SVGGroup group in data.Groups) {
                    foreach (SVGPath path in group.Paths) {
                        Vector2 currPos = new(0, 0);
                        float commandsToDraw = path.RenderCommandCount * Progress;
                        foreach (SVGCommand cmd in path.Commands) {
                            float fraction;
                            if (cmd.IsVisible()) {
                                fraction = MathF.Min(commandsToDraw, 1f);
                            } else {
                                fraction = 1f;
                            }

                            if (commandsToDraw < 0) break;
                            if (cmd.IsVisible())
                                commandsToDraw--;
                            switch (cmd.Type) {
                                case SVGCommand.SVGCommandType.MoveTo:
                                    currPos = (cmd.Relative ? currPos : Vector2.Zero) +
                                              new Vector2(cmd.Values[0], cmd.Values[1]);
                                    break;
                                case SVGCommand.SVGCommandType.LineTo:
                                    Vector2 endPos = new(cmd.Values[0], cmd.Values[1]);
                                    endPos += (currPos - endPos) * (1f - fraction);
                                    shapes.Add(new MeshShapes.Line() {
                                        XY1 = currPos,
                                        XY2 = (cmd.Relative ? currPos : Vector2.Zero) +
                                              endPos,
                                        Radius = group.StrokeWidth,
                                        Color = color,
                                    });
                                    currPos = (cmd.Relative ? currPos : Vector2.Zero) +
                                              new Vector2(cmd.Values[0], cmd.Values[1]);
                                    break;
                                case SVGCommand.SVGCommandType.CubicCurve:
                                    throw new NotImplementedException(
                                        "Please use Arc where possible (or implement it yourself)");
                                case SVGCommand.SVGCommandType.QuadraticCurve:
                                    throw new NotImplementedException(
                                        "Please use Arc where possible (or implement it yourself)");
                                case SVGCommand.SVGCommandType.ArcCurve:
                                    float rx = MathF.Abs(cmd.Values[0]);
                                    float ry = MathF.Abs(cmd.Values[1]);
                                    float rot = cmd.Values[2] * 2 * MathF.PI / 360;
                                    int largeArcFlag = (int) cmd.Values[3];
                                    int sweepFlag = (int) cmd.Values[4];
                                    float finalX = cmd.Values[5];
                                    float finalY = cmd.Values[6];

                                    if (currPos.X == finalX && currPos.Y == finalY) {
                                        // if points are equal, skip rendering
                                        break;
                                    }

                                    // we should draw a line, but im lazy...
                                    if (rx == 0 || ry == 0) {
                                        break;
                                    }

                                    // The following is based from https://www.w3.org/TR/SVG/implnote.html#ArcConversionEndpointToCenter
                                    // And partially copied from https://github.com/MadLittleMods/svg-curve-lib/blob/50b7761814384ba2497bb63cfa5417cb6cd85a5f/src/c%2B%2B/SVGCurveLib.cpp#L70
                                    float dx = (currPos.X - finalX) / 2;
                                    float dy = (currPos.Y - finalY) / 2;
                                    Vector2 transformedPoint = new(
                                        MathF.Cos(rot) * dx + MathF.Sin(rot) * dy,
                                        -MathF.Sin(rot) * dx + MathF.Cos(rot) * dy
                                    );

                                    float radiiCheck = MathF.Pow(transformedPoint.X, 2) / MathF.Pow(rx, 2)
                                                       + MathF.Pow(transformedPoint.Y, 2) / MathF.Pow(ry, 2);
                                    if (radiiCheck > 1) {
                                        rx = MathF.Sqrt(radiiCheck) * rx;
                                        ry = MathF.Sqrt(radiiCheck) * ry;
                                    }

                                    float cSquareNumerator = MathF.Pow(rx, 2) * MathF.Pow(ry, 2)
                                                             - MathF.Pow(rx, 2) * MathF.Pow(transformedPoint.Y, 2)
                                                             - MathF.Pow(ry, 2) * MathF.Pow(transformedPoint.X, 2);
                                    float cSquareRootDenom = MathF.Pow(rx, 2)
                                                             * MathF.Pow(transformedPoint.Y, 2)
                                                             + MathF.Pow(ry, 2) * MathF.Pow(transformedPoint.X, 2);
                                    float cRadicand = cSquareNumerator / cSquareRootDenom;

                                    // Make sure this never drops below zero because of precision
                                    cRadicand = cRadicand < 0 ? 0 : cRadicand;

                                    float cCoef = (largeArcFlag != sweepFlag ? 1 : -1) * MathF.Sqrt(cRadicand);
                                    Vector2 transformedCenter = new(
                                        cCoef * ((rx * transformedPoint.Y) / ry),
                                        cCoef * (-(ry * transformedPoint.X) / rx)
                                    );

                                    Vector2 center = new(
                                        MathF.Cos(rot) * transformedCenter.X - MathF.Sin(rot) * transformedCenter.Y +
                                        ((currPos.X + finalX) / 2),
                                        MathF.Sin(rot) * transformedCenter.X + MathF.Cos(rot) * transformedCenter.Y +
                                        ((currPos.Y + finalY) / 2)
                                    );

                                    // Now find the angles
                                    float AngleBetween(Vector2 a, Vector2 b) {
                                        float numerator = a.X * b.X + a.Y * b.Y;
                                        float denominator = MathF.Sqrt(
                                            (MathF.Pow(a.X, 2) + MathF.Pow(a.Y, 2)) *
                                            (MathF.Pow(b.X, 2) + MathF.Pow(b.Y, 2))
                                        );
                                        float sign = a.X * b.Y - b.X * a.Y < 0 ? -1f : 1f;
                                        return sign * MathF.Acos(numerator / denominator);
                                    }

                                    float startAngle = AngleBetween(new Vector2(1f, 0f),
                                        new Vector2(
                                            (transformedPoint.X - transformedCenter.X) / rx,
                                            (transformedPoint.Y - transformedCenter.Y) / ry));

                                    float increment = AngleBetween(
                                        new Vector2(
                                            (transformedPoint.X - transformedCenter.X) / rx,
                                            (transformedPoint.Y - transformedCenter.Y) / ry),
                                        new Vector2(
                                            (-transformedPoint.X - transformedCenter.X) / rx,
                                            (-transformedPoint.Y - transformedCenter.Y) / ry)
                                    );

                                    if (sweepFlag == 0 && increment > 0) {
                                        increment -= 2 * MathF.PI;
                                    } else if (sweepFlag == 1 && increment < 0) {
                                        increment += 2 * MathF.PI;
                                    }

                                    float endAngle = startAngle + increment;

                                    if (MathF.Abs(startAngle - endAngle) > 2 * MathF.PI) {
                                        // what
                                        throw new ArithmeticException(
                                            "startAngle or endAngle were miscalculated: abs(startAngle-endAngle)>2*PI");
                                    }

                                    endAngle -= (endAngle - startAngle) * (1f - fraction);

                                    shapes.Add(new MeshShapes.Arc() {
                                        RadiusX = rx,
                                        RadiusY = ry,
                                        XY = center,
                                        AngleStart = startAngle,
                                        AngleEnd = endAngle,
                                        Color = color,
                                        Width = group.StrokeWidth,
                                        RoundedCap = group.StrokeLineCap == SVGStrokeLineCap.Round,
                                    });
                                    currPos = new(finalX, finalY);
                                    break;
                                case SVGCommand.SVGCommandType.ClosePath:
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(cmd.ToString(), "Unknown svg command type");
                            }
                        }
                    }
                    
                }
                
                shapes.AutoApply();
            }
            
            UIDraw.Recorder.Add((Mesh, xy), static ((BasicMesh mesh, Vector2 xy) data) => {
                UI.SpriteBatch.End();
                data.mesh.Draw(UI.CreateTransform(data.xy));
                UI.SpriteBatch.BeginUI();
            });
            
            base.DrawContent();
        }

        public new abstract partial class StyleKeys {
            public static readonly Style.Key Normal = new("Normal");
        }
    }
}