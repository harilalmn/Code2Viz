# IDE Engineering Specification: "Project Core" (Non-AI Version)

## 1. System Role & Identity
You are a **Systems Architect** specialized in high-performance developer tools. Your objective is to build a robust IDE based on the **Language Server Protocol (LSP)** and **Tree-sitter** for syntax highlighting. No generative AI or external LLM calls are permitted.

---

## 2. Core Architectural Pillars

### A. High-Performance Buffer (The Piece Table)
- **Data Structure:** Implement a **Piece Table** for text storage. 
- **Efficiency:** Ensure $O(1)$ insertion/deletion regardless of file size. 
- **Line Indexing:** Maintain a line-start cache to map byte offsets to `(row, col)` in $O(\log n)$ time.

### B. Static Analysis & LSP Orchestrator
- **Protocol:** Standardized JSON-RPC over `stdio` or `pipes`.
- **Language Servers:** - Python: `pyright` or `jedi-language-server`
    - TS/JS: `typescript-language-server`
    - C/C++: `clangd`
- **Capabilities to Implement:**
    - `textDocument/completion`: Triggered by `.`, `->`, `::`, or `[`.
    - `textDocument/hover`: Triggered by mouse-over or shortcut.
    - `textDocument/definition`: Jump to source code.
    - `textDocument/references`: Find all usages.

### C. Tree-sitter Integration
- **Purpose:** Use Tree-sitter for **Semantic Syntax Highlighting** and **Code Folding**.
- **Advantage:** Faster and more accurate than Regex-based highlighting (TextMate grammars).

---

## 3. Comprehensive AutoComplete Contexts
The system must listen for these specific "Trigger Characters" to fire an LSP request:

| Context | Trigger Character | Logic / Provider |
| :--- | :--- | :--- |
| **Member Access** | `.` , `->` , `::` | LSP `completion` (Filtered by type) |
| **Function Call** | `(` , `,` | LSP `signatureHelp` (Param hints) |
| **Path Inclusion** | `/` , `./` , `"` | Local File System Crawler |
| **Scoped Types** | `<` | LSP (Generic/Template type completion) |
| **Object Literals**| `{` | LSP (Key-value completion for JS/JSON/C#) |
| **CSS Units** | `:` | Local CSS parser (Color hex/pixels/rem) |

---

## 4. UI/UX Logic for IntelliSense
- **The "Widget":** A floating list that tracks the cursor. It must support **Fuzzy Filtering** (typing `clr` matches `color`, `clear`, `classList`).
- **Priority:** Prioritize **Local Scope** variables over Global ones.
- **Documentation:** When a suggestion is highlighted, show a side-car window with the **Docstring/Markdown** provided by the LSP.

---

## 5. Implementation Roadmap: Phase 1
**Task:** Build the **LSP Handshake & Synchronization Logic**.
1. Create a `ServerProcess` class to spawn the LSP.
2. Implement `didOpen` and `didChange` notifications.
3. **Crucial:** Implement "Incremental Sync" (only sending the text diff, not the whole file) to save bandwidth and CPU.