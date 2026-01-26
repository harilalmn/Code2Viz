using System;
using System.Collections.Generic;
using System.Linq;
using Code2Viz.Geometry;
using Code2Viz.Console;

namespace CSharpSample
{
    public class SliceGene
    {
        public double Angle;
        public double Distance;

        public SliceGene(double angle, double distance)
        {
            Angle = angle;
            Distance = distance;
        }

        public SliceGene Clone()
        {
            return new SliceGene(Angle, Distance);
        }
    }

    public class Chromosome
    {
        public List<SliceGene> Genes;
        public double Fitness;
        public List<VPolygon> ResultingPieces;

        public Chromosome(List<SliceGene> genes)
        {
            Genes = genes;
            ResultingPieces = new List<VPolygon>();
        }

        public Chromosome Clone()
        {
            List<SliceGene> clonedGenes = new List<SliceGene>();
            foreach (SliceGene g in Genes)
            {
                clonedGenes.Add(g.Clone());
            }
            Chromosome cloned = new Chromosome(clonedGenes);
            cloned.Fitness = Fitness;
            return cloned;
        }
    }

    public class GeneticPolygonSlicer
    {
        private VPolygon _originalPolygon;
        private double[] _targetRatios;
        private VPoint _centroid;
        private double _maxDistance;
        private Random _random;

        public int PopulationSize = 100;
        public int MaxGenerations = 200;
        public double MutationRate = 0.15;
        public double CrossoverRate = 0.8;
        public double AngleMutationRange = 5.0 * Math.PI / 180.0;
        public double DistanceMutationRange = 10.0;
        public int EliteCount = 5;

        public Chromosome BestSolution;

        public GeneticPolygonSlicer(VPolygon polygon, double[] targetRatios, int seed)
        {
            _originalPolygon = polygon;
            _targetRatios = targetRatios;
            _random = new Random(seed);
            _centroid = CalculateCentroid(polygon);

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            foreach (VPoint p in polygon.Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
            double w = maxX - minX;
            double h = maxY - minY;
            _maxDistance = Math.Sqrt(w * w + h * h) / 2.0;
        }

        private VPoint CalculateCentroid(VPolygon polygon)
        {
            double cx = 0, cy = 0, signedArea = 0;
            for (int i = 0; i < polygon.Points.Count; i++)
            {
                int j = (i + 1) % polygon.Points.Count;
                double x0 = polygon.Points[i].X;
                double y0 = polygon.Points[i].Y;
                double x1 = polygon.Points[j].X;
                double y1 = polygon.Points[j].Y;
                double a = x0 * y1 - x1 * y0;
                signedArea += a;
                cx += (x0 + x1) * a;
                cy += (y0 + y1) * a;
            }
            signedArea *= 0.5;
            cx /= (6.0 * signedArea);
            cy /= (6.0 * signedArea);
            return new VPoint(cx, cy);
        }

        private Chromosome CreateRandomChromosome()
        {
            List<SliceGene> genes = new List<SliceGene>();
            for (int i = 0; i < 4; i++)
            {
                double angle = _random.NextDouble() * Math.PI;
                double distance = (_random.NextDouble() * 2 - 1) * _maxDistance * 0.5;
                genes.Add(new SliceGene(angle, distance));
            }
            return new Chromosome(genes);
        }

        private void GeneToSliceLine(SliceGene gene, out VPoint p1, out VPoint p2)
        {
            double perpX = Math.Cos(gene.Angle);
            double perpY = Math.Sin(gene.Angle);
            double baseX = _centroid.X + perpX * gene.Distance;
            double baseY = _centroid.Y + perpY * gene.Distance;
            double lineX = -perpY;
            double lineY = perpX;
            p1 = new VPoint(baseX - lineX * _maxDistance * 2, baseY - lineY * _maxDistance * 2);
            p2 = new VPoint(baseX + lineX * _maxDistance * 2, baseY + lineY * _maxDistance * 2);
        }

        private List<VPolygon> PerformSequentialSlicing(Chromosome chromosome)
        {
            List<VPolygon> pieces = new List<VPolygon>();
            pieces.Add(_originalPolygon);

            foreach (SliceGene gene in chromosome.Genes)
            {
                if (pieces.Count >= _targetRatios.Length) break;

                int largestIdx = 0;
                double largestArea = pieces[0].Area;
                for (int i = 1; i < pieces.Count; i++)
                {
                    if (pieces[i].Area > largestArea)
                    {
                        largestArea = pieces[i].Area;
                        largestIdx = i;
                    }
                }

                VPoint p1, p2;
                GeneToSliceLine(gene, out p1, out p2);
                List<VPolygon> sliceResult = pieces[largestIdx].Slice(p1, p2);

                if (sliceResult.Count > 1)
                {
                    pieces.RemoveAt(largestIdx);
                    foreach (VPolygon sp in sliceResult)
                    {
                        pieces.Add(sp);
                    }
                }
            }
            return pieces;
        }

        private double CalculateFitness(Chromosome chromosome)
        {
            List<VPolygon> pieces = PerformSequentialSlicing(chromosome);
            chromosome.ResultingPieces = pieces;

            if (pieces.Count < _targetRatios.Length)
            {
                return 0.01 * pieces.Count / _targetRatios.Length;
            }

            double totalArea = 0;
            foreach (VPolygon p in pieces) totalArea += p.Area;
            if (totalArea < 1e-10) return 0.001;

            List<double> ratioList = new List<double>();
            foreach (VPolygon p in pieces) ratioList.Add(p.Area / totalArea);
            ratioList.Sort();
            ratioList.Reverse();
            double[] actualRatios = ratioList.Take(_targetRatios.Length).ToArray();

            double[] sortedTargets = _targetRatios.OrderByDescending(r => r).ToArray();

            double error = 0;
            for (int i = 0; i < sortedTargets.Length; i++)
            {
                double diff = actualRatios[i] - sortedTargets[i];
                error += diff * diff;
            }

            double rmse = Math.Sqrt(error / sortedTargets.Length);
            return 1.0 / (1.0 + rmse * 10);
        }

        private Chromosome TournamentSelect(List<Chromosome> population, int tournamentSize)
        {
            Chromosome best = null;
            for (int i = 0; i < tournamentSize; i++)
            {
                Chromosome candidate = population[_random.Next(population.Count)];
                if (best == null || candidate.Fitness > best.Fitness)
                    best = candidate;
            }
            return best;
        }

        private void Crossover(Chromosome parent1, Chromosome parent2, out Chromosome child1, out Chromosome child2)
        {
            if (_random.NextDouble() > CrossoverRate)
            {
                child1 = parent1.Clone();
                child2 = parent2.Clone();
                return;
            }

            int crossPoint = _random.Next(1, parent1.Genes.Count);

            List<SliceGene> child1Genes = new List<SliceGene>();
            List<SliceGene> child2Genes = new List<SliceGene>();

            for (int i = 0; i < crossPoint; i++)
            {
                child1Genes.Add(parent1.Genes[i].Clone());
                child2Genes.Add(parent2.Genes[i].Clone());
            }
            for (int i = crossPoint; i < parent1.Genes.Count; i++)
            {
                child1Genes.Add(parent2.Genes[i].Clone());
                child2Genes.Add(parent1.Genes[i].Clone());
            }

            child1 = new Chromosome(child1Genes);
            child2 = new Chromosome(child2Genes);
        }

        private void Mutate(Chromosome chromosome)
        {
            foreach (SliceGene gene in chromosome.Genes)
            {
                if (_random.NextDouble() < MutationRate)
                {
                    gene.Angle += (_random.NextDouble() * 2 - 1) * AngleMutationRange;
                    gene.Angle = gene.Angle % Math.PI;
                    if (gene.Angle < 0) gene.Angle += Math.PI;
                }
                if (_random.NextDouble() < MutationRate)
                {
                    gene.Distance += (_random.NextDouble() * 2 - 1) * DistanceMutationRange;
                    gene.Distance = Math.Max(-_maxDistance * 0.8, Math.Min(_maxDistance * 0.8, gene.Distance));
                }
            }
        }

        public Chromosome Evolve(bool verbose)
        {
            List<Chromosome> population = new List<Chromosome>();
            for (int i = 0; i < PopulationSize; i++)
            {
                Chromosome chromosome = CreateRandomChromosome();
                chromosome.Fitness = CalculateFitness(chromosome);
                population.Add(chromosome);
            }

            population = population.OrderByDescending(c => c.Fitness).ToList();
            BestSolution = population[0].Clone();
            BestSolution.ResultingPieces = population[0].ResultingPieces;

            for (int gen = 0; gen < MaxGenerations; gen++)
            {
                population = population.OrderByDescending(c => c.Fitness).ToList();

                if (population[0].Fitness > BestSolution.Fitness)
                {
                    BestSolution = population[0].Clone();
                    BestSolution.ResultingPieces = population[0].ResultingPieces;
                }

                if (verbose && (gen % 10 == 0 || gen == MaxGenerations - 1))
                {
                    VizConsole.Log("Gen " + gen + ": Fitness = " + BestSolution.Fitness.ToString("F4"));
                }

                List<Chromosome> newPopulation = new List<Chromosome>();

                for (int i = 0; i < EliteCount && i < population.Count; i++)
                {
                    Chromosome elite = population[i].Clone();
                    elite.ResultingPieces = population[i].ResultingPieces;
                    newPopulation.Add(elite);
                }

                while (newPopulation.Count < PopulationSize)
                {
                    Chromosome parent1 = TournamentSelect(population, 3);
                    Chromosome parent2 = TournamentSelect(population, 3);
                    Chromosome child1, child2;
                    Crossover(parent1, parent2, out child1, out child2);

                    Mutate(child1);
                    Mutate(child2);

                    child1.Fitness = CalculateFitness(child1);
                    child2.Fitness = CalculateFitness(child2);

                    newPopulation.Add(child1);
                    if (newPopulation.Count < PopulationSize)
                        newPopulation.Add(child2);
                }

                population = newPopulation;
            }

            BestSolution.Fitness = CalculateFitness(BestSolution);
            return BestSolution;
        }

        public void Visualize()
        {
            if (BestSolution == null || BestSolution.ResultingPieces.Count == 0)
            {
                VizConsole.Log("No solution to visualize. Run Evolve() first.");
                return;
            }

            string[] colors = new string[] { "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7", "#DDA0DD", "#98D8C8" };

            double totalArea = 0;
            foreach (VPolygon p in BestSolution.ResultingPieces) totalArea += p.Area;

            VizConsole.Log("");
            VizConsole.Log("=== Final Solution ===");
            VizConsole.Log("Fitness: " + BestSolution.Fitness.ToString("F4"));
            VizConsole.Log("Number of pieces: " + BestSolution.ResultingPieces.Count);

            List<VPolygon> sortedPieces = BestSolution.ResultingPieces.OrderByDescending(p => p.Area).ToList();
            double[] sortedTargets = _targetRatios.OrderByDescending(r => r).ToArray();

            VizConsole.Log("");
            VizConsole.Log("Target vs Actual Ratios:");
            for (int i = 0; i < sortedPieces.Count && i < sortedTargets.Length; i++)
            {
                VPolygon piece = sortedPieces[i];
                double actualRatio = piece.Area / totalArea;
                double targetRatio = sortedTargets[i];
                double error = Math.Abs(actualRatio - targetRatio) * 100;

                piece.FillColor = colors[i % colors.Length];
                piece.Color = "White";
                piece.LineWeight = 2;

                VizConsole.Log("  Piece " + (i + 1) + ": Target=" + (targetRatio * 100).ToString("F1") + "%, Actual=" + (actualRatio * 100).ToString("F1") + "%, Error=" + error.ToString("F2") + "%");
            }

            VPoint centroidMarker = new VPoint(_centroid.X, _centroid.Y);
            centroidMarker.Color = "Yellow";

            foreach (SliceGene gene in BestSolution.Genes)
            {
                VPoint p1, p2;
                GeneToSliceLine(gene, out p1, out p2);
                VLine sliceLine = new VLine(p1, p2);
                sliceLine.Color = "Gray";
                sliceLine.LineWeight = 1;
            }
        }
    }
}
