namespace FSharpSample

open System
open Code2Viz.Geometry
open VizDsl

/// Functional genetic algorithm for polygon slicing
module GeneticSlicer =

    // ============================================================================
    // Types
    // ============================================================================

    /// A gene represents a slicing line defined by angle and distance from centroid
    type SliceGene = {
        Angle: float      // Radians
        Distance: float   // Distance from centroid
    }

    /// A chromosome is a candidate solution with 4 genes (for 5 pieces)
    type Chromosome = {
        Genes: SliceGene list
        mutable Fitness: float
        mutable ResultingPieces: VPolygon list
    }

    /// Configuration for the genetic algorithm
    type GaConfig = {
        PopulationSize: int
        MaxGenerations: int
        MutationRate: float
        CrossoverRate: float
        AngleMutationRange: float   // Radians
        DistanceMutationRange: float
        EliteCount: int
        TargetRatios: float array
    }

    let defaultConfig = {
        PopulationSize = 100
        MaxGenerations = 200
        MutationRate = 0.15
        CrossoverRate = 0.8
        AngleMutationRange = 5.0 * Math.PI / 180.0
        DistanceMutationRange = 10.0
        EliteCount = 5
        TargetRatios = [| 0.25; 0.20; 0.23; 0.17; 0.15 |]
    }

    // ============================================================================
    // Pure Functions
    // ============================================================================

    /// Calculate centroid of a polygon
    let calculateCentroid (polygon: VPolygon) =
        let mutable cx, cy, signedArea = 0.0, 0.0, 0.0
        let pts = polygon.Points
        for i in 0 .. pts.Count - 1 do
            let j = (i + 1) % pts.Count
            let x0, y0 = pts.[i].X, pts.[i].Y
            let x1, y1 = pts.[j].X, pts.[j].Y
            let a = x0 * y1 - x1 * y0
            signedArea <- signedArea + a
            cx <- cx + (x0 + x1) * a
            cy <- cy + (y0 + y1) * a
        signedArea <- signedArea * 0.5
        VPoint(cx / (6.0 * signedArea), cy / (6.0 * signedArea))

    /// Calculate max distance from centroid to bounding box corners
    let getMaxDistance (polygon: VPolygon) =
        let minX = polygon.Points |> Seq.map (fun p -> p.X) |> Seq.min
        let maxX = polygon.Points |> Seq.map (fun p -> p.X) |> Seq.max
        let minY = polygon.Points |> Seq.map (fun p -> p.Y) |> Seq.min
        let maxY = polygon.Points |> Seq.map (fun p -> p.Y) |> Seq.max
        let w, h = maxX - minX, maxY - minY
        sqrt(w * w + h * h) / 2.0

    /// Convert a gene to a slice line (two points defining the line)
    let geneToSliceLine (centroid: VPoint) (maxDist: float) (gene: SliceGene) =
        // Perpendicular direction for offset
        let perpX = cos gene.Angle
        let perpY = sin gene.Angle
        // Base point on slice line
        let baseX = centroid.X + perpX * gene.Distance
        let baseY = centroid.Y + perpY * gene.Distance
        // Line direction (perpendicular to offset)
        let lineX, lineY = -perpY, perpX
        // Two far points
        let p1 = VPoint(baseX - lineX * maxDist * 2.0, baseY - lineY * maxDist * 2.0)
        let p2 = VPoint(baseX + lineX * maxDist * 2.0, baseY + lineY * maxDist * 2.0)
        (p1, p2)

    /// Sequential slicing: always slice the largest piece
    let performSequentialSlicing (polygon: VPolygon) (centroid: VPoint) (maxDist: float) (genes: SliceGene list) (targetCount: int) =
        let rec sliceLoop pieces remainingGenes =
            match remainingGenes with
            | [] -> pieces
            | _ when List.length pieces >= targetCount -> pieces
            | gene :: rest ->
                // Find largest piece
                let largestIdx, _ =
                    pieces
                    |> List.mapi (fun i p -> (i, (p: VPolygon).Area))
                    |> List.maxBy snd
                let largest = pieces.[largestIdx]
                // Slice it
                let (p1, p2) = geneToSliceLine centroid maxDist gene
                let sliceResult = largest.Slice(p1, p2)
                if sliceResult.Count > 1 then
                    let newPieces =
                        pieces
                        |> List.mapi (fun i p -> if i = largestIdx then None else Some p)
                        |> List.choose id
                        |> List.append (sliceResult |> Seq.toList)
                    sliceLoop newPieces rest
                else
                    sliceLoop pieces rest
        sliceLoop [polygon] genes

    /// Calculate fitness (higher is better, max ~1.0)
    let calculateFitness (polygon: VPolygon) (centroid: VPoint) (maxDist: float) (config: GaConfig) (chromosome: Chromosome) =
        let pieces = performSequentialSlicing polygon centroid maxDist chromosome.Genes config.TargetRatios.Length
        chromosome.ResultingPieces <- pieces

        if pieces.Length < config.TargetRatios.Length then
            0.01 * float pieces.Length / float config.TargetRatios.Length
        else
            let totalArea = pieces |> List.sumBy (fun (p: VPolygon) -> p.Area)
            if totalArea < 1e-10 then 0.001
            else
                let actualRatios =
                    pieces
                    |> List.map (fun p -> p.Area / totalArea)
                    |> List.sortByDescending id
                    |> List.take config.TargetRatios.Length
                    |> List.toArray
                let sortedTargets = config.TargetRatios |> Array.sortByDescending id
                let error =
                    Array.zip actualRatios sortedTargets
                    |> Array.sumBy (fun (a, t) -> (a - t) ** 2.0)
                let rmse = sqrt(error / float sortedTargets.Length)
                1.0 / (1.0 + rmse * 10.0)

    // ============================================================================
    // GA Operations
    // ============================================================================

    /// Create a random gene
    let randomGene (rng: Random) (maxDist: float) =
        {
            Angle = rng.NextDouble() * Math.PI
            Distance = (rng.NextDouble() * 2.0 - 1.0) * maxDist * 0.5
        }

    /// Create a random chromosome
    let randomChromosome (rng: Random) (maxDist: float) =
        {
            Genes = [ for _ in 1..4 -> randomGene rng maxDist ]
            Fitness = 0.0
            ResultingPieces = []
        }

    /// Clone a gene
    let cloneGene gene = { gene with Angle = gene.Angle }

    /// Clone a chromosome
    let cloneChromosome chrom =
        { chrom with
            Genes = chrom.Genes |> List.map cloneGene
            ResultingPieces = chrom.ResultingPieces }

    /// Tournament selection
    let tournamentSelect (rng: Random) (tournamentSize: int) (population: Chromosome list) =
        [1..tournamentSize]
        |> List.map (fun _ -> population.[rng.Next(population.Length)])
        |> List.maxBy (fun c -> c.Fitness)

    /// Single-point crossover
    let crossover (rng: Random) (config: GaConfig) (parent1: Chromosome) (parent2: Chromosome) =
        if rng.NextDouble() > config.CrossoverRate then
            (cloneChromosome parent1, cloneChromosome parent2)
        else
            let crossPoint = rng.Next(1, parent1.Genes.Length)
            let child1Genes =
                (parent1.Genes |> List.take crossPoint)
                @ (parent2.Genes |> List.skip crossPoint)
                |> List.map cloneGene
            let child2Genes =
                (parent2.Genes |> List.take crossPoint)
                @ (parent1.Genes |> List.skip crossPoint)
                |> List.map cloneGene
            ({ Genes = child1Genes; Fitness = 0.0; ResultingPieces = [] },
             { Genes = child2Genes; Fitness = 0.0; ResultingPieces = [] })

    /// Mutate a chromosome in place
    let mutate (rng: Random) (config: GaConfig) (maxDist: float) (chromosome: Chromosome) =
        chromosome.Genes
        |> List.map (fun gene ->
            let mutable g = { gene with Angle = gene.Angle }
            if rng.NextDouble() < config.MutationRate then
                g <- { g with Angle = (g.Angle + (rng.NextDouble() * 2.0 - 1.0) * config.AngleMutationRange) % Math.PI }
                if g.Angle < 0.0 then g <- { g with Angle = g.Angle + Math.PI }
            if rng.NextDouble() < config.MutationRate then
                let newDist = g.Distance + (rng.NextDouble() * 2.0 - 1.0) * config.DistanceMutationRange
                g <- { g with Distance = max (-maxDist * 0.8) (min (maxDist * 0.8) newDist) }
            g)
        |> fun newGenes -> { chromosome with Genes = newGenes }

    // ============================================================================
    // Main Evolution Loop
    // ============================================================================

    /// Run the genetic algorithm
    let evolve (polygon: VPolygon) (config: GaConfig) (seed: int option) (verbose: bool) =
        let rng = match seed with Some s -> Random(s) | None -> Random()
        let centroid = calculateCentroid polygon
        let maxDist = getMaxDistance polygon

        // Initialize population
        let mutable population =
            [ for _ in 1..config.PopulationSize ->
                let c = randomChromosome rng maxDist
                c.Fitness <- calculateFitness polygon centroid maxDist config c
                c ]

        let mutable bestSolution = population |> List.maxBy (fun c -> c.Fitness) |> cloneChromosome

        for gen in 0 .. config.MaxGenerations - 1 do
            // Sort by fitness
            population <- population |> List.sortByDescending (fun c -> c.Fitness)

            // Update best
            if population.[0].Fitness > bestSolution.Fitness then
                bestSolution <- cloneChromosome population.[0]
                bestSolution.ResultingPieces <- population.[0].ResultingPieces

            // Report
            if verbose && (gen % 20 = 0 || gen = config.MaxGenerations - 1) then
                printfn "Gen %d: Best Fitness = %.4f" gen bestSolution.Fitness

            // Create new population
            let mutable newPopulation =
                population
                |> List.take (min config.EliteCount population.Length)
                |> List.map cloneChromosome

            while newPopulation.Length < config.PopulationSize do
                let parent1 = tournamentSelect rng 3 population
                let parent2 = tournamentSelect rng 3 population
                let (child1, child2) = crossover rng config parent1 parent2
                let c1 = mutate rng config maxDist child1
                let c2 = mutate rng config maxDist child2
                c1.Fitness <- calculateFitness polygon centroid maxDist config c1
                c2.Fitness <- calculateFitness polygon centroid maxDist config c2
                newPopulation <- c1 :: newPopulation
                if newPopulation.Length < config.PopulationSize then
                    newPopulation <- c2 :: newPopulation

            population <- newPopulation

        bestSolution.Fitness <- calculateFitness polygon centroid maxDist config bestSolution
        (bestSolution, centroid, maxDist)

    // ============================================================================
    // Visualization
    // ============================================================================

    /// Visualize the best solution
    let visualize (config: GaConfig) (solution: Chromosome) (centroid: VPoint) (maxDist: float) =
        let colors = [| "#FF6B6B"; "#4ECDC4"; "#45B7D1"; "#96CEB4"; "#FFEAA7"; "#DDA0DD"; "#98D8C8" |]

        printfn ""
        printfn "=== Final Solution ==="
        printfn "Fitness: %.4f" solution.Fitness
        printfn "Number of pieces: %d" solution.ResultingPieces.Length

        let totalArea = solution.ResultingPieces |> List.sumBy (fun (p: VPolygon) -> p.Area)
        let sortedPieces =
            solution.ResultingPieces
            |> List.sortByDescending (fun p -> p.Area)
        let sortedTargets = config.TargetRatios |> Array.sortByDescending id

        printfn ""
        printfn "Target vs Actual Ratios:"
        sortedPieces
        |> List.iteri (fun i piece ->
            if i < sortedTargets.Length then
                let actualRatio = piece.Area / totalArea
                let targetRatio = sortedTargets.[i]
                let error = abs(actualRatio - targetRatio) * 100.0

                piece.FillColor <- colors.[i % colors.Length]
                piece.Color <- "White"
                piece.StrokeThickness <- 2.0

                printfn "  Piece %d: Target=%.1f%%, Actual=%.1f%%, Error=%.2f%%"
                    (i + 1) (targetRatio * 100.0) (actualRatio * 100.0) error)

        // Draw centroid
        point centroid.X centroid.Y
        |> withStroke "Yellow"
        |> draw
        |> ignore

        // Draw slice lines
        solution.Genes
        |> List.iter (fun gene ->
            let (p1, p2) = geneToSliceLine centroid maxDist gene
            line p1.X p1.Y p2.X p2.Y
            |> withStroke "Gray"
            |> withThickness 1.0
            |> draw
            |> ignore)
