using System;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Console;

namespace CSharpSample
{
    public class Viz
    {
        public static void Main()
        {
            VizConsole.Log("=== Genetic Algorithm Polygon Slicing ===");
            VizConsole.Log("Target ratios: 25%, 20%, 23%, 17%, 15%");
            VizConsole.Log("");

            // Create points for L-shaped polygon
            List<VPoint> basePoints = new List<VPoint>();

            // Disable auto-register while creating base points
            Shape.AutoRegister = false;
            basePoints.Add(new VPoint(0, 0));
            basePoints.Add(new VPoint(200, 0));
            basePoints.Add(new VPoint(200, 100));
            basePoints.Add(new VPoint(100, 100));
            basePoints.Add(new VPoint(100, 200));
            basePoints.Add(new VPoint(0, 200));

            VPolygon polygon = new VPolygon(basePoints);
            double totalArea = polygon.Area;
            Shape.AutoRegister = true;

            VizConsole.Log("Original area: " + totalArea.ToString("F0"));

            // Target ratios (sorted descending)
            double[] sortedTargets = new double[] { 0.25, 0.23, 0.20, 0.17, 0.15 };

            // GA parameters
            Random rng = new Random(42);
            int popSize = 200;
            int generations = 350;

            double bestFitness = 0;
            double[] bestGenes = null;

            VizConsole.Log("Running GA: pop=" + popSize + ", gen=" + generations);

            // Disable auto-register during GA evaluation
            Shape.AutoRegister = false;

            VPoint centroid = new VPoint(75, 100);

            for (int gen = 0; gen < generations; gen++)
            {
                for (int p = 0; p < popSize; p++)
                {
                    // Create genes
                    double[] genes = new double[8];
                    if (bestGenes != null && rng.NextDouble() < 0.7)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            genes[i] = bestGenes[i] + (rng.NextDouble() - 0.5) * 0.3;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            genes[i] = rng.NextDouble() * Math.PI;
                            genes[i + 4] = (rng.NextDouble() - 0.5) * 150;
                        }
                    }

                    // Evaluate fitness by slicing (shapes not registered)
                    VPolygon evalPoly = new VPolygon(basePoints);
                    List<VPolygon> pieces = new List<VPolygon>();
                    pieces.Add(evalPoly);

                    for (int s = 0; s < 4 && pieces.Count < 5; s++)
                    {
                        int largest = 0;
                        double maxArea = 0;
                        for (int i = 0; i < pieces.Count; i++)
                        {
                            if (pieces[i].Area > maxArea)
                            {
                                maxArea = pieces[i].Area;
                                largest = i;
                            }
                        }

                        double angle = genes[s];
                        double offset = genes[s + 4];
                        double dirX = Math.Cos(angle);
                        double dirY = Math.Sin(angle);
                        double baseX = centroid.X + dirX * offset;
                        double baseY = centroid.Y + dirY * offset;
                        double lineX = -dirY;
                        double lineY = dirX;

                        VPoint sp1 = new VPoint(baseX - lineX * 500, baseY - lineY * 500);
                        VPoint sp2 = new VPoint(baseX + lineX * 500, baseY + lineY * 500);

                        List<VPolygon> result = pieces[largest].Slice(sp1, sp2);

                        if (result.Count > 1)
                        {
                            pieces.RemoveAt(largest);
                            for (int r = 0; r < result.Count; r++)
                            {
                                pieces.Add(result[r]);
                            }
                        }
                    }

                    // Calculate fitness
                    if (pieces.Count >= 5)
                    {
                        pieces.Sort((a, b) => b.Area.CompareTo(a.Area));

                        double error = 0;
                        for (int i = 0; i < 5; i++)
                        {
                            double actual = pieces[i].Area / totalArea;
                            double diff = actual - sortedTargets[i];
                            error += diff * diff;
                        }
                        double fitness = 1.0 / (1.0 + Math.Sqrt(error) * 10);

                        if (fitness > bestFitness)
                        {
                            bestFitness = fitness;
                            bestGenes = new double[8];
                            for (int i = 0; i < 8; i++) bestGenes[i] = genes[i];
                        }
                    }
                }

                if (gen % 5 == 0)
                {
                    VizConsole.Log("Gen " + gen + ": fitness=" + bestFitness.ToString("F4"));
                }
            }

            // Re-enable auto-register for final display
            Shape.AutoRegister = true;

            VizConsole.Log("");
            VizConsole.Log("=== Final Result ===");

            if (bestGenes != null)
            {
                // Create final visible polygon
                VPolygon finalPoly = new VPolygon(basePoints);
                finalPoly.Hide();

                // Disable for slicing intermediates
                Shape.AutoRegister = false;

                List<VPolygon> finalPieces = new List<VPolygon>();
                finalPieces.Add(finalPoly);

                for (int s = 0; s < 4 && finalPieces.Count < 5; s++)
                {
                    int largest = 0;
                    double maxArea = 0;
                    for (int i = 0; i < finalPieces.Count; i++)
                    {
                        if (finalPieces[i].Area > maxArea)
                        {
                            maxArea = finalPieces[i].Area;
                            largest = i;
                        }
                    }

                    double angle = bestGenes[s];
                    double offset = bestGenes[s + 4];
                    double dirX = Math.Cos(angle);
                    double dirY = Math.Sin(angle);
                    double baseX = 75 + dirX * offset;
                    double baseY = 100 + dirY * offset;
                    double lineX = -dirY;
                    double lineY = dirX;

                    VPoint sp1 = new VPoint(baseX - lineX * 500, baseY - lineY * 500);
                    VPoint sp2 = new VPoint(baseX + lineX * 500, baseY + lineY * 500);

                    List<VPolygon> result = finalPieces[largest].Slice(sp1, sp2);

                    if (result.Count > 1)
                    {
                        finalPieces.RemoveAt(largest);
                        for (int r = 0; r < result.Count; r++)
                        {
                            finalPieces.Add(result[r]);
                        }
                    }
                }

                // Re-enable and manually register final pieces
                Shape.AutoRegister = true;

                finalPieces.Sort((a, b) => b.Area.CompareTo(a.Area));
                string[] colors = new string[] { "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7" };

                for (int i = 0; i < 5 && i < finalPieces.Count; i++)
                {
                    VPolygon piece = finalPieces[i];

                    // Create new visible polygon from the piece's points
                    VPolygon visiblePiece = new VPolygon(piece.Points);
                    visiblePiece.FillColor = colors[i];
                    visiblePiece.Color = "White";
                    visiblePiece.LineWeight = 2;

                    double actual = piece.Area / totalArea * 100;
                    double target = sortedTargets[i] * 100;
                    VizConsole.Log("Piece " + (i+1) + ": " + actual.ToString("F1") + "% (target " + target.ToString("F1") + "%)");
                }
            }
            else
            {
                VizConsole.Log("Could not find valid solution");
            }

            VizConsole.Log("");
            VizConsole.Log("=== Done ===");
        }
    }
}
