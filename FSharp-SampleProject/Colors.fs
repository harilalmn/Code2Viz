namespace FSharpSample

/// Color palette module with predefined colors
module Colors =
	// Primary colors
	let Red = "#E74C3C"
	let Blue = "#3498DB"
	let Green = "#2ECC71"
	let Yellow = "#F1C40F"
	let Orange = "#E67E22"
	let Purple = "#9B59B6"

	// Neutral colors
	let White = "#FFFFFF"
	let Black = "#000000"
	let Gray = "#95A5A6"
	let DarkGray = "#34495E"

	// Get a color by index (cycles through palette)
	let getColorByIndex (index: int) =
		let palette = [| Red; Blue; Green; Yellow; Orange; Purple |]
		palette.[index % palette.Length]
