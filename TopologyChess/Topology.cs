﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace TopologyChess
{
    public partial class Topology
    {
        public static List<Topology> Topologies;

        private static readonly List<Brush> ArrowBrushes;

        static Topology()
        {
            ArrowBrushes = new List<Brush>()
            {
                Brushes.Blue, Brushes.Red, Brushes.Green, Brushes.Yellow,
                Brushes.Cyan, Brushes.Magenta, Brushes.Orange, Brushes.Black
            };

            Topologies = new List<Topology>() {
                new Topology("Flat", "F0"),
                new Topology("Cylinder Vertical", "C1", (3, 1, 1))
                {
                    Equation = Equations.Cylinder
                },
                new Topology("Cylinder Horizontal", "C2", (0, 2, 1))
                {
                    Equation = Equations.Tilt(Equations.Cylinder)
                },
                new Topology("Moebius Vertical", "M1", (3, 1, 0))
                {
                    Equation = Equations.Moebius
                },
                new Topology("Moebius Horizontal", "M2", (0, 2, 0))
                {
                    Equation = Equations.Tilt(Equations.Moebius)
                },
                new Topology("Torus", "T0", (3, 1, 1), (0, 2, 1))
                {
                    Equation = Equations.Torus
                },
                new Topology("Real Projective Plane", "R0", (3, 1, 0), (0, 2, 0))
                {
                    Equation = Equations.ProjectivePlane
                },
                new Topology("Klein Vertical", "K1", (3, 1, 1), (0, 2, 0))
                {
                    Equation = Equations.Tilt(Equations.Klein)
                },
                new Topology("Klein Horizontal", "K2", (3, 1, 0), (0, 2, 1))
                {
                    Equation = Equations.Klein
                },
                new Topology("Globe Vertical", "G1", (3, 1, 1), (0, 0, 2), (2, 2, 2))
                {
                    Equation = Equations.Globe
                },
                new Topology("Globe Horizontal", "G2", (0, 2, 1), (1, 1, 2), (3, 3, 2))
                {
                    Equation = Equations.Tilt(Equations.Globe)
                },
                new Topology("Pillow Vertical", "P1", (3, 1, 1), (0, 0, 1), (2, 2, 1))
                {
                    Equation = Equations.Pillow
                },
                new Topology("Pillow Horizontal", "P2", (0, 2, 1), (1, 1, 1), (3, 3, 1))
                {
                    Equation = Equations.Tilt(Equations.Pillow)
                },
                new Topology("Sphere Right", "S3", (3, 0, 1), (1, 2, 1))
                {
                    Equation = Equations.SphereR
                },
                new Topology("Sphere Left", "S4", (0, 1, 1), (2, 3, 1))
                {
                    Equation = Equations.SphereL
                },
                new Topology("Klein Right", "K3", (0, 3, 0), (1, 2, 0)),
                new Topology("Klein Left", "K4", (0, 1, 0), (2, 3, 0)),
                new Topology("Mirror Vertical", "H1", (3, 3, 0), (1, 1, 0)),
                new Topology("Mirror Horizontal", "H2",(0, 0, 0), (2, 2, 0)),
                new Topology("Mirror Hall", "H5", (0, 0, 0), (1, 1, 0), (2, 2, 0), (3, 3, 0))
            };
        }
        
        public Topology(string name, string id, params (int side1, int side2, int type)[] connection_list)
        {
            Name = name;
            Id = id;
            ConnectionList = connection_list.ToList();
            Connections = new int[4] { -1, -1, -1, -1 };
            Types = new int[4] { -1, -1, -1, -1 };
            foreach (var (side1, side2, type) in connection_list)
            {
                Connections[side1] = side2;
                Connections[side2] = side1;
                Types[side1] = type;
                Types[side2] = type;
            }
            CreateMatrices();
            SetUIs();
        }

        public string Name { get; }

        public string Id { get; }

        public Func<double, double, Point3D> Equation { get; set; } = Equations.Flat;

        public List<(int, int, int)> ConnectionList { get; }

        public int[] Connections { get; }
        public int[] Types { get; }

        public Matrix[] WarpMatrices = new Matrix[4];

        private void CreateMatrices()
        {
            for (int s1 = 0; s1 < 4; s1++)
            {
                Matrix A = Matrix.Identity;
                int s2 = Connections[s1];
                if (s2 == -1) continue;
                A.Translate(-0.5, -0.5);
                A.Rotate(-90 * s1);
                A.Translate(0, 0.5);
                A.Scale(1, -1);
                if (Types[s1] % 2 == 1)
                {
                    A.Scale(-1, 1);
                }
                else if (Types[s1] / 2 == 1)
                {
                    A.Translate(0.5, 0);
                }
                A.Translate(0, -0.5);
                A.Rotate(90 * s2);
                A.Translate(0.5, 0.5);

                A.M11 = Math.Round(A.M11); A.M12 = Math.Round(A.M12);
                A.M21 = Math.Round(A.M21); A.M22 = Math.Round(A.M22);
                A.OffsetX = Math.Round(A.OffsetX, 1); A.OffsetY = Math.Round(A.OffsetY, 1);
                WarpMatrices[s1] = A;
            }
        }

        public static List<int> Sides(IntVector p, int size)
        {
            List<int> result = new List<int>();
            if (p.X < 0) result.Add(3);
            else if (p.X >= size) result.Add(1);
            if (p.Y < 0) result.Add(0);
            else if (p.Y >= size) result.Add(2);
            return result;
        }

        public List<Step> Warp(Step step, int size)
        {
            List<Step> warps = new List<Step>();
            List<int> sides = Sides(step.P, size);
            if (!sides.Any())
            {
                warps.Add(step);
                return warps;
            }
            foreach (int side in sides)
            {
                int side2 = Connections[side];
                if (side2 == -1) continue;
                Step next = new Step();
                Matrix F = Matrix.Identity; F.Translate(0.5, 0.5); F.Scale(1.0 / size, 1.0 / size);
                Matrix B = Matrix.Identity; B.Scale(size, size); B.Translate(-0.5, -0.5);
                Matrix A = WarpMatrices[side];
                next.P = (IntVector)B.Transform(A.Transform(F.Transform((Point)step.P)));
                A.OffsetX = 0; A.OffsetY = 0;
                next.V = (IntVector)A.Transform((Vector)step.V);
                next.M = step.M * A;
                warps = warps.Union(Warp(next, size)).ToList();
            }
            return warps;
        }

        public UniformGrid[] UIs { get; } = new UniformGrid[4];

        public void SetUIs()
        {
            for (int i = 0; i < 4; i++)
            {
                UIs[i] = new UniformGrid()
                {
                    Rows = 1, Columns = 1,
                    LayoutTransform = new RotateTransform(i * 90)
                };
            }
            int pair = 0;
            foreach (var (s1, s2, type) in ConnectionList)
            {
                Brush brush, brush2;
                switch (type)
                {
                    case 0:
                        brush = ArrowBrushes[pair++]; pair %= 8;
                        UIs[s1].Children.Add(new GlueArrow() { ArrowBrush = brush });
                        if (s1 != s2) UIs[s2].Children.Add(new GlueArrow() { ArrowBrush = brush });
                        break;
                    case 1:
                        brush = ArrowBrushes[pair++]; pair %= 8;
                        if (s1 == s2) UIs[s1].Columns = 2;
                        UIs[s1].Children.Add(new GlueArrow() { ArrowBrush = brush });
                        UIs[s2].Children.Add(new GlueArrow()
                        {
                            ArrowBrush = brush,
                            LayoutTransform = new ScaleTransform(-1, 1)
                        });
                        break;
                    case 2:
                        brush = ArrowBrushes[pair++]; pair %= 8;
                        UIs[s1].Columns = 2;
                        if (s1 != s2)
                        {
                            UIs[s2].Columns = 2;
                            brush2 = ArrowBrushes[pair++]; pair %= 8;
                            UIs[s1].Children.Add(new GlueArrow() { ArrowBrush = brush });
                            UIs[s1].Children.Add(new GlueArrow() { ArrowBrush = brush2 });
                            UIs[s2].Children.Add(new GlueArrow() { ArrowBrush = brush2 });
                            UIs[s2].Children.Add(new GlueArrow() { ArrowBrush = brush });
                        }
                        else
                        {
                            UIs[s1].Children.Add(new GlueArrow() { ArrowBrush = brush });
                            UIs[s1].Children.Add(new GlueArrow() { ArrowBrush = brush });
                        }
                        break;
                    case 3:
                        break;
                    default:
                        break;
                }
            }
            for (int i = 0; i < 4; i++)
            {
                if (UIs[i].Children.Count == 0)
                {
                    UIs[i].Children.Add(new GlueArrow() { ArrowBrush = Brushes.Transparent });
                }
            }
        }
    }
}

/*
                СВАЛКА


        private static readonly Matrix[] BorderMatrices = new Matrix[4]
        {
            new ( 1,  0,  0,  1, 0, 0),
            new ( 0, -1,  1,  0, 0, 7),
            new (-1,  0,  0, -1, 7, 7),
            new ( 0,  1, -1,  0, 7, 0)
        };

        private static readonly Matrix3x2[] ToBorder = new Matrix3x2[4]
        {
            new ( 1,  0,  0,  1, 0, 0),
            new ( 0, -1,  1,  0, 0, 7),
            new (-1,  0,  0, -1, 7, 7),
            new ( 0,  1, -1,  0, 7, 0)
        };

        private static readonly Matrix3x2[] FromBorder = new Matrix3x2[4]
        {
            new ( 1,  0,  0,  1, 0, 0),
            new ( 0,  1, -1,  0, 7, 0),
            new (-1,  0,  0, -1, 7, 7),
            new ( 0, -1,  1,  0, 0, 7)
        };

         private static readonly Func<int, int, (int, int)>[] ToBorderCoords = new Func<int, int, (int, int)>[4]
        {
            (x, y) => (x, y),
            (x, y) => (y, 7 - x),
            (x, y) => (7 - x, 7 - y),
            (x, y) => (7 - y, x)
        };

        private static readonly Func<int, int, (int, int)>[] FromBorderCoords = new Func<int, int, (int, int)>[4]
        {
            (P, q) => (P, q),
            (P, q) => (7 - q, P),
            (P, q) => (7 - P, 7 - q),
            (P, q) => (q, 7 - P)
        };

private static int Side(int x, int y)
{
    if (y < 0) return 0;
    else if (x > 7) return 1;
    else if (y > 7) return 2;
    else if (x < 0) return 3;
    else return -1;
}

private static int Side(Vector2 P)
{
    if (P.Y < 0) return 0;
    else if (P.X > 7) return 1;
    else if (P.Y > 7) return 2;
    else if (P.X < 0) return 3;
    else return -1;
}
public (int, int)? Warp(int x, int y)
        {
            int s1, s2;
            while ((s1 = Side(x, y)) != -1)
            {
                s2 = Connections[s1];
                if (s2 == -1) return null;
                (int P, int q) = ToBorderCoords[s1](x, y);
                if (Types[s1] == 1) P = 7 - P;
                else if (Types[s1] == 2) P = (P + 4) % 8;
                (x, y) = FromBorderCoords[s2](P, -q - 1);
            }

            return (x, y);
        }

        public (Vector2, Vector2) Warp(Vector2 P, Vector2 d)
        {
            int s1, s2;
            Vector2 P, D;
            Matrix3x2 A;
            while ((s1 = Side(P)) != -1)
            {
                s2 = Connections[s1];
                if (s2 == -1) return (P, new Vector2(0, 0));
                A = ToBorder[s1];
                P = Vector2.Transform(P, A);
                A.M31 = 0; A.M32 = 0;
                D = Vector2.Transform(d, A);
                P.Y = -P.Y - 1;
                D.Y = -D.Y;
                if (Types[s1] == 1)
                {
                    P.X = 7 - P.X;
                    D.X = -D.X;
                }
                else if (Types[s1] == 2)
                {
                    P.X = (P.X + 4) % 8;
                }
                A = FromBorder[s2];
                P = Vector2.Transform(P, A);
                A.M31 = 0; A.M32 = 0;
                d = Vector2.Transform(D, A);
            }

            return (P, d);
        }
 * 
 */