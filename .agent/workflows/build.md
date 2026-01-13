---
description: Build the Viz2d project
---

// turbo-all

1. Run `dotnet build` in the project directory:
   ```powershell
   dotnet build
   ```

2. If build fails due to file lock (Viz2d.exe in use), kill the process first:
   ```powershell
   taskkill /F /IM Viz2d.exe
   dotnet build
   ```
